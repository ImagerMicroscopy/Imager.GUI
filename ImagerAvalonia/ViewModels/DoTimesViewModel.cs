
using CommunityToolkit.Mvvm.ComponentModel;


namespace ImagerAvalonia.ViewModels;

public partial class DoTimesViewModel : MeasurementViewModel
{


    [ObservableProperty] int _NumRepeats= 0 ;

    public int num_frames { get { return NumRepeats; } set { NumRepeats = value; } }

    public DoTimesViewModel()
    {
        DisplayedInfo = $"(0 times)";
    }

    public override void Dispose()
    {

    }

    partial void OnNumRepeatsChanged(int value)
    {
        DisplayedInfo = $"({NumRepeats} times)";
    }

}

