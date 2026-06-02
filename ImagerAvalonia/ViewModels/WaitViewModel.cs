using CommunityToolkit.Mvvm.ComponentModel;


namespace ImagerAvalonia.ViewModels;

public partial class WaitViewModel : MeasurementViewModel
{



    [ObservableProperty]
    public double _WaitPeriod;



    public WaitViewModel()
    {
        DisplayedInfo = $"({WaitPeriod} seconds)";
    }
    public override void Dispose()
    {

    }

    partial void OnWaitPeriodChanged(double value)
    {
        DisplayedInfo = $"({WaitPeriod} seconds)";
        
        // Delegate to ExperimentBuilder to update state
        ExperimentBuilder?.UpdateWaitDuration(Elementid, WaitPeriod);
    }
}

