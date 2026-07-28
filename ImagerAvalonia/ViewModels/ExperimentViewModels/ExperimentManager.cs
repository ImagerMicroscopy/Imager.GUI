using ImagerAvalonia.Services.ImagerModels.MeasurementElementsModels;
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
            var payload = ReturnMeasurementTree();
            var detections = ReturnUsedDetections();


            var program = new MeasurementProgram(payload, detections.ToDictionary(d => d.Name, d => d.Settings));
            var imagerState = new FullEquipmentState()
            {
                CurrentEquipment = _equipmentWorkspace,
                CurrentProgram = program,
            };

            if (SelectedExperiment.ReferenceContext != null)
            {
                imagerState = new FullEquipmentState()
                {
                    CurrentEquipment = SelectedExperiment.ReferenceContext.EquipmentWorkspace,
                    CurrentProgram = program,
                };
            }

            var programJson = FullEquipmentStateSerializer.Serialize(imagerState);
            //var measurementProgram = JObject.Parse(MeasurementSerializer.Serialize(payload));
            OnProgramStorageRequested?.Invoke(programJson);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
        }
        await Task.CompletedTask;
    }

    public void LoadExperiment()
    {
        OnProgramLoadRequested?.Invoke(this);
    }

    public Task ParseLoadedExperiment(string programjson)
    {
        var fullimagerstate = FullEquipmentStateSerializer.Deserialize(programjson);


        var measurementProgram = fullimagerstate.CurrentProgram;
        var nameMap = _systemDefinedSettings.ReconcileAcquisitions(
          measurementProgram.Detections.Select(kvp => (kvp.Key, kvp.Value)), fullimagerstate.CurrentEquipment);

        var context = new LoadContext
        {
            Settings = _systemDefinedSettings,
            StageControl = _stageControl,
            EquipmentWorkspace = fullimagerstate.CurrentEquipment,
            ExperimentManager = this,
            AcquisitionNameMap = nameMap
        };

        var programVMTree = MeasurementElementViewModelFactory.Build(measurementProgram.Program, context);
        var programRoot = new RootNode()
        {
            Children = programVMTree.Children
        };


        var experimentVm = new ExperimentalPanelViewModel(_systemDefinedSettings, _stageControl, _experimentBuilderFactory.Create())
        {
            ExperimentName = "Loaded Experiment",
            Root = programRoot,
            Items = new ObservableCollection<MeasurementElementViewModel> { programRoot }
        };
        var acquisitions = nameMap.Values.ToList();
        experimentVm.ReferenceContext = context;


        ExperimentLoaded?.Invoke(experimentVm, acquisitions);
        return Task.CompletedTask;
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