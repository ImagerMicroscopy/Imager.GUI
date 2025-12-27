using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;
using System;
using System.IO;

namespace ImagerAvalonia.Views;

public partial class MultiImageCanvas : UserControl
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



    public MultiImageCanvas()
    {
        InitializeComponent();

  

        _image = this.FindControl<Image>("DisplayedImage")!;
        _canvas = this.FindControl<Canvas>("DisplayedCanvas")!;
    }





    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }


    internal unsafe void UpdateMultiBitmap(
        byte[] red16,
        byte[] green16,
        byte[] blue16,
        byte[] yellow16,
        byte[] transmission16,
        int imwidth,
        int imheight,
        MultiChannelContrastViewModel contrasVM)
    {
        if (_bitmap.PixelSize.Width != imwidth || _bitmap.PixelSize.Height != imheight)
        {
            _bitmap = new WriteableBitmap(
                new Avalonia.PixelSize(imwidth, imheight),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormats.Rgba8888,
                Avalonia.Platform.AlphaFormat.Premul);
        }

        int pixelCount = imwidth * imheight;


        using (var fb = _bitmap.Lock())
        {
            byte* dst = (byte*)fb.Address.ToPointer();

            fixed (byte* rPtr = red16)
            fixed (byte* gPtr = green16)
            fixed (byte* bPtr = blue16)
            fixed (byte* yPtr = yellow16)
            fixed (byte* tPtr = transmission16)
            {
                ushort* rSrc = (ushort*)rPtr;
                ushort* gSrc = (ushort*)gPtr;
                ushort* bSrc = (ushort*)bPtr;
                ushort* ySrc = (ushort*)yPtr;
                ushort* tSrc = (ushort*)tPtr;

                for (int i = 0; i < pixelCount; i++)
                {
                    // Load raw 16-bit values
                    ushort r16 = red16 != null ? rSrc[i] : (ushort)0;
                    ushort g16 = green16 != null ? gSrc[i] : (ushort)0;
                    ushort b16 = blue16 != null ? bSrc[i] : (ushort)0;
                    ushort y16 = yellow16 != null ? ySrc[i] : (ushort)0;
                    ushort tr16 = transmission16 != null ? tSrc[i] : (ushort)0;

                    // Clamp to UI-selected min/max
                    r16 = (ushort)Math.Clamp(r16, contrasVM.RedValueMin, contrasVM.RedValueMax);
                    g16 = (ushort)Math.Clamp(g16, contrasVM.GreenValueMin, contrasVM.GreenValueMax);
                    b16 = (ushort)Math.Clamp(b16, contrasVM.BlueValueMin, contrasVM.BlueValueMax);
                    y16 = (ushort)Math.Clamp(y16, contrasVM.YellowValueMin, contrasVM.YellowValueMax);
                    tr16 = (ushort)Math.Clamp(tr16, contrasVM.TransmissionValueMin, contrasVM.TransmissionValueMax);

                    // Normalize each channel to 0–255
                    byte r = NormalizeToByte(r16, contrasVM.RedValueMin, contrasVM.RedValueMax);
                    byte g = NormalizeToByte(g16, contrasVM.GreenValueMin, contrasVM.GreenValueMax);
                    byte b = NormalizeToByte(b16, contrasVM.BlueValueMin, contrasVM.BlueValueMax);
                    byte y = NormalizeToByte(y16, contrasVM.YellowValueMin, contrasVM.YellowValueMax);
                    byte tr = NormalizeToByte(tr16, contrasVM.TransmissionValueMin, contrasVM.TransmissionValueMax);

                    // Blend yellow
                    if (yellow16 != null)
                    {
                        int rr = r + (y >> 1);
                        int gg = g + (y >> 1);
                        if (rr > 255) rr = 255;
                        if (gg > 255) gg = 255;
                        r = (byte)rr;
                        g = (byte)gg;
                    }

                    // Add transmission
                    if (transmission16 != null)
                    {
                        int rr = r + tr;
                        int gg = g + tr;
                        int bb = b + tr;
                        if (rr > 255) rr = 255;
                        if (gg > 255) gg = 255;
                        if (bb > 255) bb = 255;
                        r = (byte)rr;
                        g = (byte)gg;
                        b = (byte)bb;
                    }

                    int offset = i * 4;
                    dst[offset + 0] = r;   // R
                    dst[offset + 1] = g;   // G
                    dst[offset + 2] = b;   // B
                    dst[offset + 3] = 255; // A
                }

            }
        }
    }
    private static byte NormalizeToByte(double value, double min, double max)
    {
        if (max <= min) return 0; // avoid divide-by-zero, treat as black
        double norm = (value - min) / (max - min);
        if (norm < 0) norm = 0;
        if (norm > 1) norm = 1;
        return (byte)(norm * 255.0);
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