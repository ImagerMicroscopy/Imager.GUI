// ===================== DetectionElementViewModel.cs =====================
using AvaloniaEdit.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace ImagerAvalonia.ViewModels;

public partial class DetectionElementViewModel : MeasurementElementViewModel
{
    [ObservableProperty]
    private ObservableCollection<EnabledAcquisition> _isAquisitionEnabled = new();

    public ObservableCollection<AcquisitionSettingsViewModel> AvailableAcquisitions => UserAcquisitionSettings.Acquisitions;
    public GlobalDefinedSettingsViewModel UserAcquisitionSettings { get; set; }

    public IEnumerable<EnabledAcquisition> EnabledAcquisitions =>
        IsAquisitionEnabled.Where(x => x.IsEnabled);

    public DetectionElementViewModel(GlobalDefinedSettingsViewModel availableAcquisitions) : base()
    {
        UserAcquisitionSettings = availableAcquisitions;
        AvailableAcquisitions.CollectionChanged += AvailableAcquisitions_CollectionChanged;
        Header = "Detection";

        IsAquisitionEnabled = new ObservableCollection<EnabledAcquisition>(
            AvailableAcquisitions.Select(CreateEnabledAcquisition));
        DisplayedInfo = "(" + string.Join(',', EnabledAcquisitions.Select(x => x.Name).Distinct()) + ")";

    }

    private EnabledAcquisition CreateEnabledAcquisition(AcquisitionSettingsViewModel acq)
    {
        var ea = new EnabledAcquisition(true, acq);
        ea.PropertyChanged += EnabledAcquisition_PropertyChanged;
        return ea;
    }

    private void DisposeEnabledAcquisition(EnabledAcquisition ea)
    {
        ea.PropertyChanged -= EnabledAcquisition_PropertyChanged;
        ea.Dispose();
    }

    [RelayCommand]
    public void OnToggle(EnabledAcquisition item)
    {
        UpdateDisplayedInfo();
    }

    private void AvailableAcquisitions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            foreach (var newItem in e.NewItems.OfType<AcquisitionSettingsViewModel>())
                IsAquisitionEnabled.Add(CreateEnabledAcquisition(newItem));
        }

        if (e.Action == NotifyCollectionChangedAction.Remove)
        {
            foreach (var removedItem in e.OldItems.OfType<AcquisitionSettingsViewModel>())
            {
                var enabledAcq = IsAquisitionEnabled.FirstOrDefault(x => x.Acquisition == removedItem);
                if (enabledAcq != null)
                {
                    IsAquisitionEnabled.Remove(enabledAcq);
                    DisposeEnabledAcquisition(enabledAcq);
                }
            }
        }

        UpdateDisplayedInfo();
    }

    private void EnabledAcquisition_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EnabledAcquisition.Name) || e.PropertyName == nameof(EnabledAcquisition.IsEnabled))
            UpdateDisplayedInfo();
    }

    private void UpdateDisplayedInfo()
    {
        DisplayedInfo = "(" + string.Join(',', EnabledAcquisitions.Select(x => x.Name).Distinct()) + ")";
    }

    public override void Dispose()
    {
        foreach (var item in IsAquisitionEnabled)
            DisposeEnabledAcquisition(item);

        AvailableAcquisitions.CollectionChanged -= AvailableAcquisitions_CollectionChanged;
        base.Dispose();
    }

    public override MeasurementElementBase ToModel()
    {
        var detection = new DetectionElement() { ElementId = Elementid.ToString() };

        detection.EnabledDetectionParameters.AddRange(EnabledAcquisitions.Select(x =>
            x.Acquisition?.DetectionSettings).Where(x => x != null).Cast<DefinedDetection>());

        foreach (var enabled_acq in EnabledAcquisitions)
            detection.DetectionNames.Add(enabled_acq.Name);

        foreach (var smartprogrambinding in SmartProgramBindings)
            detection.SmartProgramIds.Add(smartprogrambinding.SmartProgramID.ToString());

        return detection;
    }

    public override void LoadFromModel(MeasurementElementBase model, LoadContext context)
    {
        if (model is not DetectionElement detection)
            throw new ArgumentException($"Expected {nameof(DetectionElement)}", nameof(model));

        base.LoadFromModel(model, context);

        var resolvedAcquisitions = new HashSet<AcquisitionSettingsViewModel>();

        foreach (var savedName in detection.DetectionNames)
        {
            if (context.AcquisitionNameMap.TryGetValue(savedName, out var resolvedVm))
                resolvedAcquisitions.Add(resolvedVm);
            // else: acquisition referenced by this element wasn't in the reconciled batch at all —
            // decide whether that's a data problem worth surfacing (log/throw) or just skip silently.
        }

        foreach (var ea in IsAquisitionEnabled)
            ea.IsEnabled = ea.Acquisition != null && resolvedAcquisitions.Contains(ea.Acquisition);


        UpdateDisplayedInfo();
    }
}

public class EnabledProcessID
{
    public EnabledProcessID(DagProcessingViewModel process, bool isenabled)
    {
        Process = process;
        IsEnabled = isenabled;
    }

    public DagProcessingViewModel Process { get; set; }
    public bool IsEnabled { get; set; }
}

/// <summary>
/// Pairs an AcquisitionSettingsViewModel with an "enabled" flag.
/// Listens for AcquisitionNameChangedMessage instead of subscribing
/// directly to the wrapped VM's PropertyChanged event.
/// </summary>
public partial class EnabledAcquisition : ObservableObject,
    IRecipient<AcquisitionNameChangedMessage>,
    System.IDisposable
{
    [ObservableProperty] private bool _isEnabled;

    public AcquisitionSettingsViewModel? Acquisition { get; }

    public string Name => Acquisition?.Name ?? string.Empty;

    public EnabledAcquisition(bool isEnabled, AcquisitionSettingsViewModel acq)
    {
        IsEnabled = isEnabled;
        Acquisition = acq;

        if (Acquisition != null)
            WeakReferenceMessenger.Default.Register(this);
    }

    public void Receive(AcquisitionNameChangedMessage message)
    {
        if (message.Acquisition == Acquisition)
            OnPropertyChanged(nameof(Name));
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.Unregister<AcquisitionNameChangedMessage>(this);
    }
}