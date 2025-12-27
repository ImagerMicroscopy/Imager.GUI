using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImagerAvalonia.Services;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;


namespace ImagerAvalonia.ViewModels
{
    public partial class SmartProcessingRegisterViewModel : ViewModelBase  
    {
        [ObservableProperty] ObservableCollection<SmartProgramViewModel> _smartProgramViewModels = new();



        public SmartProcessingRegisterViewModel() 
        {
        }



        public JArray SerializeAllDags()
        {
            JArray serializedDags = new JArray();
            foreach(var smartprogram in SmartProgramViewModels)
            {
                JObject dag_definitions = new JObject();

                dag_definitions.TryAdd("SmartProgramID", smartprogram.SmartProgramID.ToString());
                dag_definitions.TryAdd("SmartProgramDefinition", smartprogram.SerializeProgram());
                serializedDags.Add(dag_definitions);
            }
            return serializedDags;
        }
    }
}
