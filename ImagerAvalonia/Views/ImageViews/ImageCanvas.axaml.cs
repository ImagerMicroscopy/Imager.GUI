using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;
using System;

namespace ImagerAvalonia.Views;

public partial class ImageCanvas : UserControl
{
    private readonly object _bitmapLock = new object();
    private ImageCanvasViewModel _viewModel;
    private ImageRegionDisplayView imageRegions;
    private ImageRegionDisplayViewModel _imageRegionDisplay;
    private Canvas _canvas;
    private Image _image;
    private int _lastWidth = 0;
    private int _lastHeight = 0;
    private byte[] _lastImage = new byte[4];
    private WriteableBitmap _bitmap = new WriteableBitmap(
                    new Avalonia.PixelSize(1, 1),
                    new Vector(96, 96),
                    Avalonia.Platform.PixelFormats.Rgba8888,
                    Avalonia.Platform.AlphaFormat.Premul);



    public ImageCanvas(string acq, string det, ImageRegionDisplayViewModel imregionvm)
    {
        InitializeComponent();

        _viewModel = new ImageCanvasViewModel(acq, det);
        //_viewModel.SetHeader(acq, det);
        _viewModel.ContrastSettings.OnContrastValsChanged += ContrastSettings_OnContrastValsChanged;
        _imageRegionDisplay = imregionvm;

        DataContext = _viewModel;
        _image = this.FindControl<Image>("DisplayedImage")!;
        _canvas = this.FindControl<Canvas>("DisplayedCanvas")!;
        imageRegions = new ImageRegionDisplayView(imregionvm);
        _canvas.Children.Add(imageRegions);
    }

    private void ContrastSettings_OnContrastValsChanged(object? sender, EventArgs e)
    {
        if (_image != null)
        {
     
            lock (_bitmapLock)
            {
                UpdateBitmap(_lastWidth, _lastHeight, _lastImage);
            }
            Dispatcher.UIThread.Invoke(() => { SetBitmap(); InvalidateBitmap(); });

        }
    }





    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }


    public DropShadowEffect SelectedEffect()
    {
        DropShadowEffect ef = new DropShadowEffect();
        ef.Color = Avalonia.Media.Colors.Aquamarine;
        ef.BlurRadius = 30;
        ef.Opacity = 0.5;
        return ef;
    }

    internal void UpdateBitmap(int width, int height, byte[] image)
    {
        lock (_bitmapLock)
        {
            if (image != null)
            {
                if (image.Length == width * height * 2)
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
                    double[] histogram_vals_y = new double[257];
                    double[] histogram_vals_y_low = new double[257];
                    double[] histogram_vals_x = new double[257];

                    var settings = _viewModel.ContrastSettings;
                    ushort min_val = (ushort)settings.Min_disp_val;
                    ushort max_val = (ushort)settings.Max_disp_val;


                    //byte[] contrast_adj_image = TiffHandler.UpdateContrastAndHistogramValues(
                    //    image,
                    //    min_val,
                    //    max_val,
                    //     _viewModel.ContrastSettings.IsAutoContrastEnabled);

                    if (_bitmap.Size != new Size(width, height))
                        _bitmap = new WriteableBitmap(
                            new Avalonia.PixelSize(width, height),
                            new Vector(96, 96),
                            Avalonia.Platform.PixelFormats.Rgba8888,
                            Avalonia.Platform.AlphaFormat.Premul);

                    //TiffHandler.ReturnRGBABitmapFrom16BitByteArray(image, _bitmap);

                    TiffHandler.UpdateContrastAndReturnBitmap(image, width, height, min_val, max_val, _viewModel.ContrastSettings.IsAutoContrastEnabled,
                        _bitmap);
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


}