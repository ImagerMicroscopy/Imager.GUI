
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImagerAvalonia.Services.MeasurementControl;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;




namespace ImagerAvalonia.ViewModels;

public partial class IrradiationPanelViewModel  : MeasurementViewModel
{
    [ObservableProperty] public double _IrradiationTimes = 0;
    [ObservableProperty] ObservableCollection<SourcesViewModel> _sourcesViewModels = new();



    public IrradiationPanelViewModel(SystemDefinedSettingsViewModel availableAcquisitions) 
    {
        var sources = availableAcquisitions.Acquisitions.First().AcquisitionSettings.Sources;
        var newsources = sources.Select(x => {
            var src = new Source(x.allowmultiplechannels, x.cancontrolpower, x.AvailableChannels, x.LightSourceName);
            src.EquipmentName = x.EquipmentName;
            return src;
        }
        );

        SourcesViewModels = new ObservableCollection<SourcesViewModel>(newsources.Select(x => new SourcesViewModel(x)));
        var available_sources = SourcesViewModels.Select(x => x.Channels.Where(y => y.IsEnabled).Select(z => z.Name)).SelectMany(x => x).ToList();

        DisplayedInfo = $"({IrradiationTimes} s with {string.Join(',', available_sources)})";

    }


    [RelayCommand]
    public void OnToggle()
    {
        var available_sources = SourcesViewModels.Select(x => x.Channels.Where(y => y.IsEnabled).Select(z => z.Name)).SelectMany(x => x).ToList();

        DisplayedInfo = $"({IrradiationTimes}s with {string.Join(',', available_sources)})";
    }

    partial void OnIrradiationTimesChanged(double value)
    {
        var available_sources = SourcesViewModels.Select(x => x.Channels.Where(y => y.IsEnabled).Select(z => z.Name)).SelectMany(x => x).ToList();

        DisplayedInfo = $"({IrradiationTimes}s with {string.Join(',', available_sources)})";
    }


}

