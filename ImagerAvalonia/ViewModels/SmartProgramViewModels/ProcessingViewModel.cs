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

        /// <summary>
        /// Raised for a SmartProgramViewModel that was created outside of the normal
        /// "+" tab button flow (currently: project-load restoration, see
        /// ExperimentManager.RestoreSmartProgramsAsync) and therefore has no GUI tab
        /// yet. SmartProgramView subscribes to this and creates one, so restored
        /// programs become visible without SmartProgramView needing to be driven
        /// directly by ViewModel-layer code.
        /// </summary>
        public event Action<SmartProgramViewModel>? SmartProgramNeedsTab;

        public SmartProcessingRegisterViewModel(SmartProgramRegistry programRegistry)
        {
            _programRegistry = programRegistry;
        }

        /// <summary>
        /// Announces that vm was created without going through the "+" tab button and
        /// needs a GUI tab created for it. Call after AdoptModel so the tab header
        /// reflects the restored SmartProgramID.
        /// </summary>
        internal void RequestTabFor(SmartProgramViewModel vm)
        {
            SmartProgramNeedsTab?.Invoke(vm);
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

        /// <summary>
        /// Swaps the registry entry backing an already-registered SmartProgramViewModel
        /// from oldModel to newModel, preserving its position. Used when a tab created
        /// via normal DI (which always starts with a fresh SmartProgramModel) needs to
        /// adopt a SmartProgramModel just deserialized from a project load / bundle import,
        /// so the pre-existing SmartProgramID and saved bindings are what actually get used.
        /// </summary>
        internal void ReplaceSmartProgramModel(SmartProgramModel oldModel, SmartProgramModel newModel)
        {
            var index = _programRegistry.DefinedPrograms.IndexOf(oldModel);
            if (index >= 0)
            {
                _programRegistry.DefinedPrograms[index] = newModel;
            }
            else
            {
                _programRegistry.DefinedPrograms.Add(newModel);
            }
        }
    }
}
