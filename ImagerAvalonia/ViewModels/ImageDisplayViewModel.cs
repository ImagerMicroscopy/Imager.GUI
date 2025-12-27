using Autofac;
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Utils;
using ImagerAvalonia.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;



namespace ImagerAvalonia.ViewModels;

public interface IImageDisplay
{
    void SetGridData(List<AcqDetPair> acq_det_pairs);

    void ProcessImages(object sender, ImageData images);



}





public partial class ImageDisplayViewModel : ViewModelBase, IImageDisplay
{


    // Events
    public event Action<TiffPlaneMetadata, byte[], bool>? UpdateImage;
    public event Action<int, int, string, string, byte[], XYStagePosition, bool, double>? UpdateRegionProperties;
    public event EventHandler? GridValuesInitialized;
    public event EventHandler<int>? SliderPropertyChanged;
    public event EventHandler<OnDetectionRequestedEventArgs>? OnDetectionRequested;
    public event EventHandler? LiveViewDisabled;
    public event EventHandler<XYStagePosition>? PinnedPositionChanged;
    public event EventHandler<ObservableCollection<XYStagePosition>> OnXYPositionsChanged;

    // Observable Properties
    [ObservableProperty] private bool _multiViewEnabled = false;
    [ObservableProperty] private int _Min_disp_val = 10000;
    [ObservableProperty] private int _Max_disp_val = 50000;
    [ObservableProperty] private int _MaxFrameCount = 0;
    [ObservableProperty] private int _MaxSliderValue = 1;
    [ObservableProperty] private bool _IsAutoContrastEnabled = true;
    [ObservableProperty] private bool _ShowLiveView = true;
    [ObservableProperty] private int _Slider_value;
    [ObservableProperty] private double _AlphaWidth = 800;
    [ObservableProperty] private double _AlphaHeight = 800;
    [ObservableProperty] private double _AlphaTop = 0;
    [ObservableProperty] private double _AlphaLeft = 0;
    [ObservableProperty] private bool _IsExperimentRunning = false;
    [ObservableProperty] private string _AcquisitionProgress = "";
    [ObservableProperty] private int _RequestedFrame = 0;
    [ObservableProperty] private XYStagePosition _PinnedPosition; 

    // Regular Fields
    public bool IsDataStreaming = false;
    public bool process_field_view;
    private bool _canFire = true;
    private System.Timers.Timer _throttleTimer = new System.Timers.Timer(10) { AutoReset = true } ;
    private IStageControl? _stageController;
    private IExperimentSerialization _experimentTraversal;

    // Collections
    public ObservableCollection<string> Detectors = new();
    public ObservableCollection<string> Acquisitions = new();
    [ObservableProperty] ObservableCollection<XYStagePosition> _availableStagePositions  = new();

    // Properties
    public CancellationTokenSource source { get; set; } = new();

    public ImageDisplayViewModel()
    { 
        
    }

    public ImageDisplayViewModel(IExperimentSerialization experimentTraversal, IStageControl stageControl)
    {
        //_stageController = stageController;
        _experimentTraversal = experimentTraversal;
        //_throttleTimer = new System.Timers.Timer(50) { AutoReset = true };
        _throttleTimer.Elapsed += (_, _) => _canFire = true;

        SliderPropertyChanged += (s, e) =>
        {
            if (_canFire)
            {
                _canFire = false;
                _throttleTimer.Start();
                OnDetectionRequested?.Invoke(this, new OnDetectionRequestedEventArgs(_experimentTraversal.GetAcqDetPairs(),e));


            }
        };
        this.PropertyChanged += OnSliderPropertyChanged;
        _stageController = stageControl;

        AvailableStagePositions = new ObservableCollection<XYStagePosition>(experimentTraversal.ExperimentPositions);   
        PinnedPosition = AvailableStagePositions[0];
        
    }




    public void SetAvailableXYPositions(IEnumerable<XYStagePosition> stagePositions)
    {
        AvailableStagePositions = new ObservableCollection<XYStagePosition>(stagePositions);
        PinnedPosition = AvailableStagePositions[0];
        OnXYPositionsChanged?.Invoke(this, AvailableStagePositions);
    }


    public void LoadFirstImage()
    {
        AcquisitionProgress = $"1/{MaxFrameCount}";
        RequestedFrame = 1;
    }

    public void DisableLiveView()
    {
        LiveViewDisabled?.Invoke(this, new EventArgs());

    }

    partial void OnShowLiveViewChanged(bool value)
    {
        if(value)
        {
            RequestedFrame = 1;
        }
    }

    public void UpdateDisplayedImageNumber(string acqname, string detname, int frame)
    {
        if (ShowLiveView)
        {
            if (frame + 1 > MaxFrameCount)
            {
                MaxFrameCount = frame + 1;
            }

            if (_experimentTraversal != null)
            {
               

                AcquisitionProgress = $"{frame + 1}/{MaxFrameCount}";
                
                MaxSliderValue = MaxFrameCount;
                RequestedFrame = frame;
            }
        }
    }

    protected virtual void OnSliderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RequestedFrame) && !ShowLiveView)
        {
            AcquisitionProgress = $"{RequestedFrame}/{MaxFrameCount}";
            SliderPropertyChanged?.Invoke(this, RequestedFrame);
        }
    }



    protected virtual void OnGridValueInitialized(EventArgs e)
    {
        GridValuesInitialized?.Invoke(this, e);
    }





    public void SetGridData(List<AcqDetPair> acq_det_pairs)
    {
        RequestedFrame = 1;
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
        OnGridValueInitialized(new EventArgs());
    }


    public void ProcessProgress(object? sender, ImageData images)
    {

        for (int i = 0; i < images.Images.Count; i++)
        {

            UpdateDisplayedImageNumber(
                images.Metadata[i].AcquisitionName,
                images.Metadata[i].DetectorName,
                images.Metadata[i].DetectionIndex);
        }
    }


    internal void ProcessPositions(object? sender, ImageData images)
    {
        images.TraversedPositions = new List<XYStagePosition>(images.Images.Count) { };

        for (int i = 0; i < images.Images.Count; i++)
        {
            var imageXYStagePosition = images.Metadata[i].CurrentStagePosition;

            if(imageXYStagePosition.Name!=string.Empty)
            {
                images.TraversedPositions.Add(imageXYStagePosition);
            }
            else
            {
                images.TraversedPositions.Add(IStageControl.DefaultXYStagePosition);
            }
        }
    }


    public void ProcessImages(object? sender, ImageData images)
    {

        for (int i = 0; i < images.Images.Count; i++)
        {

            Int32 im_size_x = Convert.ToInt32(images.Sizes[i][0]);
            Int32 im_size_y = Convert.ToInt32(images.Sizes[i][1]);



            if (i >= Math.Max(0, images.Images.Count - Acquisitions.Count * Detectors.Count))
            {
                if (PinnedPosition != IStageControl.DefaultXYStagePosition)
                {
                    if (images.TraversedPositions[i].IsEqual(PinnedPosition))
                    {
                        UpdateImage?.Invoke(images.Metadata[i], images.Images[i], true);
                        //UpdateImage?.Invoke(im_size_x, im_size_y, images.Metadata[i].AcquisitionName, images.Metadata[i].DetectorName, images.Images[i], images.TraversedPositions[i], true, images.Metadata[i].TimePoint);
                    }
                    else
                    {
                        UpdateImage?.Invoke(images.Metadata[i], images.Images[i], false);

                        //UpdateImage?.Invoke(im_size_x, im_size_y, images.Metadata[i].AcquisitionName, images.Metadata[i].DetectorName, images.Images[i], images.TraversedPositions[i], false, images.Metadata[i].TimePoint);
                    }
                }
                else
                {
                    UpdateImage?.Invoke(images.Metadata[i], images.Images[i], true);

                    //UpdateImage?.Invoke(im_size_x, im_size_y, images.Metadata[i].AcquisitionName, images.Metadata[i].DetectorName, images.Images[i], images.TraversedPositions[i], true, images.Metadata[i].TimePoint);
                }
            }
        }
    }

    partial void OnPinnedPositionChanged(XYStagePosition? oldValue, XYStagePosition? newValue)
    {
        if (newValue != null)
        {
            PinnedPositionChanged?.Invoke(this, newValue);
        }
    }

    internal void SetExperimentSerializer(IExperimentSerialization experimentSerializer)
    {
        _experimentTraversal = experimentSerializer;
    }

    internal void ProcessImageElements(object? sender, ImageData images)
    {
        if (IsDataStreaming)
        {
            for (int i = 0; i < images.Images.Count; i++)
            {
                Int32 im_size_x = Convert.ToInt32(images.Sizes[i][0]);
                Int32 im_size_y = Convert.ToInt32(images.Sizes[i][1]);
                UpdateRegionProperties?.Invoke(im_size_x, im_size_y, images.Metadata[i].AcquisitionName, images.Metadata[i].DetectorName, images.Images[i], images.TraversedPositions[i], false, images.Metadata[i].TimePoint);
            }
        }

    }

    public class RegionPropertiesEventArgs : EventArgs
    {
        public ImageData ImageData { get; }
        public XYStagePosition XYStagePosition { get; }

        public RegionPropertiesEventArgs(ImageData imageData, XYStagePosition xyStagePosition)
        {
            ImageData = imageData;
            XYStagePosition = xyStagePosition;
        }
    }
}

public interface IImageDisplayViewModelFactory
{
    ImageDisplayViewModel Create(ILifetimeScope scope);
}

public class ImageDisplayViewModelFactory : IImageDisplayViewModelFactory
{

    public ImageDisplayViewModelFactory()
    {
    }

    public ImageDisplayViewModel Create(ILifetimeScope scope)
    {
        return scope.Resolve<ImageDisplayViewModel>();
    }
}



