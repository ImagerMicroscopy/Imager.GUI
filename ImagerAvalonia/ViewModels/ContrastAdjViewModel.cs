using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Views.ViewUtils;
using System;




namespace ImagerAvalonia.ViewModels;





public partial class ContrastAdjViewModel : ViewModelBase
{



    public event Action<double[], double[], double[]>? OnHistogramUpdateRequested;
    public event EventHandler? OnContrastValsChanged;
    [ObservableProperty]
    public int _Min_disp_val = 0;
    [ObservableProperty]
    public int _Max_disp_val = ushort.MaxValue;
    [ObservableProperty]
    public bool _IsAutoContrastEnabled = true;
    private System.Timers.Timer _throttleTimer = new System.Timers.Timer(150) { AutoReset = true };
    private bool _canFire = true;


    public ContrastAdjViewModel() 
    {
        _throttleTimer.Elapsed += (_, _) => _canFire = true;
    }

    partial void OnMax_disp_valChanged(int oldValue, int newValue)
    {
        if (_canFire)
        {
            _canFire = false;
            _throttleTimer.Start();
            OnContrastValsChanged?.Invoke(this, new EventArgs());
        }
    }

    partial void OnMin_disp_valChanged(int oldValue, int newValue)
    {
        if (_canFire)
        {
            _canFire = false;
            _throttleTimer.Start();
            OnContrastValsChanged?.Invoke(this, new EventArgs());
        }
    }

    partial void OnIsAutoContrastEnabledChanged(bool oldValue, bool newValue)
    {
        OnContrastValsChanged?.Invoke(this, new EventArgs());
    }



    public override void Dispose()
    {

    }

    internal void UpdateHistogram(byte[] imagedata)
    {
        double[] histogram_vals_y = new double[257];
        double[] histogram_vals_y_low = new double[257];
        double[] histogram_vals_x = new double[257];
        TiffHandler.UpdateHistogramValues(imagedata, ref histogram_vals_y, ref histogram_vals_y_low, ref histogram_vals_x );
        OnHistogramUpdateRequested?.Invoke(histogram_vals_x, histogram_vals_y_low, histogram_vals_y);
    }
}



