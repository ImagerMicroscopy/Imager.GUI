
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System;
using ImagerAvalonia.Services.ImagerModels.EquipmentModels;


namespace ImagerAvalonia.ViewModels;

public partial class MovableEquipmentViewModel : ViewModelBase
{
    [ObservableProperty] private string _name;
    [ObservableProperty] private ObservableCollection<MovableEquipmentViewModelProperties> _properties = new();
    public MovableComponentModel MovableEquipmentProperties;



    public MovableEquipmentViewModel(MovableComponentModel movableEquipment)
    {
        Name = movableEquipment.equipmentname;
        MovableEquipmentProperties = movableEquipment;

        foreach (var filterEquipment in movableEquipment.movablecomponentsettings)
        {
            switch (filterEquipment)
            {
                case DiscreteMovableComponentPartProperties discreteComponent:
                    var dis_mov_comp = new DiscreteMovableEquipmentViewModel(discreteComponent.ComponentName,
                        discreteComponent.PossibleSettings, discreteComponent.desiredsetting, discreteComponent);
                    //dis_mov_comp.PropertyChanged += SetChangedValueInModel;
                    Properties.Add(dis_mov_comp);
                    break;
                case ContinuousMovableComponentPartProperties continuousComponent:
                    var con_mov_comp = new ContinuousMovableEquipmentViewModel(continuousComponent.ComponentName,
                        continuousComponent.MinValue, continuousComponent.MaxValue, continuousComponent.increment, continuousComponent.desiredsetting, continuousComponent);
                    //con_mov_comp.PropertyChanged += SetChangedValueInModel;
                    Properties.Add(con_mov_comp);
                    break;
            }
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





