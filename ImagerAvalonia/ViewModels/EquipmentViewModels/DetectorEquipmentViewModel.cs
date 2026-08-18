
using Autofac;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Services.Workspace;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;


namespace ImagerAvalonia.ViewModels;

public partial class DetectorEquipmentViewModel : ViewModelBase
{
    [ObservableProperty] private string _name;
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private ObservableCollection<DetectorEquipmentViewModelProperties> _properties = new();


    [ObservableProperty] private DetectorEquipmentModel _detectorEquipmentProperties;
    public event PropertyChangedEventHandler? WhenDetectorEnabled;

    private readonly IImagerCommunicationManager _communicationManager;
    private readonly ImagerWorkspace _imagerWorkspace;
    private readonly ExperimentManager _experimentManager;
    private CancellationTokenSource? _numericThrottleCts;

    public DetectorEquipmentViewModel(DetectorEquipmentModel detEquipment, ImagerWorkspace imagerWorkspace, ExperimentManager experimentManager)
    {
        _communicationManager = ImagerCommunicationManager.Instance;
        _imagerWorkspace = imagerWorkspace;
        _experimentManager = experimentManager;
        Name = detEquipment.Detectorname;
        DetectorEquipmentProperties = detEquipment;
        IsEnabled = detEquipment.IsEnabled;
        AssignModelProperties(detEquipment.DetectorProperties);

        this.PropertyChanged += IsEnabled_PropertyChanged;
    }

    private void AssignModelProperties(List<DetectorEquipmentProperties> detEquipmentProperties)
    {
        foreach (DetectorEquipmentProperties detectorEqProperty in detEquipmentProperties)
        {
            switch (detectorEqProperty)
            {
                case NumericDetectorProperty numProp:

                    var numeric_property_val = new NumericDetectorPropertyViewModel(numProp);
                    numeric_property_val.PropertyChanged += SetChangedValueInModel;
                    Properties.Add(numeric_property_val);
                    break;

                case CategoricDetectorProperty catProp:
                    var cat_property_val = new CategoricDetectorPropertyViewModel(catProp);
                    cat_property_val.PropertyChanged += SetChangedValueInModel;
                    Properties.Add(cat_property_val);
                    break;
            }
        }
    }



    private void IsEnabled_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsEnabled))
        {
            DetectorEquipmentProperties.IsEnabled = IsEnabled;
        }
        WhenDetectorEnabled?.Invoke(this, e);
    }


    public async void SetChangedValueInModel(object? sender, PropertyChangedEventArgs e)
    {
        bool waslive = false;

        if (e.PropertyName == nameof(NumericDetectorPropertyViewModel.ThrottledValue) &&
            sender is NumericDetectorPropertyViewModel numericDetectorPropertyViewModel)
        {
            if(_imagerWorkspace.CurrentState == WorkspaceState.Acquiring)
            {
                await _imagerWorkspace.StopLiveAsync();
                waslive = true;
            }

            await ValidateParameter(numericDetectorPropertyViewModel);

            if (waslive)
            {
                await _imagerWorkspace.StartLiveAsync(_experimentManager.SelectedDetection);
            }
        }

        if (e.PropertyName == nameof(CategoricDetectorPropertyViewModel.SelectedChoice) &&
            sender is CategoricDetectorPropertyViewModel categoricDetectorPropertyViewModel)
        {
            if (_imagerWorkspace.CurrentState == WorkspaceState.Acquiring)
            {
                await _imagerWorkspace.StopLiveAsync();
                waslive = true;
            }

            await ValidateParameter(categoricDetectorPropertyViewModel);

            if (waslive)
            {
                await _imagerWorkspace.StartLiveAsync(_experimentManager.SelectedDetection);
            }
        }
    }

    private async Task ValidateParameter(DetectorEquipmentViewModelProperties property)
    {

        await _communicationManager.SetDetectorPropertyAsync(Name, property.Property);

        var detectors = await _communicationManager.ListAvailableDetectorsAsync();
        var detProperties = detectors.FirstOrDefault(x => x.Detectorname == Name);

        if (detProperties != null)
        {
            foreach (var newProperty in detProperties.DetectorProperties)
            {
                var existingVm = Properties.FirstOrDefault(x =>
                    x.Property.propertycode == newProperty.propertycode);

                if (existingVm == null)
                    continue;

                switch (existingVm)
                {
                    case NumericDetectorPropertyViewModel numericVm
                        when newProperty is NumericDetectorProperty newNum:

                        var existingNum = (NumericDetectorProperty)numericVm.Property;

                        existingNum.descriptor = newNum.descriptor;
                        existingNum.propertycode = newNum.propertycode;
                        existingNum.value = newNum.value;

                        numericVm.RefreshFromModel();
                        break;

                    case CategoricDetectorPropertyViewModel categoricVm
                        when newProperty is CategoricDetectorProperty newCat:

                        var existingCat = (CategoricDetectorProperty)categoricVm.Property;

                        existingCat.descriptor = newCat.descriptor;
                        existingCat.propertycode = newCat.propertycode;
                        existingCat.current = newCat.current;

                        existingCat.availableoptions.Clear();
                        existingCat.availableoptions.AddRange(newCat.availableoptions);

                        categoricVm.RefreshFromModel();
                        break;
                }
            }
        }

        //if (waslive)
        //{
        //    _acquisitionState.InvokeLiveStart();
        //}
    }

    public override void Dispose()
    {

    }
}
public abstract partial class DetectorEquipmentViewModelProperties : ViewModelBase
{
    [ObservableProperty] private string _label = string.Empty;
    public virtual DetectorEquipmentProperties Property { get; set; }
    //[ObservableProperty] private bool _isEnabled;
    public override void Dispose()
    {

    }
}


public partial class NumericDetectorPropertyViewModel : DetectorEquipmentViewModelProperties, IDisposable
{
    [ObservableProperty] private double _value;
    [ObservableProperty] double _throttledValue;

    private IDisposable? _throttledSubscription;
    public override DetectorEquipmentProperties Property { get; set; }

    public NumericDetectorPropertyViewModel(NumericDetectorProperty numProp)
    {
        this.Property = numProp;

        Value = numProp.value;
        Label = numProp.descriptor;

        IScheduler scheduler;
        if (Avalonia.Threading.Dispatcher.UIThread != null)
        {
            scheduler = AvaloniaScheduler.Instance;
        }
        else
        {
            scheduler = Scheduler.Default; // When unit testing, the UIThread does not exist. This avoids the deadlock.
        }

        _throttledSubscription = Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                h => PropertyChanged += h,
                h => PropertyChanged -= h)
            .Where(e => e.EventArgs.PropertyName == nameof(Value))
            .Select(_ => Value)
            .Throttle(TimeSpan.FromMilliseconds(1000))
            .DistinctUntilChanged()
            .ObserveOn(scheduler)
            .Subscribe(v => ThrottledValue = v);
    }

    public void RefreshFromModel()
    {
        if (Property is NumericDetectorProperty num)
        {
            if (Label != num.descriptor)
                Label = num.descriptor;

            if (Value != num.value)
                Value = num.value;
        }
    }

    partial void OnThrottledValueChanged(double value)
    {
        if(Property is NumericDetectorProperty numProp)
        {
            numProp.value = value;
        }
    }

    public override void Dispose()
    {
        _throttledSubscription?.Dispose();
    }
}

public partial class CategoricDetectorPropertyViewModel : DetectorEquipmentViewModelProperties
{
    [ObservableProperty] private ObservableCollection<string> _availableoptions;
    [ObservableProperty] private string _selectedChoice = string.Empty;
    public override DetectorEquipmentProperties Property { get; set; }

    public CategoricDetectorPropertyViewModel(CategoricDetectorProperty catProp)
    {
        this.Property = catProp;

        Availableoptions = new ObservableCollection<string>(catProp.availableoptions);

        Label = catProp.descriptor;
        if (Availableoptions.Count > 0)
        {
            SelectedChoice = catProp.current;
        }
    }

    partial void OnSelectedChoiceChanged(string value)
    {
        if(Property is CategoricDetectorProperty catProp)
        {
            catProp.current = value;
        }
    }

    public void RefreshFromModel()
    {
        if (Property is CategoricDetectorProperty cat)
        {
            if (Label != cat.descriptor)
                Label = cat.descriptor;

            Availableoptions.Clear();
            foreach (var option in cat.availableoptions)
                Availableoptions.Add(option);

            if (SelectedChoice != cat.current)
                SelectedChoice = cat.current;
        }
    }
}
