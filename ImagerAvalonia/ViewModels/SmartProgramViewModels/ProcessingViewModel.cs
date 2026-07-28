using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.ImagerModels.SmartProgramModels;
using ImagerAvalonia.Services.Workspace.SmartProgramWorkspace;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;


namespace ImagerAvalonia.ViewModels
{
    public partial class SmartProcessingRegisterViewModel : ViewModelBase  
    {
        [ObservableProperty] ObservableCollection<SmartProgramViewModel> _smartProgramViewModels = new();
        private SmartProgramRegistry _programRegistry;

        public SmartProcessingRegisterViewModel(SmartProgramRegistry programRegistry) 
        {
            _programRegistry = programRegistry;
        }



        internal void AddSmartProgram(SmartProgramViewModel smartProgramViewModel, SmartProgramModel model)
        {
            SmartProgramViewModels.Add(smartProgramViewModel);
            _programRegistry.DefinedPrograms.Add(model);
        }

        internal void RemoveSmartProgram(SmartProgramViewModel smartProgramViewModel, SmartProgramModel model)
        {
            SmartProgramViewModels.Remove(smartProgramViewModel);
            _programRegistry.DefinedPrograms.Remove(model);
        }
    }
}
