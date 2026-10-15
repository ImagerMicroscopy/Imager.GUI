using Autofac;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Storage;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.Views.ImageViews;
using ImagerAvalonia.Views.MultiChannelView;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;





namespace ImagerAvalonia.Views;





public partial class ImageDisplayView : UserControl
{

    private ScaleTransform? _scaleTransform;
    private TranslateTransform? _translateTransform;
    public static volatile bool _isImageProcessing = false; // Flag to prevent redundant calls

    private bool _isMultiChannelEnabled = false;
    private bool _isDotSelectionEnabled = true;
    //private RectangelBorder _cropBorder;
    private ImageDisplayViewModel? imageDisplayViewM;
    private readonly object _lockObj = new object();

    private Canvas imageCanvas;
    private MultiChannelContrastView multiChannelContrast;
    private ContrastPanelView contrastPanel;
    private ImageRegionCollectionPropertiesView regionCollectionProperties;
    private MultiChannelConfigView multiChannelConfigView;
    private ImageGrid imageGrid;
    private MultiImageGrid multiImageGrid;


    private ILifetimeScope scope = App.Container.BeginLifetimeScope();

    public Dictionary<int, WriteableBitmap> GridImages = new();
    public Dictionary<ValueTuple<int, int>, int> FlattenedGrid = new();
    public Dictionary<ValueTuple<int, int>, string> AcqNames = new();
    public Dictionary<ValueTuple<int, int>, string> DetNames = new();
    public Dictionary<ValueTuple<int, int>, ValueTuple<int, int>> ImSizes = new();

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


        imageCanvas = this.FindControl<Canvas>("ImageCanvas")!;
        imageSlider = this.FindControl<Slider>("imageSlider")!;
        imageSlider.IsEnabled = false;

        contrastPanel = (ContrastPanelView)this.FindControl<UserControl>("ContrastPanel")!;
        multiChannelContrast = new MultiChannelContrastView();
        var regionPanel = this.FindControl<ContentControl>("RegionContent")!;
        var multichannelPanel = this.FindControl<ContentControl>("MultiChannelConfig")!;




        regionCollectionProperties = new ImageRegionCollectionPropertiesView(scope);
        regionPanel.Content = regionCollectionProperties;

        multiChannelConfigView = new MultiChannelConfigView(scope);
        multichannelPanel.Content = multiChannelConfigView;

        var imageContainer = this.FindControl<Avalonia.Controls.Grid>("ImageContainer")!;
        imageContainer.Width = ScreenHelper.ScreenSize.Width / ScreenHelper.Scaling;
        imageContainer.Height = ScreenHelper.ScreenSize.Height / ScreenHelper.Scaling;


        var contentControl = this.FindControl<Panel>("ImagePanel")!;



        imageGrid = new ImageGrid();    // this.FindControl<ImagerAvalonia.Views.ImageGrid>("imageGrid")!;
        imageGrid.SharedRegionViewModel = scope.Resolve<ImageRegionDisplayViewModel>();
        imageGrid.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        imageGrid.KeyDown += ImageDisplayView_EnterSelection_KeyDown;
        contentControl.Children.Add(imageGrid);

        multiImageGrid = new MultiImageGrid(multiChannelConfigView.DataContext as MultiChannelViewModel, multiChannelContrast.DataContext as MultiChannelContrastViewModel);


        var transformGroup = imageGrid.RenderTransform! as TransformGroup;
        _scaleTransform = (ScaleTransform)transformGroup!.Children[0];
        _translateTransform = (TranslateTransform)transformGroup!.Children[1];

        DataContextChanged += OnDataContextChanged;


        _ = Task.Run(() => UpdateViewer());


    }

    public void UpdateXYitions(object? sender, ILifetimeScope scope)
    {

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
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ImageDisplayViewModel vm)
        {
            imageDisplayViewM = vm;
            imageDisplayViewM.OnXYitionsChanged += ImageDisplayViewM_OnXYitionsChanged;
            imageDisplayViewM.UpdateImage += UpdateCurrentImageFromStream;
            imageDisplayViewM.UpdateRegionProperties += UpdateRegionPropertiesFromStream;
            imageDisplayViewM.GridValuesInitialized += InitializeGrid;
            imageDisplayViewM.SliderPropertyChanged += OnSliderValChanged;
        }
        imageGrid.OnSelectedItemChanged += ImageGrid_OnSelectedItemChanged;
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

    private void ImageGrid_OnSelectedItemChanged(object? sender, EventArgs e)
    {
        if (contrastPanel.DataContext is ContrastAdjViewModel vm)
        {
            vm.OnHistogramUpdateRequested -= contrastPanel._viewModel_OnHistogramUpdateRequested;
        }
        contrastPanel.DataContext = imageGrid.SelectedCanvasViewModel.ContrastSettings;
        contrastPanel.ContrastPanelView_DataContextChanged(sender, e);
    }


    private void OnDotToggled(object? sender, RoutedEventArgs e)
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

    private void ImageDisplayView_EnterSelection_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !_isDotSelectionEnabled)
        {
            _isDotSelectionEnabled = true;
            imageGrid.SelectedImage.DisableRegionSelection();

        }
    }



    private void OnSliderValChanged(object? sender, int frame)
    {

        Dispatcher.UIThread.Invoke(() =>
        {
            imageSlider.Value = frame;
        });

    }





    public void UpdateCurrentImageFromStream(TiffPlaneMetadata mt, byte[] image_data, bool canupdate)
    {
        if ((imageGrid.isDragging)) return;

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

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (imageGrid.SelectedCanvasViewModel.ContrastSettings is ContrastAdjViewModel vm &&
                    imageGrid.SelectedCanvasViewModel.AcqName == acq && imageGrid.SelectedCanvasViewModel.DetName == det)
                {

                    vm.UpdateHistogram(image_data, format);
                }


                imageGrid[acq, det].UpdateBitmap(im_size_x, im_size_y, image_data, format);
                imageGrid[acq, det].SetBitmap();
                imageGrid[acq, det].InvalidateBitmap();

            });
        }
    }



    private void InitializeGrid(object? sender, EventArgs e)
    {

        _isDotSelectionEnabled = true;
        _isMultiChannelEnabled = false;
        imageDisplayViewM.MultiViewEnabled = false;

        imageGrid.Margin = new Avalonia.Thickness(-410 * imageDisplayViewM.Acquisitions.Count + 410, 0, 0, 0);
        imageGrid.Initialize(imageDisplayViewM.Acquisitions.ToList(), imageDisplayViewM.Detectors.ToList());
        multiImageGrid.Initialize(imageDisplayViewM.Acquisitions.ToList(), imageDisplayViewM.Detectors.ToList());

        var contentControl = this.FindControl<Panel>("ImagePanel")!;
        contentControl.Children.Clear();
        contentControl.Children.Add(imageGrid);
        //imageGrid.SharedRegionViewModel = (ImageRegionDisplayViewModel)regionCollectionProperties.DataContext;
        contrastPanel.DataContext = imageGrid.SelectedCanvasViewModel.ContrastSettings;
        regionCollectionProperties.DataContext = imageGrid.SharedRegionViewModel;
    }



    private void OnResetView(object? sender, RoutedEventArgs e)
    {
        if (_translateTransform != null && _scaleTransform != null)
        {
            _translateTransform.X = 0;
            _translateTransform.Y = 0;
            _scaleTransform.ScaleX = 0.5;
            _scaleTransform.ScaleY = 0.5;
        }
    }





    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }

    private void OnChangeToMultiChannel(object? sender, RoutedEventArgs e)
    {
        if(sender is ToggleButton button)
        {
            var contentControl = this.FindControl<Panel>("ImagePanel")!;
            var contrastControl = this.FindControl<StackPanel>("ElementsPanel")!;

            if (button.IsChecked is not null && (bool)button.IsChecked)
            {
                _isMultiChannelEnabled = true;
                contrastControl.Children.Clear();
                contrastControl.Children.Add(multiChannelContrast);
                contentControl.Children.Remove(imageGrid);
                contentControl.Children.Add(multiImageGrid);
            }
            else
            {
                _isMultiChannelEnabled = false;
                contrastControl.Children.Clear();
                contrastControl.Children.Add(contrastPanel);
                contentControl.Children.Remove(multiImageGrid);
                contentControl.Children.Add(imageGrid);
            }
        }
    }
}




public class GridContrastEventArgs : EventArgs
{
    public int row { get; }
    public int col { get; }
    public string? acquistion { get; }
    public string? detector { get; }

    public GridContrastEventArgs(int value1, int value2, string? acq, string? det)
    {
        row = value1;
        col = value2;

        acquistion = acq;
        detector = det;
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