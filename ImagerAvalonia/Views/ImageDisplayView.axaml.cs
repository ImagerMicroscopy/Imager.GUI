using Autofac;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Storage;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.Views.ImageViews;
using ImagerAvalonia.Views.MultiChannelView;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ImagerAvalonia.Views;

public partial class ImageDisplayView : UserControl
{
    private bool _isMultiChannelEnabled = false;
    private bool _isDotSelectionEnabled = true;
    private ImageDisplayViewModel? imageDisplayViewM;

    private MultiChannelContrastView multiChannelContrast;
    private ImageRegionCollectionPropertiesView regionCollectionProperties;
    private MultiChannelConfigView multiChannelConfigView;
    private ImageGrid imageGrid;
    private MultiImageGrid multiImageGrid;
    private Grid imagePanel;
    private Expander multiChannelContrastExpander;
    private ContentControl multiChannelContrastHost;

    private ILifetimeScope scope = App.Container.BeginLifetimeScope();

    // Bounded with DropOldest so a stalled UI thread cannot accumulate an unbounded
    // backlog of stale frames; UpdateViewer additionally renders only the newest
    // update per display slot (see ImageUpdateCoalescer).
    private Channel<ImageUpdateArguments> ImageChannel = Channel.CreateBounded<ImageUpdateArguments>(
        new BoundedChannelOptions(32)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });


    public ImageDisplayView()
    {
        InitializeComponent();

        imagePanel = this.FindControl<Grid>("ImagePanel")!;
        multiChannelContrastExpander = this.FindControl<Expander>("MultiChannelContrastExpander")!;
        multiChannelContrastHost = this.FindControl<ContentControl>("MultiChannelContrastHost")!;

        var regionPanel = this.FindControl<ContentControl>("RegionContent")!;
        var multichannelPanel = this.FindControl<ContentControl>("MultiChannelConfig")!;

        regionCollectionProperties = new ImageRegionCollectionPropertiesView(scope);
        regionPanel.Content = regionCollectionProperties;

        multiChannelConfigView = new MultiChannelConfigView(scope);
        multichannelPanel.Content = multiChannelConfigView;

        // One region view model per tab (InstancePerLifetimeScope): the tiles'
        // overlays and the element-properties panel share it.
        imageGrid = new ImageGrid();
        imageGrid.SharedRegionViewModel = scope.Resolve<ImageRegionDisplayViewModel>();
        imageGrid.OnSelectedItemChanged += ImageGrid_OnSelectedItemChanged;
        imageGrid.KeyDown += ImageDisplayView_KeyDown;
        imagePanel.Children.Add(imageGrid);

        multiChannelContrast = new MultiChannelContrastView();
        multiImageGrid = new MultiImageGrid(multiChannelConfigView.DataContext as MultiChannelViewModel, multiChannelContrast.DataContext as MultiChannelContrastViewModel);

        DataContextChanged += OnDataContextChanged;

        _ = Task.Run(() => UpdateViewer());
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // A view model can be attached only once here, but unsubscribe the previous
        // one anyway so re-attachment never accumulates subscriptions.
        if (imageDisplayViewM != null)
        {
            imageDisplayViewM.OnXYitionsChanged -= ImageDisplayViewM_OnXYitionsChanged;
            imageDisplayViewM.UpdateImage -= UpdateCurrentImageFromStream;
            imageDisplayViewM.UpdateRegionProperties -= UpdateRegionPropertiesFromStream;
            imageDisplayViewM.GridValuesInitialized -= InitializeGrid;
            imageDisplayViewM.Terminated -= OnViewModelTerminated;
        }

        if (DataContext is ImageDisplayViewModel vm)
        {
            imageDisplayViewM = vm;
            vm.OnXYitionsChanged += ImageDisplayViewM_OnXYitionsChanged;
            vm.UpdateImage += UpdateCurrentImageFromStream;
            vm.UpdateRegionProperties += UpdateRegionPropertiesFromStream;
            vm.GridValuesInitialized += InitializeGrid;
            vm.Terminated += OnViewModelTerminated;
        }
    }

    /// <summary>
    /// The view model this view presents has been disposed (its tab was closed). End
    /// the display loop so the pending await no longer keeps this view alive.
    /// </summary>
    private void OnViewModelTerminated(object? sender, EventArgs e)
    {
        ImageChannel.Writer.TryComplete();
    }

    private async Task UpdateViewer()
    {
        while (await ImageChannel.Reader.WaitToReadAsync())
        {
            var drained = new List<ImageUpdateArguments>();
            while (ImageChannel.Reader.TryRead(out ImageUpdateArguments im_args))
            {
                drained.Add(im_args);
            }

            // Live display shows only the newest frame per display slot: everything
            // that queued up while the previous frame was still rendering is stale
            // and discarded here. Saving is unaffected — it happens upstream in
            // ImageHandler, before updates reach this channel.
            foreach (var im_args in ImageUpdateCoalescer.KeepLatestPerSlot(drained))
            {
                if (!_isMultiChannelEnabled)
                {
                    await UpdateImageGrid(
                        im_args.ImageSizeX,
                        im_args.ImageSizeY,
                        im_args.Acquisition,
                        im_args.Detector,
                        im_args.ImageData,
                        im_args.PixelFormat,
                        im_args.Position,
                        im_args.CanUpdate,
                        im_args.TimePoint
                    );
                }
                else
                {
                    try
                    {
                        await multiImageGrid.ReceiveChannelImage(im_args.Detector, im_args.Acquisition, im_args.ElementID,
                            im_args.ImageSizeX, im_args.ImageSizeY, im_args.ImageData, im_args.PixelFormat);
                    }
                    catch
                    {

                    }

                }
            }
        }

        // The channel is completed when the view model is disposed (its tab closed);
        // nothing can use this view anymore, so its lifetime scope can be released.
        scope.Dispose();
    }

    private void ImageDisplayViewM_OnXYitionsChanged(object? sender, ObservableCollection<XYStagePosition> e)
    {
        regionCollectionProperties.SetXYitions(e);
    }

    private void UpdateRegionPropertiesFromStream(int im_size_x, int im_size_y, string acq, string det, byte[] image_data, XYStagePosition pos, bool canupdate, double timepoint)
    {
        byte[] copy_image = new byte[image_data.Length];
        Array.Copy(image_data, copy_image, image_data.Length);
        regionCollectionProperties.UpdateRegions(copy_image, acq, det, im_size_x, im_size_y, pos, timepoint);
    }

    /// <summary>
    /// A tile was clicked: the slot view model becomes the display's selection (the
    /// single source of truth; its IsSelected flag drives the tile highlight).
    /// Contrast panels live on the tiles themselves, so nothing else follows the
    /// selection here anymore.
    /// </summary>
    private void ImageGrid_OnSelectedItemChanged(object? sender, ImageCanvasViewModel slot)
    {
        if (imageDisplayViewM != null)
        {
            imageDisplayViewM.SelectedSlot = slot;
        }
    }

    private void OnDotToggled(object? sender, RoutedEventArgs r)
    {
        if (_isDotSelectionEnabled && imageGrid.SelectedImage != null)
        {
            _isDotSelectionEnabled = false;
            if (sender is Button btn)
            {
                switch (btn.Name)
                {
                    case "selectDotButton":
                        imageGrid.SelectedImage.EnableRegionSelection(new CircleElementControl());
                        break;
                    case "selectLineButton":
                        imageGrid.SelectedImage.EnableRegionSelection(new LineElementControl());
                        break;
                }
            }

        }
    }

    private void ImageDisplayView_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            // Restore the grid when a tile is maximized; a no-op otherwise.
            imageGrid.RestoreMaximizedTile();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && !_isDotSelectionEnabled)
        {
            _isDotSelectionEnabled = true;
            imageGrid.SelectedImage?.DisableRegionSelection();
        }
    }


    public void UpdateCurrentImageFromStream(TiffPlaneMetadata mt, byte[] image_data, bool canupdate)
    {
        ImageChannel.Writer.TryWrite(new ImageUpdateArguments(
            (int)mt.Width,
            (int)mt.Height,
            mt.AcquisitionName,
            mt.DetectorName,
            mt.ElementID,
            image_data,
            mt.Type,
            mt.CurrentStagePosition,
            canupdate,
            mt.TimePoint
            ));
    }

    private async Task UpdateImageGrid(int im_size_x, int im_size_y, string acq, string det, byte[] image_data, ImagePixelFormat format, XYStagePosition pos, bool canupdate, double timepoint)
    {
        if (canupdate)
        {
            // The histogram is expensive and only displayed on a tile whose
            // contrast overlay is open, so it is fed only to those.
            var slot = imageDisplayViewM?.Slots.FirstOrDefault(s => s.AcqName == acq && s.DetName == det);
            if (slot is { IsContrastPanelVisible: true })
            {
                slot.ContrastSettings.UpdateHistogram(image_data, format);
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                imageGrid[acq, det].UpdateBitmap(im_size_x, im_size_y, image_data, format);
                imageGrid[acq, det].SetBitmap();
                imageGrid[acq, det].InvalidateBitmap();

            });
        }
    }

    private void InitializeGrid(object? sender, EventArgs e)
    {
        if (imageDisplayViewM == null) return;

        _isDotSelectionEnabled = true;
        _isMultiChannelEnabled = false;
        imageDisplayViewM.MultiViewEnabled = false;

        // Tiles are built from the slot view models; the grid shares the available
        // space equally between them (no fixed sizes, no offsets).
        imageGrid.Initialize(imageDisplayViewM.Slots, imageGrid.SharedRegionViewModel);
        multiImageGrid.Initialize(imageDisplayViewM.Acquisitions.ToList(), imageDisplayViewM.Detectors.ToList());

        imagePanel.Children.Clear();
        imagePanel.Children.Add(imageGrid);
        multiChannelContrastExpander.IsVisible = false;
        multiChannelContrastHost.Content = null;
        regionCollectionProperties.DataContext = imageGrid.SharedRegionViewModel;
    }

    private void OnResetView(object? sender, RoutedEventArgs e)
    {
        // Tiles, not the grid, own their viewport: resetting re-fits every tile.
        imageGrid.ResetView();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnChangeToMultiChannel(object? sender, RoutedEventArgs e)
    {
        if(sender is ToggleButton button)
        {
            if (button.IsChecked is not null && (bool)button.IsChecked)
            {
                _isMultiChannelEnabled = true;
                multiChannelContrastExpander.IsVisible = true;
                multiChannelContrastHost.Content = multiChannelContrast;
                imagePanel.Children.Remove(imageGrid);
                imagePanel.Children.Add(multiImageGrid);
            }
            else
            {
                _isMultiChannelEnabled = false;
                multiChannelContrastExpander.IsVisible = false;
                multiChannelContrastHost.Content = null;
                imagePanel.Children.Remove(multiImageGrid);
                imagePanel.Children.Add(imageGrid);
            }
        }
    }
}

public class ImageUpdateArguments
{
    public int ImageSizeX { get; }
    public int ImageSizeY { get; }
    public string Acquisition { get; }
    public string ElementID { get; }
    public string Detector { get; }
    public byte[] ImageData { get; }
    public ImagePixelFormat PixelFormat { get; }
    public XYStagePosition Position { get; }
    public bool CanUpdate { get; }
    public double TimePoint { get; }

    public ImageUpdateArguments(
        int im_size_x,
        int im_size_y,
        string acq,
        string det,
        string elementid,
        byte[] image_data,
        ImagePixelFormat pixelFormat,
        XYStagePosition pos,
        bool canupdate,
        double timepoint)
    {
        ImageSizeX = im_size_x;
        ImageSizeY = im_size_y;
        Acquisition = acq;
        Detector = det;
        ImageData = image_data ?? throw new ArgumentNullException(nameof(image_data));
        PixelFormat = pixelFormat;
        Position = pos ?? throw new ArgumentNullException(nameof(pos));
        CanUpdate = canupdate;
        TimePoint = timepoint;
        ElementID = elementid;
    }
}

/// <summary>
/// Reduces queued display updates to the newest one per display slot
/// (acquisition x detector x element). When the UI cannot keep up with the
/// acquisition, all frames except the latest are stale and dropped. Saving to
/// disk is not affected — it happens upstream of the display queue.
/// </summary>
internal static class ImageUpdateCoalescer
{
    public static List<ImageUpdateArguments> KeepLatestPerSlot(IEnumerable<ImageUpdateArguments> updates)
    {
        var latestPerSlot = new Dictionary<(string Acquisition, string Detector, string ElementId), ImageUpdateArguments>();
        foreach (var update in updates)
        {
            latestPerSlot[(update.Acquisition, update.Detector, update.ElementID)] = update;
        }
        return latestPerSlot.Values.ToList();
    }
}
