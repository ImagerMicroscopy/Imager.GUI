
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System;
using System.Threading.Tasks;
using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Services.Workspace;
using ImagerAvalonia.ViewModels.MeasurementViewModels;


namespace ImagerAvalonia.ViewModels;

public partial class MovableEquipmentViewModel : ViewModelBase
{
    [ObservableProperty] private string _name;
    [ObservableProperty] private ObservableCollection<MovableEquipmentViewModelProperties> _properties = new();
    public MovableComponentModel MovableEquipmentProperties;

    private readonly ImagerWorkspace? _imagerWorkspace;
    private readonly ExperimentManager? _experimentManager;



    public MovableEquipmentViewModel(MovableComponentModel movableEquipment,
        ImagerWorkspace? imagerWorkspace = null,
        ExperimentManager? experimentManager = null)
    {
        _imagerWorkspace = imagerWorkspace;
        _experimentManager = experimentManager;

        Name = movableEquipment.equipmentname;
        MovableEquipmentProperties = movableEquipment;

        foreach (var filterEquipment in movableEquipment.movablecomponentsettings)
        {
            switch (filterEquipment)
            {
                case DiscreteMovableComponentPartProperties discreteComponent:
                    var dis_mov_comp = new DiscreteMovableEquipmentViewModel(discreteComponent.ComponentName,
                        discreteComponent.PossibleSettings, discreteComponent.desiredsetting, discreteComponent);
                    dis_mov_comp.PropertyChanged += SetChangedValueInModel;
                    Properties.Add(dis_mov_comp);
                    break;
                case ContinuousMovableComponentPartProperties continuousComponent:
                    var con_mov_comp = new ContinuousMovableEquipmentViewModel(continuousComponent.ComponentName,
                        continuousComponent.MinValue, continuousComponent.MaxValue, continuousComponent.increment, continuousComponent.desiredsetting, continuousComponent);
                    con_mov_comp.PropertyChanged += SetChangedValueInModel;
                    Properties.Add(con_mov_comp);
                    break;
            }
        }

    }

    private async void SetChangedValueInModel(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DiscreteMovableEquipmentViewModel.SelectedMovableComponent) &&
            sender is DiscreteMovableEquipmentViewModel discrete)
        {
            // The ComboBox pushes null into SelectedItem when its items are
            // replaced; that must not restart the live view.
            if (string.IsNullOrEmpty(discrete.SelectedMovableComponent))
                return;
        }
        else if (e.PropertyName != nameof(ContinuousMovableEquipmentViewModel.SelectedValue))
        {
            return;
        }

        await RestartLiveIfAcquiringAsync();
    }

    // Filter wheel (movable component) settings are part of the detection
    // configuration that was serialized when the live program started, so a
    // change during a live view can only take effect by stopping and
    // restarting the live program. The On...Changed partial methods have
    // already written the new value into the model before this runs.
    private async Task RestartLiveIfAcquiringAsync()
    {
        if (_imagerWorkspace == null || _experimentManager == null)
            return;
        if (_imagerWorkspace.CurrentState != WorkspaceState.Acquiring)
            return;

        await _imagerWorkspace.StopLiveAsync();
        await _imagerWorkspace.StartLiveAsync(_experimentManager.SelectedDetection);
    }

    public override void Dispose()
    {

    }


}

public abstract partial class MovableEquipmentViewModelProperties : ViewModelBase
{
    [ObservableProperty] private string _componentName = String.Empty;
}

public partial class DiscreteMovableEquipmentViewModel : MovableEquipmentViewModelProperties
{
    [ObservableProperty] private ObservableCollection<string> _possibleSettings;
    [ObservableProperty] private string _selectedMovableComponent;
    private DiscreteMovableComponentPartProperties DiscreteMovableComponentPartProperties { get; set; }

    public DiscreteMovableEquipmentViewModel(string name, List<string> possibleSettings, string desiredsetting, DiscreteMovableComponentPartProperties? movableComponent)
    {
        DiscreteMovableComponentPartProperties = movableComponent;
        SelectedMovableComponent = desiredsetting;
        ComponentName = name;
        PossibleSettings = new ObservableCollection<string>(possibleSettings);


    }
    public override void Dispose()
    {

    }
    partial void OnSelectedMovableComponentChanged(string value)
    {
        DiscreteMovableComponentPartProperties.desiredsetting = value; 
    }
}

public partial class ContinuousMovableEquipmentViewModel : MovableEquipmentViewModelProperties
{
    [ObservableProperty] private double _minValue;
    [ObservableProperty] private double _maxValue;
    [ObservableProperty] private double? _selectedValue;
    [ObservableProperty] private double _incrementValue;
    [ObservableProperty] private string _minmaxVal;

    private ContinuousMovableComponentPartProperties ContinuousMovableComponentPartProperties { get; set; }


    public ContinuousMovableEquipmentViewModel(string name, double minValue, double maxValue, double incrementValue, double? desiredsetting, ContinuousMovableComponentPartProperties movableComponent)
    {
        ContinuousMovableComponentPartProperties = movableComponent;
        MinValue = minValue;
        MaxValue = maxValue;
        ComponentName = name;
        IncrementValue = incrementValue;
        SelectedValue = desiredsetting;
        _minmaxVal = $"Min:{minValue} Max:{maxValue}";
    }
    public override void Dispose()
    {

    }

    partial void OnSelectedValueChanged(double? value)
    {
        ContinuousMovableComponentPartProperties.desiredsetting = value;
    }
}





