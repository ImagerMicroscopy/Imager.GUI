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
        private readonly SmartProcessingRegisterViewModel _smartProcessingRegister;

        public SmartProgramModel Model { get; private set; } = new();

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
            _smartProcessingRegister = smartProcessingRegister;
            smartProcessingRegister.AddSmartProgram(this, Model);
            this.PropertyChanged += SmartProgramEditorViewModel_PropertyChanged;
            HookAvailableParametersCollectionChanged(AvailableParameters);
            HookAvailableAcquisitionUpdatesCollectionChanged(AvailableAcquisitionUpdates);
        }

        /// <summary>
        /// Replaces this VM's freshly-constructed SmartProgramModel with one just
        /// deserialized (project load / bundle import), preserving its SmartProgramID,
        /// FileBundle, and saved SmartProgramDefinition (methods/parameters/bindings-by-id).
        /// Must be called before RequestMethodsAndParametersAsync/ImportBundleAsync run,
        /// since those read/write through Model.
        /// </summary>
        internal void AdoptModel(SmartProgramModel loadedModel)
        {
            var oldModel = Model;
            Model = loadedModel;
            _smartProcessingRegister.ReplaceSmartProgramModel(oldModel, loadedModel);
            OnPropertyChanged(nameof(SmartProgramID));
            OnPropertyChanged(nameof(LoadedFolder));
        }

        /// <summary>
        /// Writes this program's bundled .py source (Model.FileBundle) into targetFolder
        /// via the Python API, submits that folder so the program(s) register normally,
        /// refetches methods/parameters, then re-applies the saved per-parameter detection
        /// element bindings (Model.SmartProgramDefinition.methods[].inputparams[].elementid)
        /// against the live measurement tree rooted at treeRoot - driven through the normal
        /// InputParameterViewModel.SelectedNode setter so all the usual binding side effects
        /// (SmartProgramBindings, OnNodeDeleted subscription, acquisition wiring) happen the
        /// same way a manual drag-and-drop binding would.
        /// </summary>
        public async Task<List<string>> ImportBundleAsync(string targetFolder, MeasurementElementViewModel? treeRoot)
        {
            var warnings = new List<string>();
            var bundle = Model.FileBundle;
            if (bundle is null)
            {
                warnings.Add("No bundled source is stored for this smart program - nothing to import.");
                return warnings;
            }

            var bundleJson = JObject.FromObject(bundle);
            await _nodeComService.ImportBundle(targetFolder, bundleJson);

            await SubmitFolderToSmartProgramServer(targetFolder, toload: true);

            // Snapshot the saved bindings before RequestMethodsAndParametersAsync clears
            // and rebuilds AvailableMethods/AvailableAcquisitionUpdates from the
            // freshly-fetched lists. Method-parameter bindings and acquisition-update
            // bindings are two structurally separate mechanisms (InputParameterViewModel.
            // SelectedNode vs SmartUpdateAcquisitionFunctionViewModel.SelectedNode) - both
            // need to be captured here and restored after the refetch.
            var savedMethods = Model.SmartProgramDefinition.methods
                .Select(m => new
                {
                    m.methodname,
                    Params = m.inputparams.ToList()
                })
                .ToList();
            var savedAcquisitionUpdates = Model.SmartProgramDefinition.acquisitionupdates
                .Where(a => a.elementid != Guid.Empty)
                .ToList();

            this.PropertyChanged -= SmartProgramEditorViewModel_PropertyChanged;
            try
            {
                SelectedProgram = bundle.programname;
            }
            finally
            {
                this.PropertyChanged += SmartProgramEditorViewModel_PropertyChanged;
            }
            await RequestMethodsAndParametersAsync();

            if (treeRoot is null)
            {
                warnings.Add("No experiment tree was available - detection element bindings were not restored.");
                return warnings;
            }

            foreach (var savedMethod in savedMethods)
            {
                var liveMethod = AvailableMethods.FirstOrDefault(m => m.MethodName == savedMethod.methodname);
                if (liveMethod is null)
                {
                    warnings.Add($"Method '{savedMethod.methodname}' no longer exists in the reloaded program - its bindings were dropped.");
                    continue;
                }

                // Matched positionally, same assumption SubstituteMethods already makes
                // elsewhere in this class (method name + parameter order/count unchanged
                // since save) - there is no per-parameter name stored on InputParameterModel
                // to match on more precisely.
                if (liveMethod.MethodParams.Count != savedMethod.Params.Count)
                {
                    warnings.Add($"Method '{savedMethod.methodname}': parameter count changed since this program was saved - its bindings were dropped.");
                    continue;
                }

                for (int i = 0; i < savedMethod.Params.Count; i++)
                {
                    var savedParam = savedMethod.Params[i];
                    if (savedParam?.elementid is not Guid elementId)
                        continue;

                    var liveParam = liveMethod.MethodParams[i];
                    var targetNode = treeRoot.FindByElementId(elementId);

                    if (targetNode is null)
                    {
                        warnings.Add($"Method '{savedMethod.methodname}': detection element {elementId} was not found in the current experiment - left unbound.");
                        continue;
                    }

                    // SelectedNode only restores the elementid link - it does not touch
                    // AcquisitionInput/DetectorInput, so the saved acquisition (camera) and
                    // detector names have to be re-selected afterwards, in that order (picking
                    // an AcquisitionInput is what populates DefinedDetectors that DetectorInput
                    // then chooses from - see OnAcquisitionInputChanged/OnDetectorInputChanged).
                    liveParam.SelectedNode = targetNode;

                    if (!string.IsNullOrEmpty(savedParam.acquisition))
                    {
                        // Must match against FilteredAcquisitions (the ComboBox's actual
                        // ItemsSource, enabled-only), not DefinedAcquisitions (all of them,
                        // enabled or not) - AcquisitionInput can only visually show as
                        // selected if it's a reference that's actually present in the
                        // dropdown's item list.
                        var matchedAcquisition = liveParam.FilteredAcquisitions
                            .FirstOrDefault(a => a.Name == savedParam.acquisition);
                        if (matchedAcquisition is null)
                        {
                            warnings.Add($"Method '{savedMethod.methodname}': acquisition '{savedParam.acquisition}' is disabled or no longer available on the bound detection element - left unset.");
                        }
                        else
                        {
                            liveParam.AcquisitionInput = matchedAcquisition;

                            if (!string.IsNullOrEmpty(savedParam.detection))
                            {
                                var matchedDetector = liveParam.DefinedDetectors
                                    .FirstOrDefault(d => d.Name == savedParam.detection);
                                if (matchedDetector is null)
                                {
                                    warnings.Add($"Method '{savedMethod.methodname}': detector '{savedParam.detection}' is no longer available for acquisition '{savedParam.acquisition}' - left unset.");
                                }
                                else
                                {
                                    liveParam.DetectorInput = matchedDetector;
                                }
                            }
                        }
                    }
                }
            }

            foreach (var savedAcqUpdate in savedAcquisitionUpdates)
            {
                var liveAcqUpdate = AvailableAcquisitionUpdates
                    .FirstOrDefault(a => a.AcquisitionUpdate == savedAcqUpdate.acquisitionupdatefunction);

                if (liveAcqUpdate is null)
                {
                    warnings.Add($"Acquisition update '{savedAcqUpdate.acquisitionupdatefunction}' no longer exists in the reloaded program - its binding was dropped.");
                    continue;
                }

                var targetNode = treeRoot.FindByElementId(savedAcqUpdate.elementid);
                if (targetNode is null)
                {
                    warnings.Add($"Acquisition update '{savedAcqUpdate.acquisitionupdatefunction}': detection element {savedAcqUpdate.elementid} was not found in the current experiment - left unbound.");
                    continue;
                }

                liveAcqUpdate.SelectedNode = targetNode;
            }

            return warnings;
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
            await RequestMethodsAndParametersAsync();
        }

        // Awaitable core of RequestMethodsAndParameters - split out so the
        // smart-program-import flow (ImportBundleAsync) can await the fetch
        // completing before it reapplies saved detection-element bindings,
        // which need AvailableMethods to already be populated.
        private async Task RequestMethodsAndParametersAsync()
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

        /// <summary>
        /// Fetches this program's main .py file plus every locally-connected .py file
        /// (via the Python API's dependency-walker) and stores it on Model.FileBundle,
        /// so it gets persisted the next time the project is saved (see
        /// FullEquipmentState.SmartPrograms). Storage only - never sent to Haskell.
        /// </summary>
        public async Task ExportBundleAsync()
        {
            if (string.IsNullOrEmpty(SelectedProgram))
                return;

            var bundleJson = await _nodeComService.ExportBundle(SelectedProgram);
            Model.FileBundle = JsonConvert.DeserializeObject<SmartProgramBundle>(bundleJson);
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