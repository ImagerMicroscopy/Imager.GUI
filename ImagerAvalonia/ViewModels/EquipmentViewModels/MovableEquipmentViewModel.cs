
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ImagerAvalonia.Services.MeasurementControl;
using System.ComponentModel;
using System;


namespace ImagerAvalonia.ViewModels;

public partial class MovableEquipmentViewModel : ViewModelBase
{
    [ObservableProperty] private string _name;
    [ObservableProperty] private ObservableCollection<MovableEquipmentViewModelProperties> _properties = new();
    [ObservableProperty] private MovableComponent _movableEquipmentProperties;



    public MovableEquipmentViewModel(MovableComponent movableEquipment)
    {
        Name = movableEquipment.equipmentname;
        MovableEquipmentProperties = movableEquipment;

        foreach (var filterEquipment in MovableEquipmentProperties.movablecomponents)
        {
            switch (filterEquipment.movablecomponent)
            {
                case DiscreteMovableComponentPartProperties discreteComponent:
                    var dis_mov_comp = new DiscreteMovableEquipmentViewModel(discreteComponent.ComponentName, discreteComponent.PossibleSettings, discreteComponent.desiredsetting);
                    dis_mov_comp.PropertyChanged += SetChangedValueInModel;
                    Properties.Add(dis_mov_comp);
                    break;
                case ContinuousMovableComponentPartProperties continuousComponent:
                    var con_mov_comp = new ContinuousMovableEquipmentViewModel(continuousComponent.ComponentName, continuousComponent.MinValue, continuousComponent.MaxValue, continuousComponent.increment, continuousComponent.desiredsetting);
                    con_mov_comp.PropertyChanged += SetChangedValueInModel;
                    Properties.Add(con_mov_comp);
                    break;
            }
        }

    }

    private void SetChangedValueInModel(object? sender, PropertyChangedEventArgs e)
    {
        if(sender is ContinuousMovableEquipmentViewModel continuousComponentPropertyViewModel)
        { 
            string component_name = continuousComponentPropertyViewModel.ComponentName; 
            MovableEquipmentProperties.SetValueByName(component_name, continuousComponentPropertyViewModel.SelectedValue.ToString());
        }
        if (sender is DiscreteMovableEquipmentViewModel categoricComponentPropertyViewModel)
        {
            string component_name = categoricComponentPropertyViewModel.ComponentName;
            MovableEquipmentProperties.SetValueByName(component_name, categoricComponentPropertyViewModel.SelectedMovableComponent);

        }
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

    public DiscreteMovableEquipmentViewModel(string name, List<string> possibleSettings, string desiredsetting)
    {
        SelectedMovableComponent = desiredsetting;
        ComponentName = name;
        PossibleSettings = new ObservableCollection<string>(possibleSettings);
 
    }
    public override void Dispose()
    {

    }
}

public partial class ContinuousMovableEquipmentViewModel : MovableEquipmentViewModelProperties
{
    [ObservableProperty] private double _minValue;
    [ObservableProperty] private double _maxValue;
    [ObservableProperty] private double? _selectedValue;
    [ObservableProperty] private double _incrementValue;
    [ObservableProperty] private string _minmaxVal;


    public ContinuousMovableEquipmentViewModel(string name, double minValue, double maxValue, double incrementValue, double? desiredsetting)
    {
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
}





