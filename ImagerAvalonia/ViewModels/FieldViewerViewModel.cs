
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Utils;
using ImagerAvalonia.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;


namespace ImagerAvalonia.ViewModels;

public partial class FieldViewerViewModel : ViewModelBase, IImageDisplay
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
        CurrentStagePosition = _stageControl != null ? _stageControl.ReadStagePosition() : new XYStagePosition(0, 0, 0, false, 0,"DefaultPosition");
    }

    public void EngageFieldViewer()
    { 
        this.FindCurrentStagePosition();
        IsStageViewerEngaged = !IsStageViewerEngaged;

    }

    public void SetGridData(List<AcqDetPair> acq_det_pairs)
    {
        Acquisitions.Clear();
        Detectors.Clear();
        foreach (AcqDetPair acquisition in acq_det_pairs)
        {
            foreach (AcqDetPair detector in acq_det_pairs)
            {
                if (!Acquisitions.Contains(acquisition.acqName))
                {
                    Acquisitions.Add(acquisition.acqName);

                }
                if (!Detectors.Contains(detector.detName))
                {

                    Detectors.Add(detector.detName);
                }
            }
        }

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

