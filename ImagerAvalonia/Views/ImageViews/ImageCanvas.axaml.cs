using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.Views.ViewUtils;
using System;
using System.ComponentModel;

namespace ImagerAvalonia.Views;

/// <summary>
/// One tile of the image grid. The image and the region overlays sit in a fixed
/// 800x800 world panel; the tile scales that panel to fit its body and applies
/// zoom/pan to the same single transform, so overlays and image pixels stay
/// aligned at every tile size and zoom level. (Region coordinates are defined in
/// the 800x800 world space; the transform lives on an ancestor of the overlay
/// canvas, so pointer positions reported to the region controls remain in world
/// space, unchanged.)
/// </summary>
public partial class ImageCanvas : UserControl
{
    private const double WorldSize = 800;
    private const double MaxZoom = 32;

    private readonly object _bitmapLock = new object();
    private readonly ImageCanvasViewModel _viewModel;
    private readonly ImageRegionDisplayView imageRegions;
    private readonly Canvas _canvas;
    private readonly Image _image;
    private readonly Panel _worldHost;
    private readonly Grid _tileBody;

    private readonly ScaleTransform _scaleTransform = new(1, 1);
    private readonly TranslateTransform _translateTransform = new(0, 0);

    /// <summary>Scale at which the world panel exactly fits the tile body.</summary>
    private double _fitScale = 1;

    /// <summary>The tile body size the current viewport was fitted to; used to
    /// detect actual size changes in ArrangeOverride.</summary>
    private Size _lastBodySize = default;

    private int _lastWidth = 0;
    private int _lastHeight = 0;
    private byte[] _lastImage = new byte[4];
    private ImagePixelFormat _lastFormat = ImagePixelFormat.Mono16;
    private WriteableBitmap _bitmap = new WriteableBitmap(
                    new Avalonia.PixelSize(1, 1),
                    new Vector(96, 96),
                    Avalonia.Platform.PixelFormats.Rgba8888,
                    Avalonia.Platform.AlphaFormat.Premul);

    public ImageCanvas(ImageCanvasViewModel slotViewModel, ImageRegionDisplayViewModel imregionvm)
    {
        InitializeComponent();

        _viewModel = slotViewModel;
        DataContext = _viewModel;
        _viewModel.ContrastSettings.OnContrastValsChanged += ContrastSettings_OnContrastValsChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        imageRegions = new ImageRegionDisplayView(imregionvm);

        _image = this.FindControl<Image>("DisplayedImage")!;
        _canvas = this.FindControl<Canvas>("DisplayedCanvas")!;
        _worldHost = this.FindControl<Panel>("WorldHost")!;
        _tileBody = this.FindControl<Grid>("TileBody")!;
        _canvas.Children.Add(imageRegions);

        // Zoom and pan act on the world panel: image and overlays transform together.
        _worldHost.RenderTransform = new TransformGroup
        {
            Children = new Transforms { _scaleTransform, _translateTransform }
        };

        _tileBody.PointerWheelChanged += OnPointerWheelChanged;
        _tileBody.PointerPressed += OnPointerPressed;
        _tileBody.PointerMoved += OnPointerMoved;
        _tileBody.PointerReleased += OnPointerReleased;

        // This tile's contrast settings and histogram, bound to the slot's
        // contrast view model. The panel subscribes itself via DataContextChanged.
        var contrastPanelHost = this.FindControl<ContentControl>("ContrastPanelHost")!;
        contrastPanelHost.Content = new ContrastPanelView
        {
            DataContext = _viewModel.ContrastSettings
        };
    }

    /// <summary>
    /// Every size change (initial layout, window resize, maximizing the tile)
    /// re-fits the world panel: zoom state is transient inspection state and must
    /// not survive a size change. Arranges at an unchanged size are ignored, so
    /// unrelated layout passes do not clear the zoom. Zooming itself only touches
    /// the render transform, so it never triggers a re-fit.
    /// </summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);

        var bodySize = _tileBody.Bounds.Size;
        if (bodySize != _lastBodySize)
        {
            _lastBodySize = bodySize;
            FitWorld();
        }
        return result;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Selection (IsSelected) has no visual treatment for now: the green border
        // was visually distracting, so selection remains purely logical state
        // (see ImageCanvasViewModel.IsSelected) until a better indicator is
        // designed. Only the contrast overlay needs to react here.
        if (e.PropertyName == nameof(ImageCanvasViewModel.IsContrastPanelVisible) &&
                 _viewModel.IsContrastPanelVisible)
        {
            // Show the histogram of the frame currently on display immediately,
            // instead of waiting for the next frame to arrive.
            if (_lastWidth > 0 && _lastHeight > 0 &&
                _lastImage.Length == _lastWidth * _lastHeight * _lastFormat.BytesPerPixel())
            {
                _viewModel.ContrastSettings.UpdateHistogram(_lastImage, _lastFormat);
            }
        }
    }

    private void ContrastSettings_OnContrastValsChanged(object? sender, EventArgs e)
    {
        if (_image != null)
        {
            lock (_bitmapLock)
            {
                UpdateBitmap(_lastWidth, _lastHeight, _lastImage, _lastFormat);
            }
            Dispatcher.UIThread.Invoke(() => { SetBitmap(); InvalidateBitmap(); });
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    internal void UpdateBitmap(int width, int height, byte[] image, ImagePixelFormat format)
    {
        lock (_bitmapLock)
        {
            if (image != null)
            {
                if (image.Length == width * height * format.BytesPerPixel())
                {
                    _lastImage = new byte[image.Length];
                    unsafe
                    {
                        fixed (byte* src = image)
                        fixed (byte* dst = _lastImage)
                        {
                            Buffer.MemoryCopy(src, dst, _lastImage.Length, image.Length);
                        }
                    }

                    _lastWidth = width;
                    _lastHeight = height;
                    _lastFormat = format;

                    var settings = _viewModel.ContrastSettings;

                    if (_bitmap.Size != new Size(width, height))
                        _bitmap = new WriteableBitmap(
                            new Avalonia.PixelSize(width, height),
                            new Vector(96, 96),
                            Avalonia.Platform.PixelFormats.Rgba8888,
                            Avalonia.Platform.AlphaFormat.Premul);

                    TiffHandler.UpdateContrastAndReturnBitmap(image, width, height, format,
                        settings.Min_disp_val, settings.Max_disp_val,
                        settings.IsAutoContrastEnabled, _bitmap);
                }
            }
        }
    }

    internal void SetBitmap()
    {
        _image.Source = _bitmap;
    }

    internal void InvalidateBitmap()
    {
        _image.InvalidateVisual();
    }

    internal void EnableRegionSelection(ImageElementControlBase region)
    {
        imageRegions.EnableSelection(region);
    }

    internal void DisableRegionSelection()
    {
        imageRegions.SubmitSelection();
    }

    // --- Tile viewport: fit, zoom, pan -------------------------------------

    /// <summary>Scales the world panel so it exactly fits the tile body, centered,
    /// and clears any zoom/pan. Used on resize and by the grid's reset.</summary>
    internal void FitWorld()
    {
        double bodyWidth = _tileBody.Bounds.Width;
        double bodyHeight = _tileBody.Bounds.Height;
        if (bodyWidth <= 0 || bodyHeight <= 0) return;

        _fitScale = Math.Min(bodyWidth, bodyHeight) / WorldSize;
        _scaleTransform.ScaleX = _fitScale;
        _scaleTransform.ScaleY = _fitScale;
        ClampViewport();
    }

    /// <summary>
    /// Constrains the world panel's position to the tile body, per axis:
    /// while the world is larger than the body it must cover it entirely (the
    /// image can be moved but never pushed off the tile); while it is smaller it
    /// is centered — a shifted, partially visible image has no use. In
    /// particular, zooming out to the fitted scale always restores exactly the
    /// fitted, centered state, whatever was zoomed or panned before.
    /// </summary>
    private void ClampViewport()
    {
        double bodyWidth = _tileBody.Bounds.Width;
        double bodyHeight = _tileBody.Bounds.Height;
        double worldWidth = WorldSize * _scaleTransform.ScaleX;
        double worldHeight = WorldSize * _scaleTransform.ScaleY;

        _translateTransform.X = worldWidth >= bodyWidth
            ? Math.Clamp(_translateTransform.X, bodyWidth - worldWidth, 0)
            : (bodyWidth - worldWidth) / 2;
        _translateTransform.Y = worldHeight >= bodyHeight
            ? Math.Clamp(_translateTransform.Y, bodyHeight - worldHeight, 0)
            : (bodyHeight - worldHeight) / 2;
    }

    /// <summary>
    /// Zooms by the given factor, anchored at the given point in tile-body
    /// coordinates: the world point under it stays in place while the scale
    /// changes. The scale is clamped to [fit, MaxZoom]; the position is then
    /// constrained by ClampViewport (zooming out to the fit scale therefore
    /// recenters).
    /// </summary>
    internal void ZoomAt(Point anchor, double factor)
    {
        double oldScale = _scaleTransform.ScaleX;
        double newScale = Math.Clamp(oldScale * factor, _fitScale, MaxZoom);
        if (newScale == oldScale) return;

        _scaleTransform.ScaleX = newScale;
        _scaleTransform.ScaleY = newScale;
        _translateTransform.X = anchor.X - (newScale / oldScale) * (anchor.X - _translateTransform.X);
        _translateTransform.Y = anchor.Y - (newScale / oldScale) * (anchor.Y - _translateTransform.Y);
        ClampViewport();
    }

    /// <summary>Moves the world panel by the given offset in tile-body
    /// coordinates, constrained by ClampViewport.</summary>
    internal void PanBy(Vector delta)
    {
        _translateTransform.X += delta.X;
        _translateTransform.Y += delta.Y;
        ClampViewport();
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        double factor = e.Delta.Y > 0 ? 1.1 : 1.0 / 1.1;
        double before = _scaleTransform.ScaleX;
        ZoomAt(e.GetPosition(_tileBody), factor);
        if (_scaleTransform.ScaleX != before)
        {
            e.Handled = true;
        }
    }

    private bool _isPanning;
    private Point _lastPanPosition;

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Panning is bound to the middle button only: the left button belongs to
        // region selection and tile selection, so the gestures cannot collide.
        if (e.GetCurrentPoint(_tileBody).Properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _lastPanPosition = e.GetPosition(_tileBody);
            e.Handled = true;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning) return;

        var position = e.GetPosition(_tileBody);
        PanBy(position - _lastPanPosition);
        _lastPanPosition = position;
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            e.Handled = true;
        }
    }
}
