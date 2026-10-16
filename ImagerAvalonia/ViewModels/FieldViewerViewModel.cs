
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Exceptions;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Utils;
using ImagerAvalonia.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;


namespace ImagerAvalonia.ViewModels;

public partial class FieldViewerViewModel : ViewModelBase
{

    IStageControl? _stageControl;

    [ObservableProperty] XYStagePosition _currentStagePosition;
    [ObservableProperty] bool _isStageViewerEngaged = false;
    [ObservableProperty] double _pixelSize = 100;
    [ObservableProperty] bool _isMeasurementRunning = false;
    [ObservableProperty] bool _isStageMoveInFlight = false;

    /// <summary>
    /// Moves beyond this distance (in micrometers) ask the user to confirm
    /// before the stage is commanded.
    /// </summary>
    public const double FarMoveThresholdUm = 500;

    /// <summary>
    /// Confirmation callback for far moves (distance in micrometers);
    /// injectable so tests can answer without a window. Defaults to the
    /// MoveStageConfirmation dialog.
    /// </summary>
    public Func<double, Task<bool>>? MoveConfirmation { get; set; }

    [ObservableProperty] ObservableCollection<string> _Acquisitions = new();
    [ObservableProperty] ObservableCollection<string> _Detectors = new();

    [ObservableProperty] string? _SelectedDetector;
    [ObservableProperty] string? _SelectedAcquisition;


    public event EventHandler? FocusViewInitialized;
    public event EventHandler<ImageData>? UpdateImageData;


    public void FindCurrentStagePosition()
    {
        CurrentStagePosition = _stageControl != null ? _stageControl.ReadStagePosition() : new XYStagePosition(0, 0, 0, 0, false,"DefaultPosition");
    }

    public void EngageFieldViewer()
    {
        // The anchor is read when engaging only: disengaging must not move it.
        // An unreadable position leaves the anchoring to the first frame.
        if (!IsStageViewerEngaged)
            FindCurrentStagePosition();

        IsStageViewerEngaged = !IsStageViewerEngaged;
    }

    public void SetGridData(List<Tuple<string,string>> acq_det_pairs)
    {
        // Clearing the item sources resets the combo selections; keep them if
        // the new schema still contains the same names (a live session restart
        // typically uses the same schema).
        string? selectedAcquisition = SelectedAcquisition;
        string? selectedDetector = SelectedDetector;

        Acquisitions.Clear();
        Detectors.Clear();
        if (acq_det_pairs == null) return;

        foreach (Tuple<string, string> pair in acq_det_pairs)
        {
            if (!Acquisitions.Contains(pair.Item1))
                Acquisitions.Add(pair.Item1);

            if (!Detectors.Contains(pair.Item2))
                Detectors.Add(pair.Item2);
        }

        SelectedAcquisition = selectedAcquisition != null && Acquisitions.Contains(selectedAcquisition)
            ? selectedAcquisition
            : null;
        SelectedDetector = selectedDetector != null && Detectors.Contains(selectedDetector)
            ? selectedDetector
            : null;

        FocusViewInitialized?.Invoke(this, new EventArgs());
    }

    public void ProcessImages(object? sender, ImageData images)
    {
        if (IsStageViewerEngaged)
        {
            UpdateImageData?.Invoke(this, images);
        }
    }

    /// <summary>
    /// Commands the stage to an absolute position picked on the map. XY only:
    /// Z and the hardware-autofocus state are taken from a fresh stage read.
    /// Blocked while a measurement runs or another move is in flight; moves
    /// beyond FarMoveThresholdUm ask for confirmation first.
    /// </summary>
    public async Task MoveStageTo(double stageX, double stageY)
    {
        if (_stageControl == null || IsMeasurementRunning || IsStageMoveInFlight)
            return;

        IsStageMoveInFlight = true;
        try
        {
            // ReadStagePosition and SetStagePosition are blocking
            // hardware calls (sync-over-async); keep them off the UI thread.
            var current = await Task.Run(() => _stageControl.ReadStagePosition());
            if (current == null)
                return;

            double dx = stageX - current.Coordinates.x;
            double dy = stageY - current.Coordinates.y;
            double distanceUm = Math.Sqrt(dx * dx + dy * dy);

            if (distanceUm > FarMoveThresholdUm)
            {
                var confirm = MoveConfirmation ?? ConfirmFarMoveAsync;
                if (!await confirm(distanceUm))
                    return;
            }

            var target = new XYStagePosition(
                current.Coordinates.hardwareautofocusoffset,
                stageX,
                stageY,
                current.Coordinates.z,
                current.Coordinates.usinghardwareautofocus,
                current.Name);

            await Task.Run(() => _stageControl.SetStagePosition(target));
        }
        catch (Exception ex)
        {
            await ExceptionWindowHandler.ShowExceptionAsync(ex);
        }
        finally
        {
            IsStageMoveInFlight = false;
        }
    }

    private static Task<bool> ConfirmFarMoveAsync(double distanceUm) =>
        MoveStageConfirmation.ShowAsync(distanceUm);

    public FieldViewerViewModel()
    {

    }


    public FieldViewerViewModel(IStageControl stageControl)
    {
        _stageControl = stageControl;
        
    }

}

