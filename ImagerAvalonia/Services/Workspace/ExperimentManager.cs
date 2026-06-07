using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
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
using System.Linq;
using System.Threading.Tasks;

namespace ImagerAvalonia.Services.Workspace;

/// <summary>
/// Central manager for multiple experiments.
/// Handles creation, removal, selection, saving, and loading of experiments.
/// </summary>
public class ExperimentManager : IDisposable
{
    private readonly SystemDefinedSettingsViewModel _systemDefinedSettings;
    private readonly IStageControl _stageControl;
    private readonly SmartProcessingRegisterViewModel _processingViewModel;
    private readonly AcquisitionStateService _acquisitionStateService;
    private readonly EquipmentState _equipmentState;

    private int _numAcquisition = 1;
    private int _numExperiment = 1;
    private AcquisitionSettings? _defaultAcquisition;

    private List<Source> _availableSources = new();
    private List<MovableComponent> _availableFilterWheels = new();
    private List<Robots> _availableRobots = new();
    private List<DetectorEquipment> _availableDetectors = new();

    public ObservableCollection<ExperimentalPanelViewModel> Experiments { get; } = new();
    public ExperimentalPanelViewModel? SelectedExperiment { get; private set; }

    public Action<object, RoutedEventArgs>? OnProgramStorageRequested { get; set; }
    public Action<object>? OnProgramLoadRequested { get; set; }

    /// <summary>Fired when an experiment is added.</summary>
    public event Action<ExperimentalPanelViewModel>? ExperimentAdded;

    /// <summary>Fired when an experiment is removed.</summary>
    public event Action<ExperimentalPanelViewModel>? ExperimentRemoved;

    /// <summary>Fired when the selected experiment changes.</summary>
    public event EventHandler? SelectedExperimentChanged;

    public ExperimentManager(
        SystemDefinedSettingsViewModel systemDefinedSettings,
        IStageControl stageControl,
        SmartProcessingRegisterViewModel processingViewModel,
        AcquisitionStateService acquisitionStateService,
        EquipmentState equipmentState)
    {
        _systemDefinedSettings = systemDefinedSettings;
        _stageControl = stageControl;
        _processingViewModel = processingViewModel;
        _acquisitionStateService = acquisitionStateService;
        _equipmentState = equipmentState;
    }

    public void InitializeEquipment(
        List<Source> availableSources,
        List<MovableComponent> availableFilterWheels,
        List<Robots> availableRobots,
        List<DetectorEquipment> availableDetectors)
    {
        _availableSources = new List<Source>(availableSources);
        _availableFilterWheels = new List<MovableComponent>(availableFilterWheels);
        _availableRobots = new List<Robots>(availableRobots);
        _availableDetectors = new List<DetectorEquipment>(availableDetectors);

        _systemDefinedSettings.Robots = _availableRobots;
        
        // Ensure we have a default acquisition
        InitializeDefaultAcquisition();
    }

    private void InitializeDefaultAcquisition()
    {
        if (_defaultAcquisition == null)
        {
            _defaultAcquisition = AcquisitionSettingsFactory.FromComponents(
                "NewAcq", _availableSources, _availableFilterWheels, _availableDetectors);
            var initAcquisition = new AcquisitionSettingsViewModel(_defaultAcquisition);
            initAcquisition.AcquisitionID = _numAcquisition;
            _defaultAcquisition.AcquisitionSettingsID = _numAcquisition;
            _systemDefinedSettings.Acquisitions.Add(initAcquisition);
            _systemDefinedSettings.Robots = _availableRobots;
        }
    }

    public void SetNumAcquisition(int value)
    {
        _numAcquisition = value;
    }

    public int GetNumAcquisition()
    {
        return _numAcquisition;
    }

    public void IncrementNumAcquisition()
    {
        _numAcquisition++;
    }

    public void AddExperiment()
    {
        var exp = new ExperimentalPanelViewModel(_systemDefinedSettings, _stageControl);

        // Generate unique experiment name
        var expNames = Experiments.Select(x => x.ExperimentName).ToList();
        string expName = "Experiment ";
        int num = 1;

        while (expNames.Contains($"{expName}{num}"))
        {
            num++;
        }
        exp.ExperimentName = $"{expName}{num}";
        _numExperiment = num + 1;

        Experiments.Add(exp);
        ExperimentAdded?.Invoke(exp);
    }

    public async Task SaveExperiment()
    {
        var experimentSerializer = new ExperimentSerialization();
        if (SelectedExperiment != null)
        {
            try
            {
                JObject measurementProgram = experimentSerializer.SerializeExperiment(SelectedExperiment, _processingViewModel);
                OnProgramStorageRequested?.Invoke(measurementProgram.ToString(Newtonsoft.Json.Formatting.None), new RoutedEventArgs());
            }
            catch (Exception ex)
            {
                await ShowExceptionDialogAsync("Error", ex);
            }
        }
    }

    public void LoadExperiment()
    {
        OnProgramLoadRequested?.Invoke(this);
    }

    public async void ParseLoadedExperiment(string program)
    {
        var experimentSerializer = new ExperimentSerialization();
        JToken jsonProgram;

        try
        {
            jsonProgram = JToken.Parse(program);
        }
        catch (Exception ex)
        {
            await ShowExceptionDialogAsync("Error in parsing experiment. Invalid .imag file", ex);
            return;
        }

        try
        {
            // Load acquisitions from the program
            var acquisitionModels = JsonConvert.DeserializeObject<List<AcquisitionSettingsDeserializationModel>>(
                program, new DefinedDetectionsConverter());

            if (acquisitionModels != null)
            {
                foreach (var x in acquisitionModels)
                {
                    int id = 0;
                    string previousName = x.Name;

                    // Ensure unique acquisition name
                    while (_systemDefinedSettings.Acquisitions.Select(acq => acq.Name).Any(y => y == x.Name))
                    {
                        x.Name = $"{previousName}_{id}";
                        experimentSerializer.AcquisitionMaps.Add(previousName, x.Name);
                        id++;
                    }

                    var defaultAcquisition = AcquisitionSettingsFactory.FromComponents(
                        x.Name, _availableSources, _availableFilterWheels, _availableDetectors);
                    var modifiedAcquisition = new AcquisitionSettingsViewModel(
                        AcquisitionSettingsFactory.CopyFromDeserializedModel(defaultAcquisition, x));

                    _systemDefinedSettings.Acquisitions.Add(modifiedAcquisition);
                }
            }
        }
        catch (Exception ex)
        {
            await ShowExceptionDialogAsync("Error in loading acquisitions", ex);
        }

        // Create the experiment ViewModel
        var expVM = new ExperimentalPanelViewModel(_systemDefinedSettings, _stageControl);

        try
        {
            experimentSerializer.SetExperiment(expVM);
            NodeBase expNodes = experimentSerializer.GetDeserializedExperiment(jsonProgram, _systemDefinedSettings);
            expVM.SetExpNodes(new ObservableCollection<NodeBase>() { expNodes });
        }
        catch (Exception ex)
        {
            await ShowExceptionDialogAsync("Error in loading experiment tree", ex);
            return;
        }

        // Generate unique experiment name
        var expNames = Experiments.Select(x => x.ExperimentName).ToList();
        string expName = "Experiment ";
        int num = 1;

        while (expNames.Contains($"{expName}{num}"))
        {
            num++;
        }
        expVM.ExperimentName = $"{expName}{num}";
        _numExperiment = num + 1;

        Experiments.Add(expVM);
        ExperimentAdded?.Invoke(expVM);
    }

    public void RemoveExperiment()
    {
        if (Experiments.Count > 0 && SelectedExperiment != null)
        {
            var removed = SelectedExperiment;
            SelectedExperiment.Dispose();
            Experiments.Remove(SelectedExperiment);
            ExperimentRemoved?.Invoke(removed);
            SelectedExperiment = Experiments.Count > 0 ? Experiments[0] : null;
            _acquisitionStateService.SelectedExperiment = SelectedExperiment;
            SelectedExperimentChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetSelectedExperiment(ExperimentalPanelViewModel? experiment)
    {
        SelectedExperiment = experiment;
        _acquisitionStateService.SelectedExperiment = experiment;
        SelectedExperimentChanged?.Invoke(this, EventArgs.Empty);
    }

    private static async Task ShowExceptionDialogAsync(string title, Exception ex)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow;
            await ExceptionWindowHandler.ShowDialogAsync(title, ex.Message, ex.StackTrace, mainWindow);
        }
    }

    public void Dispose()
    {
        foreach (var exp in Experiments)
        {
            exp.Dispose();
        }
        Experiments.Clear();
    }
}
