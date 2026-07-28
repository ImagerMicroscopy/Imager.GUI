using Autofac;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Exceptions;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.ImagerModels.SmartProgramModels;
using ImagerAvalonia.Services.MeasurementControl;
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

        private readonly IPythonCom _nodeComService;
        private readonly EquipmentState _equipmentState;

        public SmartProgramModel Model { get; } = new();

        public Guid SmartProgramID => Model.SmartProgramID;

        public string LoadedFolder
        {
            get => Model.LoadedFolder;
            set => Model.LoadedFolder = value;
        }

        [ObservableProperty] string _selectedProgram;
        [ObservableProperty] ObservableCollection<string> _programNames = new();

        [ObservableProperty] ObservableCollection<InputFunctionViewModel> _availableMethods = new();
        [ObservableProperty] ObservableCollection<InputParameterBase> _availableParameters = new();
        [ObservableProperty] ObservableCollection<SmartUpdateAcquisitionFunctionViewModel> _availableAcquisitionUpdates = new();


        public SmartProgramViewModel(IPythonCom nodeComService, SmartProcessingRegisterViewModel smartProcessingRegister, EquipmentState eqState)
        {
            _nodeComService = nodeComService;
            _equipmentState = eqState;
            smartProcessingRegister.AddSmartProgram(this, Model);
            this.PropertyChanged += SmartProgramEditorViewModel_PropertyChanged;
            HookAvailableParametersCollectionChanged(AvailableParameters);
            HookAvailableAcquisitionUpdatesCollectionChanged(AvailableAcquisitionUpdates);
        }

        // Keeps Model.SelectedProgram in step whenever the bindable SelectedProgram changes.
        partial void OnSelectedProgramChanged(string value)
        {
            Model.SmartProgramDefinition.programname = value ?? string.Empty;
        }

        #region Parameters <-> Model sync


        partial void OnAvailableParametersChanged(ObservableCollection<InputParameterBase> value)
        {
            HookAvailableParametersCollectionChanged(value);
            SyncParametersToModel();
        }

        private ObservableCollection<InputParameterBase>? _hookedParameterCollection;

        private void HookAvailableParametersCollectionChanged(ObservableCollection<InputParameterBase> collection)
        {
            if (_hookedParameterCollection is not null)
            {
                _hookedParameterCollection.CollectionChanged -= AvailableParameters_CollectionChanged;
            }
            collection.CollectionChanged += AvailableParameters_CollectionChanged;
            _hookedParameterCollection = collection;
        }

        private void AvailableParameters_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            SyncParametersToModel();
        }

        private void SyncParametersToModel()
        {
            // Model.Parameters has no setter, so mutate it in place.
            Model.SmartProgramDefinition.parameters.Clear();
            Model.SmartProgramDefinition.parameters.AddRange(AvailableParameters);
        }
        #endregion

        #region Acquisition updates <-> Model sync


        partial void OnAvailableAcquisitionUpdatesChanged(ObservableCollection<SmartUpdateAcquisitionFunctionViewModel> value)
        {
            HookAvailableAcquisitionUpdatesCollectionChanged(value);
            SyncAcquisitionUpdatesToModel();
        }

        private ObservableCollection<SmartUpdateAcquisitionFunctionViewModel>? _hookedAcquisitionUpdateCollection;

        private void HookAvailableAcquisitionUpdatesCollectionChanged(ObservableCollection<SmartUpdateAcquisitionFunctionViewModel> collection)
        {
            if (_hookedAcquisitionUpdateCollection is not null)
            {
                _hookedAcquisitionUpdateCollection.CollectionChanged -= AvailableAcquisitionUpdates_CollectionChanged;
            }
            collection.CollectionChanged += AvailableAcquisitionUpdates_CollectionChanged;
            _hookedAcquisitionUpdateCollection = collection;
        }

        private void AvailableAcquisitionUpdates_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            SyncAcquisitionUpdatesToModel();
        }

        private void SyncAcquisitionUpdatesToModel()
        {
            // Each VM now owns its own Model instance (built up as the VM's bindable
            // properties/child parameter VMs change), so this just projects them -
            // same pattern as AvailableMethods.Select(x => x.Model) below.
            Model.SmartProgramDefinition.acquisitionupdates.Clear();
            Model.SmartProgramDefinition.acquisitionupdates.AddRange(AvailableAcquisitionUpdates.Select(x => x.Model));
        }
        #endregion

        private async void RequestMethodsAndParameters()
        {

            try
            {
                var program_path = Model.GetProgramPath();
                if (program_path is not null)
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

                    var update_acq_params = update_acq_json.ToObject<List<ImportedInputFunctionModel>>();
                    var newvals = methods_json.ToObject<List<ImportedInputFunctionModel>>();
                    Model.SmartProgramDefinition.methods.Clear();


                    var acquisitionUpdatesVals = JsonConvert.DeserializeObject<List<SmartUpdateAcquisitionFunctionViewModel>>(acquisitionUpdates,
                        new UpdateAcquisitionFunctionConverter());


                    foreach (var acqUpdate in acquisitionUpdatesVals)
                    {
                        acqUpdate.SmartProgramViewModel = this;
                        acqUpdate.EquipmentState = _equipmentState;
                        acqUpdate.Model.SmartProgramId = SmartProgramID;
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
            if (e.PropertyName == nameof(SelectedProgram))
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



        public async Task SubmitFolderToSmartProgramServer(string path, bool toload)
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
                        // NOTE: Model.RegisterProgram overwrites an existing entry for the
                        // same name, whereas the previous _programNameFolderPairs.TryAdd
                        // left the first-registered path in place. Flagging in case that
                        // distinction matters for repeated folder submissions.
                        Model.RegisterProgram(f.name, f.path);
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
                if (!newMethods.Select(x => x.MethodName).Contains(method.MethodName))
                {
                    foreach (var methodparam in method.MethodParams)
                    {
                        methodparam.RemoveExperimentBindings();
                    }
                    continue;
                }
                var new_method = newMethods.Where(x => x.MethodName == method.MethodName).FirstOrDefault();
                if (new_method.MethodParams.Count != method.MethodParams.Count)
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
            Model.SmartProgramDefinition.methods = AvailableMethods.Select(x => x.Model).ToList();

            var to_remove_acquisition_updates = new List<SmartUpdateAcquisitionFunctionViewModel>();
            foreach (var currentUpdateAcq in AvailableAcquisitionUpdates)
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
            AvailableParameters.Clear();
            Model.Clear();
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
}