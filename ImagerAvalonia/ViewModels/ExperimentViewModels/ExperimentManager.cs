using Autofac;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ImagerAvalonia.Services.ImagerModels.MeasurementElementsModels;
using ImagerAvalonia.Services.ImagerModels.SmartProgramModels;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Workspace;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ImagerAvalonia.ViewModels.MeasurementViewModels;

/// <summary>
/// Central manager for multiple experiments.
/// Handles creation, removal, selection, and save/load orchestration of experiments.
/// Equipment/acquisition setup lives in EquipmentContext; dialogs are the UI layer's job.
/// </summary>
public class ExperimentManager : IDisposable
{
    private readonly GlobalDefinedSettingsViewModel _systemDefinedSettings;
    private readonly IStageControl _stageControl;
    private readonly SmartProcessingRegisterViewModel _processingViewModel;
    private readonly IMeasurementElementViewModelFactory _factory;
    private readonly EquipmentWorkspace _equipmentWorkspace;
    private readonly ExperimentBuilderFactory _experimentBuilderFactory;

    public ObservableCollection<ExperimentalPanelViewModel> Experiments { get; } = new();
    public ExperimentalPanelViewModel? SelectedExperiment { get; private set; }

    /// <summary>Raised when a program needs to be persisted. Payload is the serialized JSON string.</summary>
    public Action<string>? OnProgramStorageRequested { get; set; }

    /// <summary>Raised when a program load is requested.</summary>
    public Action<object>? OnProgramLoadRequested { get; set; }
    public DefinedDetection SelectedDetection { get; set; }

    private int NumAcquisitions = 0;
    /// <summary>Raised on a recoverable error (e.g. failed save). UI layer decides how to present it.</summary>
    public event EventHandler<Exception>? ErrorOccurred;

    public event Action<ExperimentalPanelViewModel, List<AcquisitionSettingsViewModel>>? ExperimentLoaded;
    public event Action<ExperimentalPanelViewModel>? ExperimentAdded;
    public event Action<ExperimentalPanelViewModel>? ExperimentRemoved;
    public event EventHandler? SelectedExperimentChanged;

    public ExperimentManager(
        GlobalDefinedSettingsViewModel systemDefinedSettings,
        IStageControl stageControl,
        EquipmentWorkspace equipmentWorkspace,
        SmartProcessingRegisterViewModel processingViewModel,
        IMeasurementElementViewModelFactory factory,
        ExperimentBuilderFactory experimentBuilderFactory)
    {
        _systemDefinedSettings = systemDefinedSettings;
        _stageControl = stageControl;
        _processingViewModel = processingViewModel;
        _factory = factory;
        _equipmentWorkspace = equipmentWorkspace;
        _experimentBuilderFactory = experimentBuilderFactory;
    }


    public void SetNumAcquisition(int value) => NumAcquisitions = value;
    public int GetNumAcquisition() => NumAcquisitions;
    public void IncrementNumAcquisition() => NumAcquisitions++ ;

    public void AddExperiment()
    {
        var builder = _experimentBuilderFactory.Create();
        var exp = new ExperimentalPanelViewModel(_systemDefinedSettings, _stageControl, builder);

        var expNames = Experiments.Select(x => x.ExperimentName).ToList();
        const string expName = "Experiment ";
        int num = 1;

        while (expNames.Contains($"{expName}{num}"))
        {
            num++;
        }
        exp.ExperimentName = $"{expName}{num}";

        Experiments.Add(exp);
        ExperimentAdded?.Invoke(exp);
    }

    public async Task SaveExperiment()
    {
        if (SelectedExperiment == null)
            return;

        try
        {
            // Refresh each SmartProgram's bundled .py source (main file + connected
            // local files) before saving, so the project stays reloadable even if the
            // original folder later disappears/moves - fully automatic, no user action.
            foreach (var smartProgramVm in _processingViewModel.SmartProgramViewModels)
            {
                try
                {
                    await smartProgramVm.ExportBundleAsync();
                }
                catch (Exception bundleEx)
                {
                    // Non-fatal: still save the rest of the project even if the Python
                    // server is unreachable or a program has no folder loaded yet.
                    ErrorOccurred?.Invoke(this, bundleEx);
                }
            }

            var programJson = BuildFullEquipmentStateJson();
            OnProgramStorageRequested?.Invoke(programJson);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
        }
    }

    public string BuildFullEquipmentStateJson()
    {
        if (SelectedExperiment == null)
            throw new InvalidOperationException("No experiment is selected.");

        var payload = ReturnMeasurementTree();
        var detections = ReturnUsedDetections();

        var program = new MeasurementProgram(payload, detections.ToDictionary(d => d.Name, d => d.Settings));
        var smartPrograms = _processingViewModel.SmartProgramViewModels.Select(vm => vm.Model).ToList();
        var imagerState = new FullEquipmentState()
        {
            CurrentEquipment = SelectedExperiment.ReferenceContext?.EquipmentWorkspace ?? _equipmentWorkspace,
            CurrentProgram = program,
            SmartPrograms = smartPrograms,
        };

        return FullEquipmentStateSerializer.Serialize(imagerState);
    }

    public void LoadExperiment()
    {
        OnProgramLoadRequested?.Invoke(this);
    }

    public async Task ParseLoadedExperiment(string programjson)
    {
        var fullimagerstate = FullEquipmentStateSerializer.Deserialize(programjson);


        var measurementProgram = fullimagerstate.CurrentProgram;
        var nameMap = _systemDefinedSettings.ReconcileAcquisitions(
          measurementProgram.Detections.Select(kvp => (kvp.Key, kvp.Value)), fullimagerstate.CurrentEquipment);

        RenameAcquisitionReferencesInSmartPrograms(fullimagerstate.SmartPrograms, nameMap);

        var context = new LoadContext
        {
            Settings = _systemDefinedSettings,
            StageControl = _stageControl,
            EquipmentWorkspace = fullimagerstate.CurrentEquipment,
            ExperimentManager = this,
            AcquisitionNameMap = nameMap
        };

        var experimentBuilder = _experimentBuilderFactory.Create();
        var programVMTree = MeasurementElementViewModelFactory.Build(measurementProgram.Program, context);
        var programRoot = new RootNode()
        {
            Children = programVMTree.Children,
            StorageService = experimentBuilder.StorageService
        };


        var experimentVm = new ExperimentalPanelViewModel(_systemDefinedSettings, _stageControl, experimentBuilder)
        {
            ExperimentName = "Loaded Experiment",
            Root = programRoot,
            Items = new ObservableCollection<MeasurementElementViewModel> { programRoot }
        };
        var acquisitions = nameMap.Values.ToList();
        experimentVm.ReferenceContext = context;

        await RestoreSmartProgramsAsync(fullimagerstate.SmartPrograms, programRoot);

        ExperimentLoaded?.Invoke(experimentVm, acquisitions);
    }

    private static void RenameAcquisitionReferencesInSmartPrograms(
        List<SmartProgramModel> smartPrograms,
        IReadOnlyDictionary<string, AcquisitionSettingsViewModel> nameMap)
    {
        var renames = nameMap
            .Where(kvp => kvp.Value.Name != kvp.Key)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name);

        if (renames.Count == 0)
            return;

        foreach (var program in smartPrograms)
        {
            foreach (var method in program.SmartProgramDefinition.methods)
            {
                foreach (var inputParam in method.inputparams)
                {
                    if (inputParam.acquisition != null && renames.TryGetValue(inputParam.acquisition, out var newName))
                    {
                        inputParam.acquisition = newName;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Recreates a SmartProgramViewModel (GUI tab) for each SmartProgram saved with this
    /// project, adopting the deserialized SmartProgramModel (preserving its SmartProgramID
    /// and bundled .py source) instead of the fresh one DI normally hands out. If the
    /// program's original folder is no longer reachable, prompts once per program for a
    /// folder to restore its bundled files into, then re-applies its saved detection
    /// element bindings against treeRoot - fully automatic, no explicit "import" action.
    /// </summary>
    private async Task RestoreSmartProgramsAsync(List<SmartProgramModel> smartPrograms, MeasurementElementViewModel treeRoot)
    {
        foreach (var savedModel in smartPrograms)
        {
            var vm = App.Container.Resolve<SmartProgramViewModel>();
            vm.AdoptModel(savedModel);
            _processingViewModel.RequestTabFor(vm);

            // ProgramFolders/LoadedFolder are [JsonIgnore] (never persisted - see
            // SmartProgramModel), so a freshly deserialized program never has a
            // remembered folder path to fall back on; the bundled source (if any) is
            // always what drives restoration after a project load.
            if (savedModel.FileBundle is null)
            {
                ErrorOccurred?.Invoke(this, new InvalidOperationException(
                    $"Smart program '{savedModel.SmartProgramDefinition.programname}' has no reachable folder and no bundled source - it could not be restored."));
                continue;
            }

            var chosenFolder = await PromptForBundleRestoreFolderAsync(savedModel.SmartProgramDefinition.programname);
            if (string.IsNullOrEmpty(chosenFolder))
            {
                ErrorOccurred?.Invoke(this, new InvalidOperationException(
                    $"Smart program '{savedModel.SmartProgramDefinition.programname}' was skipped - no folder was chosen to restore its files into."));
                continue;
            }

            var warnings = await vm.ImportBundleAsync(chosenFolder, treeRoot);
            if (warnings.Count > 0)
            {
                ErrorOccurred?.Invoke(this, new InvalidOperationException(
                    $"Smart program '{savedModel.SmartProgramDefinition.programname}' was restored with warnings:\n- {string.Join("\n- ", warnings)}"));
            }
        }
    }

    private static async Task<string?> PromptForBundleRestoreFolderAsync(string programName)
    {
        var topLevel = new Window();
        try
        {
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = $"Choose a folder to restore smart program '{programName}'",
                AllowMultiple = false
            });

            if (folders.Count == 0)
                return null;

            return folders[0].Path.ToString().Replace("file:///", "");
        }
        catch
        {
            return null;
        }
    }

    public void RemoveExperiment()
    {
        if (Experiments.Count == 0 || SelectedExperiment == null)
            return;

        var removed = SelectedExperiment;
        removed.Dispose();
        Experiments.Remove(removed);
        ExperimentRemoved?.Invoke(removed);
        SelectedExperiment = Experiments.Count > 0 ? Experiments[0] : null;
        SelectedExperimentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetSelectedExperiment(ExperimentalPanelViewModel? experiment)
    {
        SelectedExperiment = experiment;
        SelectedExperimentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        foreach (var exp in Experiments)
        {
            exp.Dispose();
        }
        Experiments.Clear();
    }

    public string? GetStoragePath()
    {
        if (SelectedExperiment != null)
        {
            return SelectedExperiment.GetStoragePath();
        }
        return null;    
    }

    public bool IsStorageEnabled()
    {
        if(SelectedExperiment!=null && 
            !SelectedExperiment.Root.IsExperimentStorageEnabled)
        {
            return false;
        }
        return true;
    }

    public MeasurementElementBase ReturnMeasurementTree()
    {
        if (SelectedExperiment == null)
            throw new InvalidOperationException("No experiment is selected.");

        return SelectedExperiment.GetMeasurementElement();
    }

    internal List<DefinedDetection> ReturnUsedDetections()
    {
        if (SelectedExperiment == null)
            throw new InvalidOperationException("No experiment is selected.");
        var measurementTree = SelectedExperiment.GetMeasurementElement();
        var detections = new List<DefinedDetection>();
        ReturnDetections(measurementTree, ref detections);
        return detections;
    }

    public static void ReturnAcqDetPairs(MeasurementElementBase measurementElement,
        ref List<Tuple<string, string>> pairs)
    {
        if (measurementElement is DetectionElement detectionElement)
        {
            foreach(var detection in detectionElement.EnabledDetectionParameters)
            {
                foreach (var detector in detection.Settings.Detectors)
                {
                    if (detector.IsEnabled)
                    {
                        var acq_det_pair = new Tuple<string, string>(
                        detection.Name,
                        detector.Detectorname);
                        if(!pairs.Contains(acq_det_pair))
                        {
                            pairs.Add(acq_det_pair);
                        }
                    }             
                }
            }            
        }
        foreach(var element in measurementElement.Elements)
        {
            ReturnAcqDetPairs(element, ref pairs);
        }
    }

    public static List<XYStagePosition> ReturnUsedStagePositions(MeasurementElementBase measurementElement)
    {
        var positions = new List<XYStagePosition>();
        ReturnStagePositions(measurementElement, ref positions);
        return positions;
    }
    
   public static void ReturnStagePositions(MeasurementElementBase measurementElement, ref List<XYStagePosition> positions)
    {
        if (measurementElement is StageLoopElement stagePositionElement)
        {
            foreach(var position in stagePositionElement.Positions)
            {
                if (!positions.Contains(position))
                {
                    positions.Add(position);
                }
            }   
        }

        foreach (var element in measurementElement.Elements)
        {
            ReturnStagePositions(element, ref positions);
        }
    }   

    public static void ReturnDetections(MeasurementElementBase measurementElement, ref List<DefinedDetection> detections)
    {
        if (measurementElement is DetectionElement detectionElement)
        {
            foreach (var detection in detectionElement.EnabledDetectionParameters)
            {
                if (!detections.Any(d => d.Name == detection.Name))
                {
                    detections.Add(detection.Clone());
                }
            }
        }

        foreach (var element in measurementElement.Elements)
        {
            ReturnDetections(element, ref detections);
        }
    }
}