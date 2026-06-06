using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImagerAvalonia.Data.Measurements;
using ImagerAvalonia.Services.MeasurementControl;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;




namespace ImagerAvalonia.ViewModels;

public partial class IrradiationPanelViewModel  : MeasurementViewModel {
    [ObservableProperty] public double _IrradiationTimes = 0;
    [ObservableProperty] ObservableCollection<SourcesViewModel> _sourcesViewModels = new();



    public IrradiationPanelViewModel(SystemDefinedSettingsViewModel availableAcquisitions) {
        var sources = availableAcquisitions.Acquisitions.First().AcquisitionSettings.Sources;
        var newsources = sources.Select(x => {
            var src = new Source(x.allowmultiplechannels, x.cancontrolpower, x.AvailableChannels, x.LightSourceName);
            src.EquipmentName = x.EquipmentName;
            return src;
        });

        SourcesViewModels = new ObservableCollection<SourcesViewModel>(newsources.Select(x => new SourcesViewModel(x)));
        var available_sources = SourcesViewModels.Select(x => x.Channels.Where(y => y.IsEnabled).Select(z => z.Name)).SelectMany(x => x).ToList();

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
        SyncIrradiationToState();
        
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null) {
            foreach (SourcesViewModel newSource in e.NewItems) {
                newSource.Channels.CollectionChanged += SourceChannels_CollectionChanged;
            }
        } else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null) {
            foreach (SourcesViewModel oldSource in e.OldItems) {
                oldSource.Channels.CollectionChanged -= SourceChannels_CollectionChanged;
            }
        }
    }

    private void SourceChannels_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        UpdateDisplayedInfo();
        SyncIrradiationToState();
        
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null) {
            foreach (SourcesViewModel.Channel newChannel in e.NewItems) {
                newChannel.PropertyChanged += Channel_PropertyChanged;
            }
        } else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null) {
            foreach (SourcesViewModel.Channel oldChannel in e.OldItems) {
                oldChannel.PropertyChanged -= Channel_PropertyChanged;
            }
        }
    }

    private void Channel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(SourcesViewModel.Channel.IsEnabled) || 
            e.PropertyName == nameof(SourcesViewModel.Channel.PowerLevel)) {
            UpdateDisplayedInfo();
            SyncIrradiationToState();
        }
    }

    private void UpdateDisplayedInfo() {
        var available_sources = SourcesViewModels.Select(x => x.Channels.Where(y => y.IsEnabled).Select(z => z.Name)).SelectMany(x => x).ToList();
        DisplayedInfo = "(" + IrradiationTimes + " s with " + string.Join(',', available_sources) + ")";
    }

    private void SyncIrradiationToState() {
        // Delegate to ExperimentBuilder to update state
        if (ExperimentBuilder != null) {
            var irradiations = new List<IrradiationParams>();
            foreach (var sourceVm in SourcesViewModels) {
                if (sourceVm.Channels == null) continue;
                var enabledChannels = sourceVm.Channels.Where(c => c.IsEnabled).ToList();
                if (enabledChannels.Count == 0) continue;
                irradiations.Add(new IrradiationParams {
                    EquipmentName = sourceVm.EquipmentName,
                    LightSourceName = sourceVm.LightSource.LightSourceName,
                    LightSourceChannels = enabledChannels.Select(c => c.Name).ToList(),
                    Powers = enabledChannels.Select(c => (double)c.PowerLevel).ToList()
                });
            }
            ExperimentBuilder.UpdateIrradiation(Elementid, IrradiationTimes, irradiations);
        }
    }

    [RelayCommand]
    public void OnToggle() {
        UpdateDisplayedInfo();
    }

    partial void OnIrradiationTimesChanged(double value) {
        UpdateDisplayedInfo();
        SyncIrradiationToState();
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

}
