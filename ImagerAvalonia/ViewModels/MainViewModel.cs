using Autofac;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Exceptions;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Workspace;
using ImagerAvalonia.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;




namespace ImagerAvalonia.ViewModels;

public partial class MainViewModel : ViewModelBase
{


    public IImagerCommunicationManager _communicationManager;
    public ImageDisplayViewModel? ImageView { get; private set; }

    public bool IsLiveEnabled = false;



    [ObservableProperty] ImageControlPanelViewModel _ImageControlPanel;

    private bool IsExperimentalPanelOpen { get; set; } = false;

    private readonly ExperimentManager _experimentManager;

    [ObservableProperty] bool _IsExperimentEnabled = false;
    [ObservableProperty] bool _IsEnableExperimentalPanel = true;

    [ObservableProperty] public ObservableCollection<AcquisitionSettingsViewModel> _Acquisitions = new();

    [ObservableProperty] SourcesViewModel? _SelectedSource;
    [ObservableProperty] DetectorEquipmentViewModel? _SelectedDetector;
    [ObservableProperty] AcquisitionSettingsViewModel? _SelectedAcquisition;
    [ObservableProperty] SystemDefinedSettingsViewModel _SystemDefinedSettings;

    public List<MovableComponent> AvailableFilterWheels = new();
    public List<Source> AvailableSources = new();
    public List<Robots> AvailableRobots = new();
    public List<DetectorEquipment> AvailableDetectors = new List<DetectorEquipment> { };

    private readonly IStageControl _stageControl;
    private readonly SmartProcessingRegisterViewModel _processingViewModel;
    private readonly AcquisitionStateService _acquisitionStateService;
    private readonly EquipmentState _equipmentState;

    // These properties now delegate to ExperimentManager for binding
    public ObservableCollection<ExperimentalPanelViewModel> Experiments => _experimentManager.Experiments;
    
    public ExperimentalPanelViewModel? SelectedExperiment
    {
        get => _experimentManager.SelectedExperiment;
        set
        {
            if (_experimentManager.SelectedExperiment != value)
            {
                _experimentManager.SetSelectedExperiment(value);
                OnPropertyChanged(nameof(SelectedExperiment));
            }
        }
    }


    public MainViewModel(
        IStageControl stageControl, 
        ImageControlPanelViewModel imagePanel, 
        SystemDefinedSettingsViewModel userDefinedAcquisitions, 
        SmartProcessingRegisterViewModel processViewModel,
        AcquisitionStateService acquisitionState, 
        EquipmentState equipmentState)
    {
        _SystemDefinedSettings = userDefinedAcquisitions;
        _stageControl = stageControl;
        _communicationManager = ImagerCommunicationManager.Instance;
        _processingViewModel = processViewModel;
        _acquisitionStateService = acquisitionState;
        _equipmentState = equipmentState;

        // Create the ExperimentManager to handle experiment collection logic
        _experimentManager = new ExperimentManager(
            userDefinedAcquisitions,
            stageControl,
            processViewModel,
            acquisitionState,
            equipmentState);
        
        // Forward storage/load callbacks from ExperimentManager to MainViewModel
        _experimentManager.OnProgramStorageRequested = (program, args) => 
            OnProgramStorageRequested?.Invoke(program, args);
        _experimentManager.OnProgramLoadRequested = sender => 
            OnProgramLoadRequested?.Invoke(sender);

        _experimentManager.SelectedExperimentChanged += (sender, args) =>
        {
            OnPropertyChanged(nameof(SelectedExperiment));
        };

        ImageControlPanel = imagePanel;
        ImageControlPanel.SetAvailableAcquisitions(SystemDefinedSettings);
    }

    public void InitializeImageControlPanel()
    {
        ImageControlPanel.OnInitializeExperiment += _acquisitionStateService.GetSelectedExperiment;
        ImageControlPanel.OnInitializeExperiment += DisableExperimentalPanel;

        ImageControlPanel.OnInitializeLive += _acquisitionStateService.GetLiveSettings;
        ImageControlPanel.OnInitializeTifReader += _acquisitionStateService.GetTifSettings;
        ImageControlPanel.OnFinishExperiment += EnableExperimentalPanel;

    }

    public void EnableExperimentalPanel(object? sender, EventArgs e)
    {
        IsEnableExperimentalPanel = true;
    }

    public void DisableExperimentalPanel(object? sender, ILifetimeScope scope)
    {
        IsEnableExperimentalPanel = false;
    }

    public async void InitializeEquipment()
    {
        await _communicationManager.CancelMeasurementProgramAsync();

        var detectors = await _communicationManager.ListAvailableDetectorsAsync();
        foreach (var detector in detectors)
        {
            AvailableDetectors.Add(detector);
        }

        var eq = await _communicationManager.ListAvailableEquipmentAsync();
        AvailableSources = _equipmentState.ParseAvailableLightSources(eq);
        AvailableFilterWheels = _equipmentState.ParseAvailableFilterWheels(eq);
        AvailableRobots = _equipmentState.ParseAvailableRobots(eq);

        _stageControl.InitializeStageInfo();

        AvailableDetectors.ForEach(x => x.IsEnabled = true);    
        
        // Initialize ExperimentManager with equipment
        _experimentManager.InitializeEquipment(
            AvailableSources, 
            AvailableFilterWheels, 
            AvailableRobots, 
            AvailableDetectors);

        // Set selected acquisition
        if (_SystemDefinedSettings.Acquisitions.Count > 0)
        {
            SelectedAcquisition = _SystemDefinedSettings.Acquisitions[0];
            _acquisitionStateService.SelectedAcquisition = SelectedAcquisition;
        }
        _acquisitionStateService.SelectedExperiment = SelectedExperiment;
    }


    partial void OnSelectedAcquisitionChanged(AcquisitionSettingsViewModel? value)
    {
        _acquisitionStateService.SelectedAcquisition = value;
    }

    public void OpenExperimentPanel(object sender)
    {
        ExperimentalPanelViewModel experiment = (ExperimentalPanelViewModel)sender;
    }


    public void CopyAcquisition()
    {
        _experimentManager.IncrementNumAcquisition();
        if (SelectedAcquisition != null)
        {
            AcquisitionSettings new_acq = AcquisitionSettingsFactory.CloneWithName(
                $"Acquisition {_experimentManager.GetNumAcquisition()}", 
                SelectedAcquisition.AcquisitionSettings);

            new_acq.AcquisitionSettingsID = _experimentManager.GetNumAcquisition();
            AcquisitionSettingsViewModel new_acq_model = new AcquisitionSettingsViewModel(new_acq);
            new_acq_model.AcquisitionID = _experimentManager.GetNumAcquisition();



            SystemDefinedSettings.Acquisitions.Add(new_acq_model);

        }
    }



    public void RemoveAcquisition()
    {
        if (SystemDefinedSettings.Acquisitions.Count > 1)
        {
            _experimentManager.IncrementNumAcquisition();
            var toRemove = SystemDefinedSettings.Acquisitions
            .FirstOrDefault(a => a == SelectedAcquisition);

            if (toRemove != null)
            {
                SystemDefinedSettings.Acquisitions.Remove(toRemove);
            }

            SelectedAcquisition = SystemDefinedSettings.Acquisitions[SystemDefinedSettings.Acquisitions.Count - 1];
        }

    }

    public void AddExperiment()
    {
        _experimentManager.AddExperiment();
    }

    public async Task SaveExperiment()
    {
        await _experimentManager.SaveExperiment();
    }

    public void LoadExperiment()
    {
        _experimentManager.LoadExperiment();
    }

    public async void ParseLoadedExperiment(string program)
    {
        _experimentManager.ParseLoadedExperiment(program);
    }



    public void RemoveExperiment()
    {
        _experimentManager.RemoveExperiment();
    }
    
    public Action<object, RoutedEventArgs>? OnProgramStorageRequested { get; internal set; }
    public Action<object>? OnProgramLoadRequested { get; internal set; }
    
    
    public override void Dispose()
    {
        _experimentManager.Dispose();
    }
}

public partial class SystemDefinedSettingsViewModel : ViewModelBase
{
    [ObservableProperty] ObservableCollection<AcquisitionSettingsViewModel> _Acquisitions = new();
    public List<Robots> Robots { get; set; } = new();  
    public SystemDefinedSettingsViewModel(ObservableCollection<AcquisitionSettingsViewModel> acquisitions, List<Robots> robots)
    {
        this.Acquisitions = acquisitions;
        this.Robots = robots;
    }
    public SystemDefinedSettingsViewModel(ObservableCollection<AcquisitionSettings> acquisitionSettings, List<Robots> robots)
    {
        this.Acquisitions = new ObservableCollection<AcquisitionSettingsViewModel>( acquisitionSettings.Select(x => new AcquisitionSettingsViewModel(x)));
        this.Robots = robots;

    }

    public SystemDefinedSettingsViewModel()
    {

    }
}

