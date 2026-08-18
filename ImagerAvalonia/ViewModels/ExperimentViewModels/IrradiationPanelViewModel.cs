using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;




namespace ImagerAvalonia.ViewModels;

public partial class IrradiationPanelViewModel  : MeasurementElementViewModel {
    [ObservableProperty] public double _IrradiationTimes = 0;
    [ObservableProperty] ObservableCollection<SourcesEquipmentViewModel> _sourcesViewModels = new();

    public IrradiationPanelViewModel(GlobalDefinedSettingsViewModel availableAcquisitions) {
        var sources = availableAcquisitions.Acquisitions.First().DetectionSettings.Settings.Irradiation;
        var newsources = sources.Select(x => {
            var src = new Source(x.allowmultiplechannels, x.cancontrolpower, x.AvailableChannels, x.LightSourceName);
            src.EquipmentName = x.EquipmentName;
            return src;
        });

        SourcesViewModels = new ObservableCollection<SourcesEquipmentViewModel>(newsources.Select(x => new SourcesEquipmentViewModel(x)));
        var available_sources = SourcesViewModels.Select(x => x.Channels.Where(y => y.IsEnabled).Select(z => z.Name)).SelectMany(x => x).ToList();
        Header = "Irradiation";

        DisplayedInfo = "(" + IrradiationTimes + " s with " + string.Join(',', available_sources) + ")";
        
        // Subscribe to collection changes in SourcesViewModels
        SourcesViewModels.CollectionChanged += SourcesViewModels_CollectionChanged;
        
        // Subscribe to each source's channel changes
        foreach (var sourceVm in SourcesViewModels) {
            sourceVm.Channels.CollectionChanged += SourceChannels_CollectionChanged;
        }
    }

    private void SourcesViewModels_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        UpdateDisplayedInfo();
        
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null) {
            foreach (SourcesEquipmentViewModel newSource in e.NewItems) {
                newSource.Channels.CollectionChanged += SourceChannels_CollectionChanged;
            }
        } else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null) {
            foreach (SourcesEquipmentViewModel oldSource in e.OldItems) {
                oldSource.Channels.CollectionChanged -= SourceChannels_CollectionChanged;
            }
        }
    }

    private void SourceChannels_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        UpdateDisplayedInfo();
        
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null) {
            foreach (SourcesEquipmentViewModel.Channel newChannel in e.NewItems) {
                newChannel.PropertyChanged += Channel_PropertyChanged;
            }
        } else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null) {
            foreach (SourcesEquipmentViewModel.Channel oldChannel in e.OldItems) {
                oldChannel.PropertyChanged -= Channel_PropertyChanged;
            }
        }
    }

    private void Channel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(SourcesEquipmentViewModel.Channel.IsEnabled) || 
            e.PropertyName == nameof(SourcesEquipmentViewModel.Channel.PowerLevel)) {
            UpdateDisplayedInfo();
        }
    }

    private void UpdateDisplayedInfo() {
        var available_sources = SourcesViewModels.Select(x => x.Channels.Where(y => y.IsEnabled).Select(z => z.Name)).SelectMany(x => x).ToList();
        DisplayedInfo = "(" + IrradiationTimes + " s with " + string.Join(',', available_sources) + ")";
    }

    [RelayCommand]
    public void OnToggle() {
        UpdateDisplayedInfo();
    }

    partial void OnIrradiationTimesChanged(double value) {
        UpdateDisplayedInfo();
    }

    public override void Dispose() {
        SourcesViewModels.CollectionChanged -= SourcesViewModels_CollectionChanged;
        foreach (var sourceVm in SourcesViewModels) {
            sourceVm.Channels.CollectionChanged -= SourceChannels_CollectionChanged;
            foreach (var channel in sourceVm.Channels) {
                channel.PropertyChanged -= Channel_PropertyChanged;
            }
        }
    }

    public override MeasurementElementBase ToModel()
    {
        var irradiation = new IrradiationElement()
        {
            ElementId = Elementid.ToString(),
            Duration = IrradiationTimes
        };

        foreach(var src_vm in SourcesViewModels)
        {
            var irr_config = new IrradiationConfig()
            {
                EquipmentName = src_vm.EquipmentName,
                LightSourceName = src_vm.LightSource.LightSourceName,
                LightSourceChannel = src_vm.Channels.Where(x => x.IsEnabled).Select(y => y.Name).ToList(),
                LightSourcePower = src_vm.Channels.Where(x => x.IsEnabled).Select(y => (double)y.PowerLevel).ToList()
            };
            irradiation.Irradiation.Add(irr_config);
        }

        return irradiation;
    }

    public override void LoadFromModel(MeasurementElementBase measurementElement, LoadContext context)
    {
        var model = (IrradiationElement)measurementElement;

        IrradiationTimes = model.Duration;

        if (Guid.TryParse(model.ElementId, out var parsedId))
        {
            Elementid = parsedId;
        }

        foreach (var irr_config in model.Irradiation)
        {
            var sourceVm = SourcesViewModels.FirstOrDefault(x =>
                x.EquipmentName == irr_config.EquipmentName &&
                x.LightSource.LightSourceName == irr_config.LightSourceName);

            if (sourceVm == null)
                continue;

            for (int i = 0; i < irr_config.LightSourceChannel.Count; i++)
            {
                var channelName = irr_config.LightSourceChannel[i];
                var power = i < irr_config.LightSourcePower.Count ? irr_config.LightSourcePower[i] : 0d;

                var channel = sourceVm.Channels.FirstOrDefault(c => c.Name == channelName);
                if (channel == null)
                    continue;

                channel.IsEnabled = true;
                channel.PowerLevel = (int)power;
            }
        }

        UpdateDisplayedInfo();
    }
}
