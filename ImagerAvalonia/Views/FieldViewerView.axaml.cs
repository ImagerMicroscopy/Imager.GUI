using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ImagerAvalonia.Services.Storage;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.Views.ViewUtils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ImagerAvalonia.Views
{
    /// <summary>
    /// The field viewer: a live, coarse map of the sample built from acquired
    /// frames, one canvas overlay per acquisition x detector pair. The spec is
    /// in FieldViewerSpecTests; the placement logic is FieldViewerMosaic. This
    /// class is UI wiring only: every piece of shared state lives on the UI
    /// thread. The acquisition thread only writes into the image channel, and
    /// the pump task forwards batches to the UI thread - there are no
    /// cross-thread dictionary accesses.
    /// </summary>
    public partial class FieldViewerView : UserControl
    {
        private const int ChannelCapacity = 64;

        private readonly FieldViewerMosaic _mosaic = new();
        private Channel<ImageData>? _imageChannel;
        private CancellationTokenSource? _pumpShutdown;

        private readonly Dictionary<(string Acquisition, string Detector), Canvas> _pairCanvases = new();
        private readonly Dictionary<(string Acquisition, string Detector), (int Row, int Col)> _contrastCellForPair = new();
        private readonly Dictionary<(string Acquisition, string Detector), (int Min, int Max)> _contrastSettings = new();
        private readonly Dictionary<FieldViewerTile, Image> _tileControls = new();
        private readonly List<Canvas> _createdCanvases = new();

        private Canvas? _selectedCanvas;
        private (string Acquisition, string Detector)? _selectedPair;
        private FieldViewerViewModel? _viewModel;

        private ScaleTransform? _scaleTransform;
        private TranslateTransform? _translateTransform;
        private ContrastPanelView? _contrastView;

        private double _initPosX;
        private double _initPosY;
        private double _lastTileX;
        private double _lastTileY;

        private bool _isDragging;
        private Point _lastMousePosition;
        private Point _contextClickPoint;

        public FieldViewerView()
        {
            InitializeComponent();

            var screenBounds = ScreenHelper.ScreenSize;
            var screenScale = ScreenHelper.Scaling;
            _initPosX = (screenBounds.Width / 2 - 260 * screenScale) / screenScale;
            _initPosY = screenBounds.Height / 2 / screenScale;
            _mosaic.OriginX = _initPosX;
            _mosaic.OriginY = _initPosY;

            var transformGroup = (TransformGroup)((Canvas)this.FindControl<Canvas>("CNVS")).RenderTransform;
            _scaleTransform = (ScaleTransform)transformGroup.Children[0];
            _translateTransform = (TranslateTransform)transformGroup.Children[1];

            _contrastView = this.FindControl<ContrastPanelView>("ContrastPanelFieldView");
            _contrastView.SetSliderVisibility(false);
            _contrastView.SetHistogramVisibility(false);
            _contrastView.SetToggleAutoContrastVisibility(false);

            // The tab control caches this view (see ImageTabContentTemplate) and
            // attaches/detaches it on every tab switch, so activation is a
            // suspend/resume cycle, not a create/destroy one.
            DataContextChanged += OnDataContextChanged;
            AttachedToVisualTree += OnAttachedToVisualTree;
            DetachedFromVisualTree += OnDetachedFromVisualTree;
            ContextRequested += OnContextRequested;
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


        // --- Image pipeline ---------------------------------------------------

        /// <summary>
        /// Drains the image channel (written by the acquisition thread) and
        /// forwards each batch to the UI thread. Bounded with DropOldest: the
        /// map only needs recent frames, so under load the oldest pending
        /// batches are discarded instead of queueing unboundedly.
        /// </summary>
        private async Task PumpImagesAsync()
        {
            var channel = _imageChannel!;
            var token = _pumpShutdown!.Token;
            try
            {
                while (await channel.Reader.WaitToReadAsync(token).ConfigureAwait(false))
                {
                    while (channel.Reader.TryRead(out var batch))
                        Dispatcher.UIThread.Post(() => ProcessBatch(batch));
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void OnImageData(object? sender, ImageData batch) => _imageChannel?.Writer.TryWrite(batch);

        private void ProcessBatch(ImageData batch)
        {
            var vm = _viewModel;
            if (vm is null || !vm.IsStageViewerEngaged)
                return;

            _mosaic.PixelSizeNm = vm.PixelSize;

            for (int i = 0; i < batch.Images.Count; i++)
            {
                var metadata = batch.Metadata[i];
                var key = (metadata.AcquisitionName, metadata.DetectorName);
                if (!_mosaic.TryGetChannel(key, out var channel) || !_pairCanvases.TryGetValue(key, out var canvas))
                    continue;

                if (!_mosaic.ShouldPlace(metadata, channel, out var screenX, out var screenY))
                    continue;

                var cell = _contrastCellForPair.TryGetValue(key, out var cellIndex)
                    ? _contrastView!.contrastSettings[cellIndex.Row][cellIndex.Col]
                    : null;
                if (cell == null)
                    continue;

                // The cell adapts its bounds and stored limits to the pixel format.
                cell.SetPixelFormat(metadata.Type);
                _contrastSettings[key] = (cell.Min_disp_val, cell.Max_disp_val);

                var sample = TiffHandler.SubsampleToUshort(
                    batch.Images[i], (int)metadata.Width, (int)metadata.Height, metadata.Type);
                var tile = _mosaic.Place(
                    metadata, channel, sample,
                    (int)metadata.Width / 8, (int)metadata.Height / 8,
                    screenX, screenY, cell.Min_disp_val, cell.Max_disp_val);

                DrawTile(canvas, tile, metadata);

                _lastTileX = screenX;
                _lastTileY = screenY;
            }
        }

        private void DrawTile(Canvas canvas, FieldViewerTile tile, TiffPlaneMetadata metadata)
        {
            var bitmap = TiffHandler.SampledToGray8(
                tile.Sample, tile.Width, tile.Height, tile.Format, tile.ContrastMin, tile.ContrastMax);

            var image = new Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform,
                // One screen unit spans 10 image pixels (see FieldViewerMosaic).
                Width = metadata.Width / 10,
                Height = metadata.Height / 10,
                ZIndex = 0,
            };
            Canvas.SetLeft(image, tile.ScreenX);
            Canvas.SetTop(image, tile.ScreenY);
            canvas.Children.Add(image);
            _tileControls[tile] = image;
        }

        // --- Contrast -----------------------------------------------------------

        private void OnContrastChanged(object? sender, EventArgs e)
        {
            if (_selectedPair is not { } key)
                return;
            if (!_mosaic.TryGetChannel(key, out var channel) || !_contrastCellForPair.TryGetValue(key, out var cellIndex))
                return;

            var cell = _contrastView!.contrastSettings[cellIndex.Row][cellIndex.Col];
            var (min, max) = (cell.Min_disp_val, cell.Max_disp_val);
            _contrastSettings[key] = (min, max);

            // Contrast applies to the whole map, not just future tiles: each
            // tile is re-rendered from its cached sample.
            foreach (var tile in channel.Tiles)
            {
                if (tile.ContrastMin == min && tile.ContrastMax == max)
                    continue;
                tile.ContrastMin = min;
                tile.ContrastMax = max;

                if (_tileControls.TryGetValue(tile, out var image) && image.Source is WriteableBitmap oldBitmap)
                {
                    image.Source = TiffHandler.SampledToGray8(tile.Sample, tile.Width, tile.Height, tile.Format, min, max);
                    oldBitmap.Dispose();
                }
            }
        }

        // --- View-model wiring ----------------------------------------------------

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            Suspend();
            Resume();
        }

        private void Subscribe(FieldViewerViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            vm.FocusViewInitialized += OnSchemaInitialized;
            vm.UpdateImageData += OnImageData;
        }

        private void Unsubscribe(FieldViewerViewModel vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            vm.FocusViewInitialized -= OnSchemaInitialized;
            vm.UpdateImageData -= OnImageData;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_viewModel == null)
                return;

            switch (e.PropertyName)
            {
                case nameof(FieldViewerViewModel.CurrentStagePosition):
                    // Read when the viewer engages; anchors every channel that
                    // has not received frames yet. An unreadable position leaves
                    // the anchoring to the first frame.
                    if (_viewModel.CurrentStagePosition is { } position)
                        _mosaic.SetAnchor(position.Coordinates.x, position.Coordinates.y);
                    break;

                case nameof(FieldViewerViewModel.SelectedAcquisition):
                case nameof(FieldViewerViewModel.SelectedDetector):
                    ShowSelectedPair();
                    break;
            }
        }

        // --- Schema and selection ------------------------------------------------

        private void OnSchemaInitialized(object? sender, EventArgs e)
        {
            if (DataContext is FieldViewerViewModel vm)
                RebuildSchema(vm);
        }

        private void RebuildSchema(FieldViewerViewModel vm)
        {
            var parentGrid = this.FindControl<Grid>("ParentCanvasGrid")!;

            // A new session's schema replaces the previous one entirely: the
            // old canvases leave the tree (no orphans), the per-pair contrast
            // settings survive.
            foreach (var canvas in _createdCanvases)
                parentGrid.Children.Remove(canvas);
            _createdCanvases.Clear();
            _pairCanvases.Clear();
            _tileControls.Clear();
            _selectedCanvas = null;
            _contrastView!.contrastSettings.Clear();
            _contrastCellForPair.Clear();

            var keys = new List<(string, string)>();
            foreach (var (acquisition, row) in vm.Acquisitions.Select((v, i) => (v, i)))
            {
                foreach (var (detector, col) in vm.Detectors.Select((v, i) => (v, i)))
                {
                    var key = (acquisition, detector);
                    keys.Add(key);

                    _contrastView.PopulateContrastSettings(this, new GridContrastEventArgs(row, col, null, null));
                    var cell = _contrastView.contrastSettings[row][col];
                    cell.IsAutoContrastEnabled = false;
                    if (_contrastSettings.TryGetValue(key, out var saved))
                    {
                        cell.Min_disp_val = saved.Min;
                        cell.Max_disp_val = saved.Max;
                    }
                    _contrastCellForPair[key] = (row, col);

                    var canvas = new Canvas { IsVisible = false, Background = Brushes.Transparent };
                    canvas.PointerWheelChanged += OnPointerWheelChanged;
                    canvas.PointerPressed += OnPointerPressed;
                    canvas.PointerMoved += OnPointerMoved;
                    canvas.PointerReleased += OnPointerReleased;

                    var transform = new TransformGroup();
                    transform.Children.Add(_translateTransform!);
                    transform.Children.Add(_scaleTransform!);
                    canvas.RenderTransform = transform;

                    _pairCanvases[key] = canvas;
                    _createdCanvases.Add(canvas);
                    parentGrid.Children.Add(canvas);

                    // Selecting the last cell mirrors the previous behavior;
                    // the panel then has a selection before the user picks one.
                    _contrastView.SetCurrentContrastVals(this, new GridContrastEventArgs(row, col, null, null));
                    _selectedPair = key;
                }
            }

            _mosaic.Rebuild(keys);

            // The anchor may already be known from an earlier engage; the
            // selection may already be set on the view model.
            if (vm.CurrentStagePosition is { } position)
                _mosaic.SetAnchor(position.Coordinates.x, position.Coordinates.y);
            ShowSelectedPair();
        }

        private void ShowSelectedPair()
        {
            var vm = _viewModel;
            if (vm == null || vm.SelectedAcquisition == null || vm.SelectedDetector == null)
                return;

            var key = (vm.SelectedAcquisition, vm.SelectedDetector);
            if (!_pairCanvases.TryGetValue(key, out var canvas))
                return;

            _selectedCanvas?.SetValue(IsVisibleProperty, false);
            canvas.IsVisible = true;
            _selectedCanvas = canvas;
            _selectedPair = key;

            if (_contrastCellForPair.TryGetValue(key, out var cellIndex))
                _contrastView!.SetCurrentContrastVals(this, new GridContrastEventArgs(cellIndex.Row, cellIndex.Col, null, null));
        }

        // --- Lifecycle -------------------------------------------------------------

        /// <summary>
        /// Activates the view for the view model in its DataContext: subscribes
        /// to its events, starts the image pump, and catches up on the schema
        /// and anchor if they changed while the view was detached. Idempotent
        /// for the same view model.
        /// </summary>
        private void Resume()
        {
            if (DataContext is not FieldViewerViewModel vm || _viewModel == vm)
                return;

            _viewModel = vm;
            Subscribe(vm);
            _contrastView!.UpdateContrastOnCurrentImage += OnContrastChanged;

            _imageChannel = Channel.CreateBounded<ImageData>(
                new BoundedChannelOptions(ChannelCapacity) { FullMode = BoundedChannelFullMode.DropOldest });
            _pumpShutdown = new CancellationTokenSource();
            _ = PumpImagesAsync();

            if (vm.CurrentStagePosition is { } position)
                _mosaic.SetAnchor(position.Coordinates.x, position.Coordinates.y);

            // A session may have started while the view was detached; rebuild
            // the schema if it no longer matches the view model.
            if (!SchemaMatches(vm))
                RebuildSchema(vm);
        }

        /// <summary>
        /// Deactivates the view: unsubscribes and stops the pump. The view model
        /// and the canvases (tiles, zoom, contrast) are kept, so a re-attach
        /// continues where the view left off.
        /// </summary>
        private void Suspend()
        {
            if (_viewModel == null)
                return;

            Unsubscribe(_viewModel);
            _viewModel = null;
            _contrastView!.UpdateContrastOnCurrentImage -= OnContrastChanged;
            _pumpShutdown?.Cancel();
            _imageChannel?.Writer.TryComplete();
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e) => Resume();

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e) => Suspend();

        /// <summary>Whether the canvases still match the view model's schema.</summary>
        private bool SchemaMatches(FieldViewerViewModel vm)
        {
            if (_pairCanvases.Count != vm.Acquisitions.Count * vm.Detectors.Count)
                return false;

            foreach (var acquisition in vm.Acquisitions)
                foreach (var detector in vm.Detectors)
                    if (!_pairCanvases.ContainsKey((acquisition, detector)))
                        return false;
            return true;
        }

        // --- Stage moves from the map ----------------------------------------------

        private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
        {
            if (!TryGetMoveTarget(_contextClickPoint, out var stageX, out var stageY))
                return;

            e.Handled = true;

            var item = new MenuItem { Header = "Move stage here" };
            item.Click += (_, _) =>
            {
                if (_viewModel is { } vm)
                    _ = vm.MoveStageTo(stageX, stageY);
            };

            var menu = new ContextMenu();
            menu.Items.Add(item);
            menu.Open(this);
        }

        /// <summary>
        /// The stage target a right-click on the map corresponds to. Available
        /// only when a map exists, its anchor is a trusted stage read (engage),
        /// and no measurement is running and no stage move is in flight.
        /// </summary>
        internal bool TryGetMoveTarget(Point gridPoint, out double stageX, out double stageY)
        {
            stageX = stageY = 0;

            if (_viewModel is not { } vm || vm.IsMeasurementRunning || vm.IsStageMoveInFlight)
                return false;
            if (_createdCanvases.Count == 0)
                return false;

            // The click is in screen (grid) coordinates; tiles are placed in
            // canvas coordinates behind the shared pan/zoom transform. All pair
            // canvases share the same transform, so the first one's matrix is
            // the map's transform.
            var canvas = _createdCanvases[0];
            var canvasPoint = gridPoint.Transform(canvas.RenderTransform!.Value.Invert());

            _mosaic.PixelSizeNm = vm.PixelSize;
            return _mosaic.TryGetStagePositionAt(canvasPoint.X, canvasPoint.Y, out stageX, out stageY);
        }

        /// <summary>Test seam: sets the shared pan/zoom transform.</summary>
        internal void SetMapTransformForTests(double scale, double translateX, double translateY)
        {
            _scaleTransform!.ScaleX = scale;
            _scaleTransform.ScaleY = scale;
            _translateTransform!.X = translateX;
            _translateTransform.Y = translateY;
        }

        // --- View controls --------------------------------------------------------

        private void GoToCurrent(object? sender, RoutedEventArgs e)
        {
            _scaleTransform!.ScaleX = 1;
            _scaleTransform.ScaleY = 1;
            _translateTransform!.X = -(_lastTileX - _initPosX);
            _translateTransform.Y = -(_lastTileY - _initPosY);
        }

        private void ClearSelected(object? sender, RoutedEventArgs e)
        {
            foreach (var canvas in _createdCanvases)
                canvas.Children.Clear();

            foreach (var image in _tileControls.Values)
                (image.Source as WriteableBitmap)?.Dispose();
            _tileControls.Clear();
            _mosaic.ClearTiles();
        }

        private void OnResetView(object? sender, RoutedEventArgs e)
        {
            _translateTransform!.X = 0;
            _translateTransform.Y = 0;
            _scaleTransform!.ScaleX = 0.5;
            _scaleTransform.ScaleY = 0.5;
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            double zoomFactor = e.Delta.Y > 0 ? 1.1 : 0.9;
            _scaleTransform!.ScaleX *= zoomFactor;
            _scaleTransform.ScaleY *= zoomFactor;
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this.FindControl<Grid>("ParentGrid"));

            if (point.Properties.IsRightButtonPressed)
            {
                // Right click targets a stage move; the pan drag starts on
                // left press only.
                _contextClickPoint = point.Position;
                return;
            }

            _lastMousePosition = point.Position;
            _isDragging = true;
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isDragging)
            {
                var currentPosition = e.GetPosition(this.FindControl<Grid>("ParentGrid"));
                var delta = currentPosition - _lastMousePosition;

                _translateTransform!.X += delta.X;
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
