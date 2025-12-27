using Autofac;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Exceptions;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagerAvalonia.ViewModels
{
    public partial class SmartProgramViewModel : ViewModelBase
    {

        public event EventHandler? OnOpenFolderRequested;
        public event EventHandler? OnReloadRequested;

        public event EventHandler<string> OnSelectedProgramChangedEvent;
        public string LoadedFolder = string.Empty;
        private readonly IPythonComService _nodeComService;
        private readonly EquipmentState _equipmentState;
        private Dictionary<string, string> _programNameFolderPairs = new();

        [ObservableProperty] Guid _smartProgramID = Guid.NewGuid();
        [ObservableProperty] string _selectedProgram;
        [ObservableProperty] ObservableCollection<string> _programNames = new();
        [ObservableProperty] ObservableCollection<InputFunctionViewModel> _availableMethods = new();
        [ObservableProperty] ObservableCollection<InputParameterBase> _availableParameters = new();
        [ObservableProperty] ObservableCollection<SmartUpdateAcquisitionFunctionViewModel> _availableAcquisitionUpdates = new();


        public SmartProgramViewModel( IPythonComService nodeComService , SmartProcessingRegisterViewModel smartProcessingRegister, EquipmentState eqState) 
        {
            _nodeComService = nodeComService;
            _equipmentState = eqState;
            smartProcessingRegister.SmartProgramViewModels.Add(this);
            this.PropertyChanged += SmartProgramEditorViewModel_PropertyChanged;
        }

        private async void RequestMethodsAndParameters()
        {

            try
            {
                if (_programNameFolderPairs.TryGetValue(SelectedProgram, out string? program_path))
                {
                    OnSelectedProgramChangedEvent?.Invoke(this, program_path);
                    var methods =
                        await _nodeComService.GetMethods(SelectedProgram);
                    var parameters =
                        await _nodeComService.GetParameters(SelectedProgram);
                    var update_acq_parameters =
                        await _nodeComService.GetUpdateAcqParameters(SelectedProgram);
                    var acquisitionUpdates =
                        await _nodeComService.GetAcquisitionUpdates(SelectedProgram);

                    var methods_json = JArray.Parse(methods);
                    var update_acq_json = JArray.Parse(update_acq_parameters);

                    var update_acq_params = update_acq_json.ToObject<List<InputFunction>>();
                    var newvals = methods_json.ToObject<List<InputFunction>>();


                    var acquisitionUpdatesVals = JsonConvert.DeserializeObject<List<SmartUpdateAcquisitionFunctionViewModel>>(acquisitionUpdates,
                        new UpdateAcquisitionFunctionConverter());


                    foreach(var acqUpdate in acquisitionUpdatesVals)
                    {
                        acqUpdate.SmartProgramViewModel = this;
                        acqUpdate.EquipmentState = _equipmentState;  
                        acqUpdate.UpdateParameters = update_acq_params.Find(x => x.method_name == acqUpdate.AcquisitionUpdate);
                    }


                    if (parameters is not null)
                    {
                        List<InputParameterBase?> input_parameters = new();
                        if (JObject.Parse(parameters).TryGetValue("program_parameters", out JToken? deserialized_value))
                        {
                            var deserialized_array = deserialized_value as JArray;
                            if (deserialized_array is not null)
                            {
                                foreach (var value in deserialized_array)
                                {
                                    input_parameters.Add(JsonConvert.DeserializeObject<InputParameterBase>(value.ToString(), new InputParameterConverter()));
                                }
                            }
                        }
                        AvailableParameters = new ObservableCollection<InputParameterBase>(input_parameters);

                    }
                    var newAvailableMethods = new ObservableCollection<InputFunctionViewModel>(newvals.Select(x => new InputFunctionViewModel(x, SmartProgramID)));
                    var newAvailableAcquisitionUpdates = new ObservableCollection<SmartUpdateAcquisitionFunctionViewModel>(acquisitionUpdatesVals);
                    SubstituteMethods(newAvailableMethods, newAvailableAcquisitionUpdates);

                }

            }
            catch (Exception ex)
            {
                await ShowExceptionAsync(ex);
            }
        }

        private void SmartProgramEditorViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(e.PropertyName== nameof(SelectedProgram))
            {
                RequestMethodsAndParameters();
            }
        }


        private async Task ShowExceptionAsync(Exception ex)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                await ExceptionWindowHandler.ShowDialogAsync(
                    "Error", ex.Message, ex.StackTrace, desktop.MainWindow);
            }
        }

        public async Task OnFileReloadRequested(object? sender, string filepath)
        {
            await _nodeComService.ReloadFile(filepath);
            RequestMethodsAndParameters();

        }

        public void LoadFolder()
        {
            OnOpenFolderRequested?.Invoke(this, new EventArgs());
        }

        public void ReloadFolder()
        {
            OnReloadRequested?.Invoke(this, new EventArgs());
        }



        public async Task SubmitFolderToSmartProgramServer(string path,bool toload)
        {
            LoadedFolder = path;
            var response = await _nodeComService.SubmitFolder(path);

            var folders = JsonConvert.DeserializeObject<FolderResponse>(response);
            if (folders is not null)
            {
                foreach (var f in folders.programs)
                {
                    if (f.name != null && f.path != null)
                    {
                        _programNameFolderPairs.TryAdd(f.name, f.path);
                        if (toload)
                        {
                            if (!ProgramNames.Contains(f.name))
                            {
                                ProgramNames.Add(f.name);
                            }
                        }
                    }
                }
            }
        }
        public void SubstituteMethods(ObservableCollection<InputFunctionViewModel> newMethods,
          ObservableCollection<SmartUpdateAcquisitionFunctionViewModel> newAcquisitionUpdates)
        {

            foreach (var method in AvailableMethods)
            {
                if(!newMethods.Select(x => x.MethodName).Contains(method.MethodName))
                { 
                    foreach (var methodparam in method.MethodParams)
                    {
                        methodparam.RemoveExperimentBindings();
                    }
                    continue;
                }
                var new_method = newMethods.Where(x => x.MethodName == method.MethodName).FirstOrDefault();
                if(new_method.MethodParams.Count!= method.MethodParams.Count)
                {
                    foreach (var methodparam in method.MethodParams)
                    {
                        methodparam.RemoveExperimentBindings();
                    }
                    continue;
                }
                int index = newMethods.IndexOf(new_method);
                if (index >= 0)
                {
                    newMethods[index] = method;
                }
            }

            AvailableMethods = newMethods;
            var to_remove_acquisition_updates = new List<SmartUpdateAcquisitionFunctionViewModel>();
            foreach(var currentUpdateAcq in AvailableAcquisitionUpdates)
            {

                currentUpdateAcq.RemoveExperimentBindings();

            }
            AvailableAcquisitionUpdates.Clear();
            AvailableAcquisitionUpdates = newAcquisitionUpdates;

        }

        public void ClearMethods()
        {
            foreach (var method in AvailableMethods)
            {
                foreach (var methodparam in method.MethodParams)
                {
                    methodparam.RemoveExperimentBindings();
                }
            }
            AvailableMethods.Clear();
            foreach (var updateAcq in AvailableAcquisitionUpdates)
            {
                updateAcq.RemoveExperimentBindings();
                
            }
            AvailableAcquisitionUpdates.Clear();
        }

        public JObject SerializeProgram()
        {
            JObject serializedProgram = new JObject();
            JArray serializedMethods = new JArray();
            JArray serializedAcquisitionUpdates = new JArray();

            serializedProgram.TryAdd("programname", JToken.FromObject(SelectedProgram)); 
           

            foreach (var method in AvailableMethods)
            {

                JArray methodparams_array = new JArray();
                var methodvals = new JObject();
                methodvals.TryAdd("methodname", JToken.FromObject(method.MethodName));
                foreach (var methodparam in method.MethodParams)
                {
                    JObject methodparams = new JObject();


                    if (methodparam.SelectedDetection == null)
                    {
                        throw new Exception($"Unable to serialize smart program: " +
                        $"No Detector selected for parameter {methodparam.InputParameterName}");
                    }


                    if (methodparam.AcquisitionInput.Name == string.Empty)
                    { throw new Exception($"Unable to serialize smart program: " +
                        $"No Acquisition selected for parameter {methodparam.InputParameterName}"); 
                    }


                    if (methodparam.AcquisitionInput.Name == string.Empty)
                    {
                        throw new Exception($"Unable to serialize smart program: " +
                        $"No Detector selected for parameter {methodparam.InputParameterName}"); 
                    }

                    methodparams.TryAdd("acquisition", JToken.FromObject(methodparam.AcquisitionInput.Name));
                    methodparams.TryAdd("detection", JToken.FromObject(methodparam.DetectorInput.Name));
                    methodparams.TryAdd("elementid", JToken.FromObject(methodparam.SelectedDetection.Elementid));
                    methodparams_array.Add(methodparams);


                }
                methodvals.TryAdd("inputparams", methodparams_array);
                serializedMethods.Add(methodvals);
            }
            JArray serializedParameters = new JArray();

            foreach (var parameter in AvailableParameters)
            {
                serializedParameters.Add(JToken.Parse(JsonConvert.SerializeObject(parameter, new InputParameterConverter())));
            }
            foreach (var parameter in AvailableAcquisitionUpdates)
            {
                serializedAcquisitionUpdates.Add(JToken.Parse(JsonConvert.SerializeObject(parameter, new UpdateAcquisitionFunctionConverter())));
            }

            serializedProgram.TryAdd("methods", serializedMethods);
            serializedProgram.TryAdd("parameters", serializedParameters);
            serializedProgram.TryAdd("acquisitionupdates", serializedAcquisitionUpdates);


            return serializedProgram;
        }
    }

    internal class FolderResponse
    {
        public string? status;
        public List<FolderResponsePaths> programs = new();
    }

    internal class FolderResponsePaths
    {
        public string? name;
        public string? path;
    }
}


