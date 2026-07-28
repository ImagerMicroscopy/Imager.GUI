using Autofac;
using AvaloniaEdit.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Workspace;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
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

    private readonly ExperimentManager _experimentManager;
    private readonly EquipmentWorkspace _equipmentContext;
    [ObservableProperty] bool _IsExperimentEnabled = false;
    [ObservableProperty] bool _IsEnableExperimentalPanel = true;

    [ObservableProperty] public ObservableCollection<AcquisitionSettingsViewModel> _Acquisitions = new();

    [ObservableProperty] SourcesEquipmentViewModel? _SelectedSource;
    [ObservableProperty] DetectorEquipmentViewModel? _SelectedDetector;
    [ObservableProperty] AcquisitionSettingsViewModel? _SelectedAcquisition;
    [ObservableProperty] GlobalDefinedSettingsViewModel _SystemDefinedSettings;

    public List<MovableComponentModel> AvailableFilterWheels = new();
    public List<Source> AvailableSources = new();
    public List<RobotModel> AvailableRobots = new();
    public List<DetectorEquipmentModel> AvailableDetectors = new List<DetectorEquipmentModel> { };

    private readonly IStageControl _stageControl;
    private readonly SmartProcessingRegisterViewModel _processingViewModel;
    private readonly ImagerWorkspace _imagerWorkspace;

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
        GlobalDefinedSettingsViewModel userDefinedAcquisitions, 
        SmartProcessingRegisterViewModel processViewModel,
        ExperimentManager experimentManager,
        EquipmentWorkspace equipmentContext,
        ImagerWorkspace imagerWorkspace)
    {
        _SystemDefinedSettings = userDefinedAcquisitions;
        _stageControl = stageControl;
        _communicationManager = ImagerCommunicationManager.Instance;
        _processingViewModel = processViewModel;
        _experimentManager = experimentManager;
        _equipmentContext = equipmentContext;
        _imagerWorkspace = imagerWorkspace;
        // Create the ExperimentManager to handle experiment collection logic


        // Forward storage/load callbacks from ExperimentManager to MainViewModel
        _experimentManager.OnProgramStorageRequested = (program) => 
            OnProgramStorageRequested?.Invoke(program);
        _experimentManager.OnProgramLoadRequested = sender => 
            OnProgramLoadRequested?.Invoke(sender);
        _experimentManager.ExperimentLoaded += ExperimentManager_ExperimentLoaded;

        _experimentManager.SelectedExperimentChanged += (sender, args) =>
        {
            OnPropertyChanged(nameof(SelectedExperiment));
        };

        _SystemDefinedSettings.SetImagerWorkSpace(_imagerWorkspace);
        _SystemDefinedSettings.SetExperimentManager(_experimentManager);

        ImageControlPanel = imagePanel;
        ImageControlPanel.SetAvailableAcquisitions(SystemDefinedSettings);
    }


    public void InitializeImageControlPanel()
    {
        ImageControlPanel.OnInitializeExperiment += DisableExperimentalPanel;
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

    public void ApplyEquipment(EquipmentInitResult result)
    {
        AvailableDetectors.AddRange(result.Detectors);
        AvailableSources = result.Sources;
        AvailableFilterWheels = result.FilterWheels;
        AvailableRobots = result.Robots;
        AvailableDetectors.ForEach(x => x.IsEnabled = true);

        _equipmentContext.Initialize(
            _imagerWorkspace,
            AvailableSources,
            AvailableFilterWheels,
            AvailableRobots,
            AvailableDetectors);

        var initAcquisition = new AcquisitionSettingsViewModel(
            _equipmentContext.DefaultAcquisition,
            _imagerWorkspace,
            _experimentManager)
        {
            AcquisitionID = 1
        };
        SystemDefinedSettings.Acquisitions.Add(initAcquisition);
        SystemDefinedSettings.Robots = AvailableRobots;

        if (_SystemDefinedSettings.Acquisitions.Count > 0)
        {
            SelectedAcquisition = _SystemDefinedSettings.Acquisitions[0];
        }
    }

    partial void OnSelectedAcquisitionChanged(AcquisitionSettingsViewModel? value)
    {
        _experimentManager.SelectedDetection = value.DetectionSettings;
    }

    private void ExperimentManager_ExperimentLoaded(ExperimentalPanelViewModel exp, List<AcquisitionSettingsViewModel> acq)
    {
        Acquisitions.AddRange(acq);
        Experiments.Add(exp);
        SelectedExperiment = exp;
    }


    public void CopyAcquisition()
    {
        _experimentManager.IncrementNumAcquisition();
        if (SelectedAcquisition != null)
        {
            var copiedDetection = SelectedAcquisition.DetectionSettings.Clone();
        
            AcquisitionSettingsViewModel new_acq = new AcquisitionSettingsViewModel(
                $"Acquisition {_experimentManager.GetNumAcquisition()}",
                copiedDetection.Settings.Irradiation,
                copiedDetection.Settings.MovableComponents,
                copiedDetection.Settings.Detectors,
                _imagerWorkspace,
                _experimentManager);

            SystemDefinedSettings.Acquisitions.Add(new_acq);

        }
    }



    public void RemoveAcquisition()
    {
        if (SystemDefinedSettings.Acquisitions.Count > 1)
        {
            _experimentManager.IncrementNumAcquisition();
            var toRemove = SystemDefinedSettings.Acquisitions
            .FirstOrDefault(a => a == SelectedAcquisition);
            var toRemoveInd = SystemDefinedSettings.Acquisitions.IndexOf(toRemove);
            if (toRemoveInd != 0)
            {
                SelectedAcquisition = SystemDefinedSettings.Acquisitions[toRemoveInd - 1];
            }
            else
            {
                SelectedAcquisition = SystemDefinedSettings.Acquisitions[toRemoveInd + 1];
            }

            if (toRemove != null)
            {
                SystemDefinedSettings.Acquisitions.Remove(toRemove);
            }

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
        await _experimentManager.ParseLoadedExperiment(program);
    }

    public void RemoveExperiment()
    {
        _experimentManager.RemoveExperiment();
    }
    

    public Action<object>? OnProgramStorageRequested { get; internal set; }
    public Action<object>? OnProgramLoadRequested { get; internal set; }
    
    
    public override void Dispose()
    {
        _experimentManager.Dispose();
    }
}


