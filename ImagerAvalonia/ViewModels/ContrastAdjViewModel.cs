using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Utils;
using ImagerAvalonia.Views.ViewUtils;
using System;



namespace ImagerAvalonia.ViewModels;



public partial class ContrastAdjViewModel : ViewModelBase
{


    /// <summary>Minimum interval between contrast-change notifications.</summary>
    private const int ThrottleIntervalMs = 50;

    public event Action<double[], double[], double[]>? OnHistogramUpdateRequested;
    public event EventHandler? OnContrastValsChanged;
    [ObservableProperty]
    public int _Min_disp_val = 0;
    [ObservableProperty]
    public int _Max_disp_val = ushort.MaxValue;
    [ObservableProperty]
    public bool _IsAutoContrastEnabled = true;

    /// <summary>
    /// Upper bound of the contrast controls: Mono8 limits are raw byte values
    /// (0..255), all other formats live on the 16-bit display scale (0..65535).
    /// </summary>
    [ObservableProperty]
    public int _SliderMaximum = ushort.MaxValue;

    private readonly object _throttleLock = new();
    private System.Timers.Timer? _throttleTimer;
    private bool _windowOpen;
    private bool _pendingFire;



    public ContrastAdjViewModel()
    {
        _throttleTimer = new System.Timers.Timer(ThrottleIntervalMs) { AutoReset = false };
        _throttleTimer.Elapsed += (_, _) =>
        {
            bool fire;
            lock (_throttleLock)
            {
                _windowOpen = false;
                fire = _pendingFire;
                _pendingFire = false;
            }

            // Trailing-edge fire: coalesced changes during the window raise one
            // final notification on the UI thread, so the last slider position
            // is always applied.
            if (fire)
            {
                Dispatcher.UIThread.Post(() => OnContrastValsChanged?.Invoke(this, new EventArgs()));
            }
        };
    }

    partial void OnMax_disp_valChanged(int oldValue, int newValue) => RequestContrastUpdate();

    partial void OnMin_disp_valChanged(int oldValue, int newValue) => RequestContrastUpdate();

    partial void OnIsAutoContrastEnabledChanged(bool oldValue, bool newValue) => RequestContrastUpdate();

    /// <summary>
    /// Raises the contrast-change notification at most once per throttle
    /// interval. The first change fires immediately; changes arriving while
    /// the window is open are coalesced into a single trailing fire.
    /// </summary>
    private void RequestContrastUpdate()
    {
        bool startWindow = false;
        lock (_throttleLock)
        {
            if (_windowOpen)
            {
                _pendingFire = true;
                return;
            }

            if (_throttleTimer != null)
            {
                _windowOpen = true;
                startWindow = true;
            }
        }

        if (startWindow)
        {
            _throttleTimer!.Start();
        }

        OnContrastValsChanged?.Invoke(this, new EventArgs());
    }

    /// <summary>
    /// Adapts the contrast control bounds to the pixel format and clamps
    /// stored limits into the new range. Must be called on the UI thread (the
    /// bound values feed Avalonia bindings).
    /// </summary>
    public void SetPixelFormat(ImagePixelFormat format)
    {
        SliderMaximum = format == ImagePixelFormat.Mono8 ? byte.MaxValue : ushort.MaxValue;

        if (Min_disp_val > SliderMaximum)
            Min_disp_val = SliderMaximum;
        if (Max_disp_val > SliderMaximum)
            Max_disp_val = SliderMaximum;
    }

    public override void Dispose()
    {
        if (_throttleTimer == null)
            return;

        _throttleTimer.Stop();
        _throttleTimer.Dispose();
        _throttleTimer = null;
    }

    internal void UpdateHistogram(byte[] imagedata, ImagePixelFormat format)
    {
        // Callers run on a background thread (image channel consumers), so the
        // format-dependent control bounds are marshaled to the UI thread.
        Dispatcher.UIThread.Post(() => SetPixelFormat(format));

        double[] histogram_vals_y = new double[257];
        double[] histogram_vals_y_low = new double[257];
        double[] histogram_vals_x = new double[257];
        TiffHandler.UpdateHistogramValues(imagedata, format, ref histogram_vals_y, ref histogram_vals_y_low, ref histogram_vals_x );
        OnHistogramUpdateRequested?.Invoke(histogram_vals_x, histogram_vals_y_low, histogram_vals_y);
    }
}
