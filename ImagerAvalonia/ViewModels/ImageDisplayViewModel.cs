using Autofac;
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Storage;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using ImagerAvalonia.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;



namespace ImagerAvalonia.ViewModels;



public partial class ImageDisplayViewModel : ViewModelBase
{


    // Events
    public event Action<TiffPlaneMetadata, byte[], bool>? UpdateImage;
    public event Action<int, int, string, string, byte[], XYStagePosition, bool, double>? UpdateRegionProperties;
    public event EventHandler? GridValuesInitialized;
    public event EventHandler<OnDetectionRequestedEventArgs>? OnDetectionRequested;
    public event EventHandler<XYStagePosition>? SelectedStagePositionChanged;
    public event EventHandler<ObservableCollection<XYStagePosition>> OnXYitionsChanged;

    /// <summary>
    /// Notifies the attached ImageHandler (and anyone else) whenever ShowLiveView
    /// changes, so the handler's display-forwarding flag always mirrors the
    /// checkbox instead of being toggled independently.
    /// </summary>
    public event EventHandler<bool>? LiveViewChanged;

    /// <summary>Raised on Dispose; the display view ends its update loop when this fires.</summary>
    public event EventHandler? Terminated;

    [ObservableProperty] private bool _multiViewEnabled = false;
    [ObservableProperty] private int _Min_disp_val = 10000;
    [ObservableProperty] private int _Max_disp_val = 50000;

    /// <summary>Total number of frames the measurement plans to acquire. A count,
    /// not an index. A completed run ends with AcquiredFrameCount == MaxFrameCount.</summary>
    [ObservableProperty] private int _MaxFrameCount = 0;

    /// <summary>Zero-based index of the last frame the slider can select (the planned
    /// total minus one, since MaxFrameCount is a count). The slider's maximum.</summary>
    [ObservableProperty] private int _LastFrameIndex = 0;

    /// <summary>Number of frames acquired so far. Frames up to this number exist in
    /// storage; beyond it they have not been measured yet (see IsFramePending).</summary>
    [ObservableProperty] private int _AcquiredFrameCount = 0;

    /// <summary>True when the selected frame lies beyond what has been acquired; the
    /// view shows a "not measured yet" indicator instead of silently displaying the
    /// newest frame.</summary>
    [ObservableProperty] private bool _IsFramePending = false;

    /// <summary>True for the live view: it only ever shows live acquisitions, which are
    /// not stored, so there is no history to browse at any time — running or not.
    /// "Show last" is locked on and the checkbox and slider are always disabled. A
    /// permanent trait of that tab's view model; measurement and file tabs keep it
    /// false and retain independent slider and checkbox settings.</summary>
    [ObservableProperty] private bool _IsLiveView = false;

    [ObservableProperty] private bool _IsAutoContrastEnabled = true;
    [ObservableProperty] private bool _ShowLiveView = true;
    [ObservableProperty] private double _AlphaWidth = 800;
    [ObservableProperty] private double _AlphaHeight = 800;
    [ObservableProperty] private double _AlphaTop = 0;
    [ObservableProperty] private double _AlphaLeft = 0;
    [ObservableProperty] private bool _IsExperimentRunning = false;

    /// <summary>User-facing progress text, derived from SelectedFrame and
    /// MaxFrameCount (see UpdateProgressText): one-based, e.g. "3/10".</summary>
    [ObservableProperty] private string _AcquisitionProgress = "";

    /// <summary>Zero-based index of the frame selected on the slider. Frame indices are
    /// zero-based everywhere internally (this property, storage, handler requests);
    /// one-based numbers exist only in user-facing text, e.g. AcquisitionProgress.</summary>
    [ObservableProperty] private int _SelectedFrame = 0;
    /// <summary>Stage position this view is filtered to: only images acquired at this
    /// position are displayed. The special "&lt;None&gt;" position shows all.</summary>
    [ObservableProperty] private XYStagePosition _SelectedStagePosition;

    public bool IsDataStreaming = false;
    public int StorageID { get; set; } = -1;

    private List<Tuple<string, string>> AcqDetPairs = new();


    //private IExperimentSerialization _experimentTraversal;

    public ObservableCollection<string> Detectors = new();
    public ObservableCollection<string> Acquisitions = new();
    [ObservableProperty] ObservableCollection<XYStagePosition> _availableStagePositions  = new();

    // The ImageHandler this view model is currently wired up to. Tracked here (rather
    // than asking the workspace) so DetachHandler always unsubscribes the exact instance
    // that was subscribed, even if the workspace has already moved on to another session.
    private ImageHandler? _attachedHandler;



    public ImageDisplayViewModel()
    {
    }




    public void SetAvailableXYPositions(List<XYStagePosition> positions)
    {
        AvailableStagePositions = new ObservableCollection<XYStagePosition>(
            positions);

        AvailableStagePositions.Add(IStageControl.DefaultStagePosition);
        SelectedStagePosition = AvailableStagePositions[AvailableStagePositions.Count - 1];
        
        OnXYitionsChanged?.Invoke(this, AvailableStagePositions);       
    }


    public void LoadFirstImage()
    {
        SelectedFrame = 0;
        RequestFrameImage();
    }

    /// <summary>
    /// Routes the given handler's display events into this view model. When
    /// enableFrameRequests is set, slider frame requests are also routed back into the
    /// handler, so saved frames can be browsed (measurements and opened files; live
    /// acquisition does not support that). Any previously attached handler is detached
    /// first, so subscriptions never accumulate.
    /// </summary>
    public void AttachHandler(ImageHandler handler, bool enableFrameRequests)
    {
        DetachHandler();
        _attachedHandler = handler;

        handler.UpdateImageDisplay += ProcessImages;
        handler.UpdateImageElements += ProcessImageElements;
        handler.UpdateCurrentPositions += ProcessPositions;
        handler.UpdateAsyncProgress += ProcessProgress;
        LiveViewChanged += handler.OnLiveViewChanged;

        // The handler's forwarding flag mirrors the checkbox state from the start, so
        // the two can never disagree (programmatic changes raise LiveViewChanged too).
        handler.OnLiveViewChanged(this, ShowLiveView);

        if (enableFrameRequests)
        {
            OnDetectionRequested += handler.LoadImage;
        }
    }

    /// <summary>
    /// Detaches the handler previously attached with <see cref="AttachHandler"/>. Safe to
    /// call when nothing is attached.
    /// </summary>
    public void DetachHandler()
    {
        if (_attachedHandler == null) return;
        var handler = _attachedHandler;
        _attachedHandler = null;

        handler.UpdateImageDisplay -= ProcessImages;
        handler.UpdateImageElements -= ProcessImageElements;
        handler.UpdateCurrentPositions -= ProcessPositions;
        handler.UpdateAsyncProgress -= ProcessProgress;
        LiveViewChanged -= handler.OnLiveViewChanged;

        // No-op if it was never added (display-only attachments).
        OnDetectionRequested -= handler.LoadImage;
    }

    partial void OnShowLiveViewChanged(bool value)
    {
        if (IsLiveView && !value)
        {
            // The live view has no stored history to browse, running or not: "show
            // last" cannot be turned off. The revert raises LiveViewChanged for the
            // restored value only, so the attached handler never sees a transient
            // "off".
            ShowLiveView = true;
            return;
        }

        LiveViewChanged?.Invoke(this, value);

        if (!value)
        {
            return;
        }

        // Re-enabling "show last" snaps to the newest acquired frame and loads it, so
        // the display and slider agree immediately instead of on the next frame.
        IsFramePending = false;
        SelectedFrame = Math.Max(0, Math.Min(AcquiredFrameCount, MaxFrameCount) - 1);
        if (AcquiredFrameCount > 0)
        {
            RequestFrameImage();
        }
    }

    partial void OnMaxFrameCountChanged(int value)
    {
        LastFrameIndex = Math.Max(0, value - 1);
        UpdateProgressText();
    }

    partial void OnSelectedFrameChanged(int value)
    {
        UpdateProgressText();

        // Serving the selected frame happens only while browsing: while "show last"
        // is on, the stream itself updates the display (see UpdateDisplayedImageNumber).
        if (ShowLiveView)
        {
            return;
        }

        if (SelectedFrame >= AcquiredFrameCount)
        {
            // Beyond what has been measured: keep the current image and show the
            // pending indicator. Requesting would silently clamp to the newest frame.
            IsFramePending = true;
            return;
        }

        IsFramePending = false;
        RequestFrameImage();
    }

    /// <summary>
    /// The progress text is derived state: whenever the selected frame or the planned
    /// total changes, it is recomputed here, so no code path can leave it stale. This
    /// is the only place where one-based numbering is produced.
    /// </summary>
    private void UpdateProgressText()
    {
        AcquisitionProgress = $"{SelectedFrame + 1}/{MaxFrameCount}";
    }

    partial void OnIsLiveViewChanged(bool value)
    {
        if (value)
        {
            // The live view always follows the stream; there is no stored history
            // to browse, running or not.
            ShowLiveView = true;
        }
    }

    /// <summary>
    /// Called for every acquired frame (frame is the zero-based detection index),
    /// whether or not live view is on: it tracks how many frames exist. While "show
    /// last" is on, the slider follows the newest frame; while browsing, a frame the
    /// user has browsed ahead to is loaded as soon as it arrives.
    /// </summary>
    public void UpdateDisplayedImageNumber(string acqname, string detname, int frame)
    {
        if (frame + 1 > AcquiredFrameCount)
        {
            AcquiredFrameCount = frame + 1;
        }

        // Safety net for a planned total that was too low: the slider must never be
        // able to select beyond the last frame that exists.
        if (AcquiredFrameCount > MaxFrameCount)
        {
            MaxFrameCount = AcquiredFrameCount;
        }

        if (ShowLiveView)
        {
            if (AcquiredFrameCount > 0)
            {
                SelectedFrame = AcquiredFrameCount - 1;
            }
        }
        else if (IsFramePending && SelectedFrame < AcquiredFrameCount)
        {
            // The frame the user browsed ahead to has arrived: show it now.
            IsFramePending = false;
            RequestFrameImage();
        }
    }

    /// <summary>Requests the currently selected frame from the attached handler.
    /// Zero-based throughout: the selected index is passed to storage unchanged.</summary>
    private void RequestFrameImage()
    {
        OnDetectionRequested?.Invoke(this,
            new OnDetectionRequestedEventArgs(AcqDetPairs, SelectedFrame));
    }


    protected virtual void OnGridValueInitialized(EventArgs e)
    {
        GridValuesInitialized?.Invoke(this, e);
    }


    public void SetGridData(List<Tuple<string,string>> acq_det_pairs)
    {
        SelectedFrame = 0;
        Acquisitions.Clear();
        Detectors.Clear();
        if (acq_det_pairs == null) return;
        AcqDetPairs = new List<Tuple<string, string>>(acq_det_pairs);


        foreach (Tuple<string, string> pair in acq_det_pairs)
        {
            if (!Acquisitions.Contains(pair.Item1))
            {
                Acquisitions.Add(pair.Item1);
            }
            if (!Detectors.Contains(pair.Item2))
            {
                Detectors.Add(pair.Item2);
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
            var imageStagePosition = images.Metadata[i].CurrentStagePosition;

            if(imageStagePosition.Name!=string.Empty)
            {
                images.TraversedPositions.Add(imageStagePosition);
            }
            else
            {
                images.TraversedPositions.Add(IStageControl.DefaultStagePosition);
            }
        }
    }


    public void ProcessImages(object? sender, ImageData images)
    {

        for (int i = 0; i < images.Images.Count; i++)
        {
            if (i >= Math.Max(0, images.Images.Count - Acquisitions.Count * Detectors.Count))
            {
                if (SelectedStagePosition != IStageControl.DefaultStagePosition)
                {
                    if (images.Metadata[i].PositionName == SelectedStagePosition.Name)
                    {
                        UpdateImage?.Invoke(images.Metadata[i], images.Images[i], true);
                    }
                }
                else
                {
                    UpdateImage?.Invoke(images.Metadata[i], images.Images[i], true);
                }
            }
        }
    }

    partial void OnSelectedStagePositionChanged(XYStagePosition? oldValue, XYStagePosition? newValue)
    {
        if (newValue != null)
        {
            SelectedStagePositionChanged?.Invoke(this, newValue);
        }
    }

    internal void ProcessImageElements(object? sender, ImageData images)
    {
        if (IsDataStreaming)
        {
            for (int i = 0; i < images.Images.Count; i++)
            {
                Int32 im_size_x = Convert.ToInt32(images.Sizes[i][0]);
                Int32 im_size_y = Convert.ToInt32(images.Sizes[i][1]);
                UpdateRegionProperties?.Invoke(im_size_x, im_size_y, 
                    images.Metadata[i].AcquisitionName, 
                    images.Metadata[i].DetectorName, 
                    images.Images[i], 
                    images.TraversedPositions[i], 
                    false, 
                    images.Metadata[i].TimePoint);
            }
        }

    }

    internal void SetOpenID(int openID)
    {
        StorageID = openID;
    }

    public override void Dispose()
    {
        DetachHandler();
        Terminated?.Invoke(this, EventArgs.Empty);
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



