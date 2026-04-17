using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace ImagerAvalonia.ViewModels;

public class EnabledAcquisitionTracker
{
    public List<EnabledAcquisition> EnabledAcquisitions = new();

    public EnabledAcquisitionTracker() { }

    public void AddEnabledAcquisition(EnabledAcquisition enabledAcquisition)
    {
        EnabledAcquisitions.Add(enabledAcquisition);
    }

    public void RemoveEnabledAcquisition(EnabledAcquisition enabledAcquisition)
    {
        EnabledAcquisitions.Remove(enabledAcquisition);
    }
}

public partial class AcquisitionPanelViewModel : MeasurementViewModel
{
    [ObservableProperty]
    private ObservableCollection<EnabledAcquisition> _isAquisitionEnabled = new();
    public ObservableCollection<AcquisitionSettingsViewModel> AvailableAcquisitions => UserAcquisitionSettings.Acquisitions;
    public SystemDefinedSettingsViewModel UserAcquisitionSettings { get; set; }
    public EnabledAcquisitionTracker AcquisitionTracker = new();

    public AcquisitionPanelViewModel(SystemDefinedSettingsViewModel availableAcquisitions) : base()
    {
        UserAcquisitionSettings = availableAcquisitions;
        AvailableAcquisitions.CollectionChanged += AvailableAcquisitions_CollectionChanged;




        IsAquisitionEnabled = new ObservableCollection<EnabledAcquisition>(
            AvailableAcquisitions.Select(x =>
            {
                var ea = new EnabledAcquisition(true, x);
                ea.PropertyChanged += EnabledAcquisition_PropertyChanged;
                return ea;
            })
        );
    }


    public void SetAcquisitionTracker(EnabledAcquisitionTracker acquisitionTracker)
    {
        AcquisitionTracker = acquisitionTracker;

        foreach (var item in IsAquisitionEnabled)
        {
            if (item.IsEnabled)
            {
                AcquisitionTracker.AddEnabledAcquisition(item);
            }
        }

        UpdateDisplayedInfo();
    }

    [RelayCommand]
    public void OnToggle(EnabledAcquisition item)
    {
        if (item.IsEnabled)
        {
            AcquisitionTracker.AddEnabledAcquisition(item);
        }
        else
        {
            AcquisitionTracker.RemoveEnabledAcquisition(item);

        }

        UpdateDisplayedInfo();
    }

    private void AvailableAcquisitions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            foreach (var newItem in e.NewItems.OfType<AcquisitionSettingsViewModel>())
            {
                var newEnabledAcq = new EnabledAcquisition(true, newItem);
                newEnabledAcq.PropertyChanged += EnabledAcquisition_PropertyChanged;

                IsAquisitionEnabled.Add(newEnabledAcq);
                AcquisitionTracker.AddEnabledAcquisition(newEnabledAcq);
            }
        }

        if (e.Action == NotifyCollectionChangedAction.Remove)
        {
            foreach (var removedItem in e.OldItems.OfType<AcquisitionSettingsViewModel>())
            {
                var enabledAcq = IsAquisitionEnabled.FirstOrDefault(x => x.acquisition == removedItem);
                if (enabledAcq != null)
                {
                    enabledAcq.PropertyChanged -= EnabledAcquisition_PropertyChanged;

                    IsAquisitionEnabled.Remove(enabledAcq);
                    AcquisitionTracker.RemoveEnabledAcquisition(enabledAcq);
                }
            }
        }

        UpdateDisplayedInfo();
    }

    private void EnabledAcquisition_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EnabledAcquisition.Name))
        {
            UpdateDisplayedInfo();
        }
    }

    private void UpdateDisplayedInfo()
    {
        DisplayedInfo = $"({string.Join(',', IsAquisitionEnabled.Where(x=>x.IsEnabled).Select(x => x.Name).Distinct())})";
    }

    public override void Dispose()
    {
        foreach (var item in IsAquisitionEnabled)
        {
            item.PropertyChanged -= EnabledAcquisition_PropertyChanged;
            AcquisitionTracker.RemoveEnabledAcquisition(item);
        }

        AvailableAcquisitions.CollectionChanged -= AvailableAcquisitions_CollectionChanged;
    }
}

public class EnabledProcessID
{
   
    public EnabledProcessID(DagProcessingViewModel process, bool isenabled)
    {
        this.Process = process;
        this.IsEnabled = isenabled;
    }

    public DagProcessingViewModel Process { get; set; }
    public bool IsEnabled { get; set; }
}

public partial class EnabledAcquisition : ObservableObject
{
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] AcquisitionSettingsViewModel? _acquisitionSettings;

    public AcquisitionSettingsViewModel? acquisition { get; }




    public string Name => acquisition?.Name ?? string.Empty;

    public EnabledAcquisition(bool isEnabled, AcquisitionSettingsViewModel acq)
    {
        IsEnabled = isEnabled;
        acquisition = acq;

        // Forward PropertyChanged from Acquisition.Name to EnabledAcquisition.Name
        if (acquisition != null)
        {
            acquisition.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AcquisitionSettingsViewModel.Name))
                {
                    OnPropertyChanged(nameof(Name));
                }
            };
        }
    }
}
