
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Utils;
using ImagerAvalonia.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;


namespace ImagerAvalonia.ViewModels;

public partial class FieldViewerViewModel : ViewModelBase
{

    IStageControl? _stageControl;

    [ObservableProperty] XYStagePosition _currentStagePosition;
    [ObservableProperty] bool _isStageViewerEngaged = false;
    [ObservableProperty] double _pixelSize = 100;

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

    public FieldViewerViewModel()
    { 
        
    }


    public FieldViewerViewModel(IStageControl stageControl)
    {
        _stageControl = stageControl;
        
    }

}

