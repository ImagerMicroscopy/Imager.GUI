using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace ImagerAvalonia.ViewModels
{
    public partial class MultiChannelContrastViewModel : ViewModelBase
    {
        [ObservableProperty] private double redValueMin;
        [ObservableProperty] private double redValueMax;

        [ObservableProperty] private double greenValueMin;
        [ObservableProperty] private double greenValueMax;

        [ObservableProperty] private double blueValueMin;
        [ObservableProperty] private double blueValueMax;

        [ObservableProperty] private double yellowValueMin;
        [ObservableProperty] private double yellowValueMax;

        [ObservableProperty] private double transmissionValueMin;
        [ObservableProperty] private double transmissionValueMax;

        public MultiChannelContrastViewModel()
        {
            RedValueMin = 0;
            RedValueMax = 65535;
            GreenValueMin = 0;
            GreenValueMax = 65535;
            BlueValueMin = 0;
            BlueValueMax = 65535;
            YellowValueMin = 0;
            YellowValueMax = 65535;
            TransmissionValueMin = 0;
            TransmissionValueMax = 65535;
        }
    }
}
