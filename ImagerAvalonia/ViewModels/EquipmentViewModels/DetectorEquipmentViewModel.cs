
using Autofac;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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


    [ObservableProperty] private DetectorEquipment _detectorEquipmentProperties;
    public event PropertyChangedEventHandler? WhenDetectorEnabled;

    private readonly ComUtils _comUtils;
    private readonly AcquisitionStateService _acquisitionState;
    private CancellationTokenSource _numericThrottleCts;

    public DetectorEquipmentViewModel(DetectorEquipment detEquipment)
    {
        _comUtils = App.Container.Resolve<ComUtils>();
        _acquisitionState = App.Container.Resolve<AcquisitionStateService>();
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

                    var numeric_property_val = new NumericDetectorPropertyViewModel(numProp.value, numProp.descriptor);
                    numeric_property_val.PropertyChanged += SetChangedValueInModel;
                    Properties.Add(numeric_property_val);
                    break;

                case CategoricDetectorProperty catProp:
                    var cat_property_val = new CategoricDetectorPropertyViewModel(catProp.availableoptions, catProp.descriptor, catProp.current);
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
            foreach (DetectorEquipmentViewModelProperties property in Properties)
            {
                if (property is NumericDetectorPropertyViewModel numeric_property)
                {
                    DetectorEquipmentProperties.SetValueByName(numeric_property.Label, string.Empty, numeric_property.Value);
                }
                if (property is CategoricDetectorPropertyViewModel categoric_property)
                {
                    DetectorEquipmentProperties.SetValueByName(categoric_property.Label, categoric_property.SelectedChoice, 0);
                }
            }
        }
        WhenDetectorEnabled?.Invoke(this, e);
    }

    public async void SetChangedValueInModel(object? sender, PropertyChangedEventArgs e)
    {
        bool waslive = false;

        if (e.PropertyName == nameof(NumericDetectorPropertyViewModel.ThrottledValue) &&
            sender is NumericDetectorPropertyViewModel numericDetectorPropertyViewModel)
        {
            DetectorEquipmentProperties.SetValueByName(
                numericDetectorPropertyViewModel.Label,
                string.Empty,
                numericDetectorPropertyViewModel.ThrottledValue
            );

            if (_acquisitionState.RunningAcquisitionState == RunningAcquisitionState.IsInLive)
            {
                _acquisitionState.InvokeLiveEnd();
                waslive = true;
            }

            await ValidateParameter(numericDetectorPropertyViewModel);

            if (waslive)
            {
                _acquisitionState.InvokeLiveStart();
            }
        }

        if (e.PropertyName == nameof(CategoricDetectorPropertyViewModel.SelectedChoice) &&
            sender is CategoricDetectorPropertyViewModel categoricDetectorPropertyViewModel)
        {
            DetectorEquipmentProperties.SetValueByName(
                categoricDetectorPropertyViewModel.Label,
                categoricDetectorPropertyViewModel.SelectedChoice,
                0
            );
            if (_acquisitionState.RunningAcquisitionState == RunningAcquisitionState.IsInLive)
            {
                _acquisitionState.InvokeLiveEnd();
                waslive = true;
            }
            await ValidateParameter(categoricDetectorPropertyViewModel);
            if (waslive)
            {
                _acquisitionState.InvokeLiveStart();
            }
        }
    }

    private async Task ValidateParameter(DetectorEquipmentViewModelProperties property)
    {
        bool waslive = false;
        if (_acquisitionState.RunningAcquisitionState == RunningAcquisitionState.IsInLive)
        {
            _acquisitionState.InvokeLiveEnd();
            waslive = true;
        }

        var catProperty = DetectorEquipmentProperties.GetPropertyByName(property.Label);
        var wrapper = new
        {
            action = "setdetectorproperty",
            detectorname = Name,
            property = catProperty
        };

        string message = string.Empty;
        await _acquisitionState.CheckIfAcquisitionFinsihed();


        string serialized = JsonConvert.SerializeObject(wrapper, Formatting.None);
        string response;
        _comUtils.SendDataRequest(serialized, "", x => { response = x; }, x => { });
        _comUtils.SendDataRequest(ComUtils.get_detectorproperties(Name), _comUtils.detectorproperties, (Action<string>)(message_response =>
        {
            JToken detector_properties = JObject.Parse(message_response);
            var detProperties = new DetectorEquipment(Name, detector_properties);
            foreach (var prop in Properties)
            {
                prop.PropertyChanged -= SetChangedValueInModel;
            }
            Properties.Clear();
            AssignModelProperties(detProperties.DetectorProperties);

        }), null);
    }


    public override void Dispose()
    {

    }
}

public abstract partial class DetectorEquipmentViewModelProperties : ViewModelBase
{
    [ObservableProperty] private string _label = string.Empty;
    //[ObservableProperty] private bool _isEnabled;
    public override void Dispose()
    {

    }
}



public partial class NumericDetectorPropertyViewModel : DetectorEquipmentViewModelProperties, IDisposable
{
    [ObservableProperty] private double _value;
    private double _throttledValue;
    public double ThrottledValue
    {
        get => _throttledValue;
        private set => SetProperty(ref _throttledValue, value);
    }

    private IDisposable? _throttledSubscription;

    public NumericDetectorPropertyViewModel(double value, string label)
    {
        Value = value;
        Label = label;

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

    public void Dispose()
    {
        _throttledSubscription?.Dispose();
    }
}


public partial class CategoricDetectorPropertyViewModel : DetectorEquipmentViewModelProperties
{
    [ObservableProperty] private ObservableCollection<string> _availableoptions;
    [ObservableProperty] private string _selectedChoice = string.Empty;

    public CategoricDetectorPropertyViewModel(List<string> options, string label, string current)
    {
        Availableoptions = new ObservableCollection<string>(options);

        Label = label;
        if (Availableoptions.Count > 0)
        {
            SelectedChoice = current;
        }
    }
}
