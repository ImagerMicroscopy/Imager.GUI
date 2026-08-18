using Autofac;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.Storage;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.Views.MultiChannelView;
using ImagerAvalonia.Views.ViewUtils;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;





public class CanvasContext
{
    public Canvas Canvas { get; set; }
    public double CenterX { get; set; } = double.MaxValue;
    public double CenterY { get; set; } = double.MaxValue;
    public double CenterZ { get; set; } = double.MaxValue;
    public double ScreenPosX { get; set; }
    public double ScreenPosY { get; set; }


    public int ContrastMin { get; set; } = 0;
    public int ContrastMax { get; set; } = 0;
    public byte[] _lastImage;
}

namespace ImagerAvalonia.Views
{
    public partial class FieldViewerView : UserControl
    {
        private bool IsCenterPositionSet = false;
        private bool _isDragging = false;
        private Point _lastMousePosition;

        private double LastImageTransformX = 0;
        private double LastImageTransformY = 0;
        private Dictionary<Tuple<string, string>, CanvasContext> _canvasContextDict = new();

        //This is a bit stupid. ContrastPanelView was designed to work with a 2D list of Acquisition x Detectors. 
        //The field view was designed to work with a dictionary. This inconsistency requires a mapping between two.
        //Ideally, ContrastPanelView, together with ImageDisplayView may need a re-work so that everything accepts an
        //Acq Det key pair. For now, this stays as is.

        private Dictionary<Tuple<int, int>,Tuple<string, string>> _contrastIntAcqDetPairDict = new();
        private Dictionary<Tuple<string, string>, Tuple<int, int>> _contrastAcqDetPairIntDict = new();
        private Dictionary<Tuple<string, string>, Tuple<int, int>> _contrastSettings = new();

        private Canvas? _selectedCanvas;
        private Canvas? _relstageViewVisualizer = new();


        private const double ZDisplacementTHS = 0.01; // In microns
        private double _DisplacementThsMicron = 20;
        private double _PixelSizeNM = 130; // In nanometers
        private double _init_pos_x;
        private double _init_pos_y;
        private ScaleTransform? _scaleTransform;
        private TranslateTransform? _translateTransform;
        private ContrastPanelView contrastView;
        private Channel<ImageData> ImageChannel = Channel.CreateUnbounded<ImageData>();

        public bool IsFieldViewerEngaged = false;
        private readonly object _updateLock = new object();

        public FieldViewerView()
        {
            InitializeComponent();



            var screenBounds = ScreenHelper.ScreenSize;
            var screenScale = ScreenHelper.Scaling;

            _init_pos_x = (screenBounds.Width / 2 - 260 * screenScale) / screenScale;
            _init_pos_y = (screenBounds.Height / 2) / screenScale;

            LastImageTransformX = _init_pos_x;
            LastImageTransformY = _init_pos_y;

            var transformGroup = (TransformGroup)((Canvas)this.FindControl<Canvas>("CNVS")).RenderTransform;
            _scaleTransform = (ScaleTransform)transformGroup.Children[0];
            _translateTransform = (TranslateTransform)transformGroup.Children[1];
            contrastView = this.FindControl<ContrastPanelView>("ContrastPanelFieldView");
            contrastView.SetSliderVisibility(false);
            contrastView.SetHistogramVisibility(false);
            contrastView.SetToggleAutoContrastVisibility(false);
            contrastView.UpdateContrastOnCurrentImage += FieldView_UpdateContrastSettings;
            InitializeUpdateContrastFieldView += contrastView.PopulateContrastSettings;

            
            var _ = Task.Run(() => UpdateViewer());

            DataContextChanged += OnDataContextChanged;
        }
        private async Task UpdateViewer()
        {
            while (await ImageChannel.Reader.WaitToReadAsync())
            {
                while (ImageChannel.Reader.TryRead(out ImageData im_args))
                {
                    try
                    {
                        await UpdateFieldViewImage(im_args);
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine($"{e.Message}\n{e.StackTrace}");
                    }
                }
            }
        }



        private void FieldView_UpdateContrastSettings(object? sender, EventArgs e)
        {
            var vm = DataContext as FieldViewerViewModel;
            if (vm.SelectedAcquisition != null && vm.SelectedDetector != null)
            {
                var key = new Tuple<string, string>(vm.SelectedAcquisition, vm.SelectedDetector);

                int row = _contrastAcqDetPairIntDict[key].Item1;
                int col = _contrastAcqDetPairIntDict[key].Item2;

                _contrastSettings[key] = new Tuple<int, int>(contrastView.contrastSettings[row][col].Min_disp_val, contrastView.contrastSettings[row][col].Max_disp_val);
            }
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (DataContext is FieldViewerViewModel field_viewer)
            {
                field_viewer.PropertyChanged += Field_viewer_IsFieldViewerEngaged;
                field_viewer.PropertyChanged += Field_viewer_CenterXYitionChanged;
                field_viewer.PropertyChanged += Field_viewer_SelectedPropertyChanged;

                field_viewer.FocusViewInitialized += PopulateCanvas;
                field_viewer.UpdateImageData += AddImageToFieldViewerChannel; ;
            }
        }

        private void AddImageToFieldViewerChannel(object? sender, ImageData e)
        {
            ImageChannel.Writer.TryWrite(e);
        }

        public event EventHandler<GridContrastEventArgs> InitializeUpdateContrastFieldView;


        private void Field_viewer_SelectedPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "SelectedAcquisition" || e.PropertyName=="SelectedDetector") 
            {
                if(DataContext is FieldViewerViewModel vm)
                if (vm.SelectedDetector != null && vm.SelectedAcquisition != null)
                {
                    var key = Tuple.Create(vm.SelectedAcquisition, vm.SelectedDetector);
                    if (_canvasContextDict.ContainsKey(key))
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            _selectedCanvas?.SetValue(IsVisibleProperty, false);
                            _canvasContextDict[key].Canvas.IsVisible = true;
                            _selectedCanvas = _canvasContextDict[key].Canvas;
                            contrastView.SetCurrentContrastVals(this, new GridContrastEventArgs(_contrastAcqDetPairIntDict[key].Item1, _contrastAcqDetPairIntDict[key].Item2, null, null));
                        }, DispatcherPriority.Background);

                    }
                }
            }
        }

        private void Field_viewer_CenterXYitionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CurrentStagePosition" && DataContext is FieldViewerViewModel vm)
            {
                try
                {
                    foreach (var key in _canvasContextDict.Keys.ToList())
                    {
                        _canvasContextDict[key].CenterX = vm.CurrentStagePosition.Coordinates.x;
                        _canvasContextDict[key].CenterY = vm.CurrentStagePosition.Coordinates.y;
                        _canvasContextDict[key].CenterZ = vm.CurrentStagePosition.Coordinates.z;

                    }

                    IsCenterPositionSet = true;
                }
                catch
                {
                    IsCenterPositionSet = false;
                }
            }
        }

        private void Field_viewer_IsFieldViewerEngaged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsStageViewerEngaged" && IsCenterPositionSet)
            {
                IsFieldViewerEngaged = !IsFieldViewerEngaged;
            }
        }

        private void PopulateCanvas(object? sender, EventArgs e)
        {
            contrastView.contrastSettings.Clear();
            _contrastAcqDetPairIntDict.Clear();
            _contrastIntAcqDetPairDict.Clear();

            if (sender is FieldViewerViewModel vm && vm.Acquisitions.Count > 0 && vm.Detectors.Count > 0)
            {
                foreach (var (acquisition, row) in vm.Acquisitions.Select((v, i) => (v, i)))
                {
                    foreach (var (detector, col) in vm.Detectors.Select((v, i) => (v, i)))
                    {
                        var key = Tuple.Create(acquisition, detector);

                        contrastView.PopulateContrastSettings(this, new GridContrastEventArgs(row, col, null, null));
                        contrastView.contrastSettings[row][col].IsAutoContrastEnabled = false;
                        _contrastAcqDetPairIntDict[key] = new Tuple<int, int>(row, col);
                        _contrastIntAcqDetPairDict[new Tuple<int, int>(row, col)] = key;


                        if (!_canvasContextDict.ContainsKey(key))
                        {
                            _canvasContextDict.Clear();
                        }
                        if (_contrastSettings.ContainsKey(key))
                        {
                            contrastView.contrastSettings[row][col].Min_disp_val = _contrastSettings[key].Item1;
                            contrastView.contrastSettings[row][col].Max_disp_val = _contrastSettings[key].Item2;
                        }
                    }
                }
            
                var parentGrid = this.FindControl<Grid>("ParentCanvasGrid");

                foreach (var (acquisition, row) in vm.Acquisitions.Select((v, i) => (v, i)))
                {
                    foreach (var (detector, col) in vm.Detectors.Select((v, i) => (v, i)))
                    {
                        var canvas = new Canvas { IsVisible = false };

                        canvas.PointerWheelChanged += OnPointerWheelChanged;
                        canvas.PointerPressed += OnPointerPressed;
                        canvas.PointerMoved += OnPointerMoved;
                        canvas.PointerReleased += OnPointerReleased;

                        var tf = new TransformGroup();
                        tf.Children.Add(_translateTransform);
                        tf.Children.Add(_scaleTransform);
                        canvas.RenderTransform = tf;
                        canvas.Background = Brushes.Transparent;
                        var key = Tuple.Create(acquisition, detector);
                        if (!_canvasContextDict.ContainsKey(key))
                        {
                            var context = new CanvasContext
                            {
                                Canvas = canvas,
                                ScreenPosX = _init_pos_x,
                                ScreenPosY = _init_pos_y
                            };

                            _canvasContextDict[key] = context;
                        }
                        parentGrid.Children.Add(canvas);
                        contrastView.SetCurrentContrastVals(this, new GridContrastEventArgs(row, col, null, null));
                    }
                }
            }
        }



        public double CalculateXScreenSpaceCoordinate(TiffPlaneMetadata metadata, double center_x, double current_x)
        {
            double pos_x = center_x == double.MaxValue ? 0 : metadata.PositionX - center_x;
            return current_x + pos_x / ((_PixelSizeNM * 10) / 1000);
        }

        public double CalculateYScreenSpaceCoordinate(TiffPlaneMetadata metadata, double center_y, double current_y)
        {
            double pos_y = center_y == double.MaxValue ? 0 : metadata.PositionY - center_y;
            return current_y - pos_y / ((_PixelSizeNM * 10) / 1000);
        }

        public async Task UpdateFieldViewImage(ImageData imageData)
        {
            Dispatcher.UIThread.Invoke(() => { 
            if (DataContext is FieldViewerViewModel vm)
            {
                _PixelSizeNM = vm.PixelSize;
            }});

            for (int i = 0; i < imageData.Images.Count; i++)
            {
                var metadata = imageData.Metadata[i];

                var key = Tuple.Create(metadata.AcquisitionName, metadata.DetectorName);
                if (!_canvasContextDict.TryGetValue(key, out var context))
                    continue;

                bool xyDisplacement = Math.Abs(metadata.PositionX - context.CenterX) > _DisplacementThsMicron ||
                                        Math.Abs(metadata.PositionY - context.CenterY) > _DisplacementThsMicron;

                if (xyDisplacement)
                {
                    byte[] bytes = new byte[imageData.Images[i].Length];

                    context.ScreenPosX = CalculateXScreenSpaceCoordinate(metadata, context.CenterX, context.ScreenPosX);
                    context.ScreenPosY = CalculateYScreenSpaceCoordinate(metadata, context.CenterY, context.ScreenPosY);

                    if (IsFieldViewerEngaged)
                    {
                        int row = _contrastAcqDetPairIntDict[key].Item1;
                        int col = _contrastAcqDetPairIntDict[key].Item2;

                        int minval = contrastView.contrastSettings[row][col].Min_disp_val;
                        int maxval = contrastView.contrastSettings[row][col].Max_disp_val;
                        Stopwatch stopWatch = new Stopwatch();
                        stopWatch.Start();
                        var bitmap = new WriteableBitmap(
                            new Avalonia.PixelSize((int)metadata.Width / 8, (int)metadata.Height / 8),
                            new Vector(96, 96),
                            Avalonia.Platform.PixelFormats.Gray8,
                            Avalonia.Platform.AlphaFormat.Premul);
                        
                        var contrast_adj_image = TiffHandler.UpdateContrastMinMaxIn16Bit(imageData.Images[i], minval, maxval);
                        context._lastImage = TiffHandler.GetSubsampledImage(contrast_adj_image, (int)metadata.Width, (int)metadata.Height);
                        var new_bitmap = TiffHandler.ReturnBitmapFromByteArray(context._lastImage, bitmap);


                        stopWatch.Stop();
                        Console.WriteLine(stopWatch.ElapsedMilliseconds);
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            UpdateCanvasImage(key, metadata, new_bitmap);
                        });
                        Console.WriteLine($"Detection index:{metadata.DetectionIndex}");

                        context.CenterX = metadata.PositionX;
                        context.CenterY = metadata.PositionY;
                        context.CenterZ = metadata.PositionZ;
                    }
                }
                //else if (Math.Abs(metadata.PositionZ - context.CenterZ) > ZDisplacementTHS)
                //{
                //    if (IsFieldViewerEngaged)
                //    {
                //        byte[] bytes = new byte[imageData.Images[i].Length];
                //        var img = TiffHandler.CopyBytes(imageData.Images[i], bytes);

                //        byte[] newImg = TiffHandler.GetSubsampledImage(img, (int)metadata.Width, (int)metadata.Height);
                //        if (context._lastImage == null)
                //        {
                //            context._lastImage = newImg;
                //        }
                //        context._lastImage = TiffHandler.MaxIntensityProject8Bit(context._lastImage, newImg);

                //        Dispatcher.UIThread.Post(() =>
                //        {
                //            var control_indx = context.Canvas.Children.ToList().FindLastIndex(x => x is Image);
                //            if (control_indx != -1)
                //            {
                //                context.Canvas.Children.RemoveAt(control_indx);
                //            }

                           
                //            context.ScreenPosX = CalculateXScreenSpaceCoordinate(metadata, context.CenterX, context.ScreenPosX);
                //            context.ScreenPosY = CalculateYScreenSpaceCoordinate(metadata, context.CenterY, context.ScreenPosY);



                //            UpdateCanvasImage(key, metadata, TiffHandler.UpdateAutoContrast8Bit(context._lastImage));
                //        }, DispatcherPriority.Background);
                //    }
                //    context.CenterZ = metadata.PositionZ;

                //}
            }
            
        }


        private void UpdateCanvasImage(Tuple<string, string> key, TiffPlaneMetadata metadata, WriteableBitmap image)
        {
            if (!_canvasContextDict.TryGetValue(key, out var context))
                return;

            var canvas = context.Canvas;



            var imgControl = new Image
            {
                Source = image,
                Stretch = Stretch.Uniform,
                Width = metadata.Width / 10,
                Height = metadata.Height / 10,
                ZIndex = 0
            };


            Canvas.SetLeft(imgControl, context.ScreenPosX);
            Canvas.SetTop(imgControl, context.ScreenPosY);

            LastImageTransformX = context.ScreenPosX;
            LastImageTransformY = context.ScreenPosY;


            canvas.Children.Add(imgControl);
        }

        private void GoToCurrent(object? sender, RoutedEventArgs e)
        {
            _scaleTransform.ScaleX = 1;
            _scaleTransform.ScaleY = 1;
            _translateTransform.X =  -(LastImageTransformX - _init_pos_x);
            _translateTransform.Y =  -(LastImageTransformY - _init_pos_y);

        }

        private void ClearSelected(object? sender, RoutedEventArgs e)
        {
            foreach( var item in _canvasContextDict.Values )
            {
                item.Canvas.Children.Clear();
            }
        }

        private void OnResetView(object? sender, RoutedEventArgs e)
        {
            _translateTransform.X = 0;
            _translateTransform.Y = 0;
            _scaleTransform.ScaleX = 0.5;
            _scaleTransform.ScaleY = 0.5;
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            double zoomFactor = e.Delta.Y > 0 ? 1.1 : 0.9;
            _scaleTransform.ScaleX *= zoomFactor;
            _scaleTransform.ScaleY *= zoomFactor;
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _lastMousePosition = e.GetPosition(this.FindControl<Grid>("ParentGrid"));
            _isDragging = true;
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isDragging)
            {
                var currentPosition = e.GetPosition(this.FindControl<Grid>("ParentGrid"));
                var delta = currentPosition - _lastMousePosition;

                _translateTransform.X += delta.X;
                _translateTransform.Y += delta.Y;
                _lastMousePosition = currentPosition;
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _isDragging = false;
        }
    }
}

