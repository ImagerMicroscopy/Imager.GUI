using Autofac;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;

namespace ImagerAvalonia.ViewModels;

public partial class MeasurementViewModel: ViewModelBase
{
    [ObservableProperty] string _DisplayedInfo = string.Empty;
    [ObservableProperty] Guid _elementid = Guid.NewGuid();
    [ObservableProperty] ObservableCollection<SmartProgramViewModel> _smartPrograms = new();
    [ObservableProperty] SmartProgramViewModel? _selectedProgramId = null;
    [ObservableProperty] bool _fromProgramId = false;

    public MeasurementViewModel() 
    {
        SmartPrograms = App.Container.Resolve<SmartProcessingRegisterViewModel>().SmartProgramViewModels;

    }
}
