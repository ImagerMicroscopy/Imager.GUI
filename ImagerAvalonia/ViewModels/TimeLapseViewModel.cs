using CommunityToolkit.Mvvm.ComponentModel;


namespace ImagerAvalonia.ViewModels;

public partial class TimeLapseViewModel : MeasurementViewModel
{



    [ObservableProperty]
    private decimal? _timeDelta = 0.001m;

    [ObservableProperty]
    private double? _nTimes = 1;

    // Ensure num_frames always returns a valid int based on NTimes
    public int num_frames => (int)(NTimes ?? 1);

    // Constructor
    public TimeLapseViewModel()
    {
        DisplayedInfo = $"({NTimes} times \u0394t = {TimeDelta}s)";
        PropertyChanged += TimeLapseViewModel_PropertyChanged;
    }

    private void TimeLapseViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        DisplayedInfo = $"({NTimes} times \u0394t = {TimeDelta}s)";
    }


    // Called automatically when TimeDelta changes
    partial void OnTimeDeltaChanged(decimal? value)
    {
        if (value == null || value < 0.001m)
            TimeDelta = 0.001m;
        else
        {
            // Delegate to ExperimentBuilder to update state
            ExperimentBuilder?.UpdateTimeLapse(
                Elementid,
                (int)(NTimes ?? 1),
                (double)(TimeDelta ?? 0.001m));
        }
    }

    // Called automatically when NTimes changes
    partial void OnNTimesChanged(double? value)
    {
        if (value == null || value < 1)
            NTimes = 1;
        else
        {
            // Delegate to ExperimentBuilder to update state
            ExperimentBuilder?.UpdateTimeLapse(
                Elementid,
                (int)(NTimes ?? 1),
                (double)(TimeDelta ?? 0.001m));
        }
    }
    public override void Dispose()
    {

    }

}

