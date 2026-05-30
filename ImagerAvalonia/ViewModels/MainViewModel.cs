using Autofac;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Exceptions;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
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
    public Action<object, RoutedEventArgs>? OnProgramStorageRequested { get; internal set; }
    public Action<object>? OnProgramLoadRequested { get; internal set; } 

    private int num_acquisition = 1;
    private AcquisitionSettings? DefaultAcquisition;


    [ObservableProperty] bool _IsExperimentEnabled = false;
    [ObservableProperty] bool _IsEnableExperimentalPanel = true;

    [ObservableProperty] public ObservableCollection<AcquisitionSettingsViewModel> _Acquisitions = new();
    [ObservableProperty] public ObservableCollection<ExperimentalPanelViewModel> _Experiments = new();


    [ObservableProperty] SourcesViewModel? _SelectedSource;
    [ObservableProperty] DetectorEquipmentViewModel? _SelectedDetector;
    [ObservableProperty] AcquisitionSettingsViewModel? _SelectedAcquisition;
    [ObservableProperty] ExperimentalPanelViewModel? _SelectedExperiment;
    [ObservableProperty] SystemDefinedSettingsViewModel _SystemDefinedSettings;

    public List<MovableComponent> AvailableFilterWheels = new();
    public List<Source> AvailableSources = new();
    public List<Robots> AvailableRobots = new();
    public List<DetectorEquipment> AvailableDetectors = new List<DetectorEquipment> { };

    private readonly IStageControl _stageControl;
    private readonly SmartProcessingRegisterViewModel _processingViewModel;
    private readonly AcquisitionStateService _acquisitionStateService;
    private readonly EquipmentState _equipmentState;





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
        DefaultAcquisition = AcquisitionSettingsFactory.FromComponents( "NewAcq", AvailableSources, AvailableFilterWheels, AvailableDetectors);
        AcquisitionSettingsViewModel init_acquisition = new AcquisitionSettingsViewModel(DefaultAcquisition);



        init_acquisition.AcquisitionID = num_acquisition;
        DefaultAcquisition.AcquisitionSettingsID = num_acquisition;
        SystemDefinedSettings.Acquisitions.Add( init_acquisition );
        SystemDefinedSettings.Robots = AvailableRobots;
        SelectedAcquisition = init_acquisition;
        _acquisitionStateService.SelectedAcquisition = SelectedAcquisition;
        _acquisitionStateService.SelectedExperiment = SelectedExperiment;
    }


    partial void OnSelectedAcquisitionChanged(AcquisitionSettingsViewModel? value)
    {
        _acquisitionStateService.SelectedAcquisition = value;
    }

    partial void OnSelectedExperimentChanged(ExperimentalPanelViewModel? value)
    {
        _acquisitionStateService.SelectedExperiment = value;
    }  


    public void OpenExperimentPanel(object sender)
    {
        ExperimentalPanelViewModel experiment = (ExperimentalPanelViewModel)sender;
    }


    public void CopyAcquisition()
    {
        num_acquisition++;
        if (SelectedAcquisition != null)
        {
            AcquisitionSettings new_acq = AcquisitionSettingsFactory.CloneWithName($"Acquisition {num_acquisition}", SelectedAcquisition.AcquisitionSettings);

            new_acq.AcquisitionSettingsID = num_acquisition;
            AcquisitionSettingsViewModel new_acq_model = new AcquisitionSettingsViewModel(new_acq);
            new_acq_model.AcquisitionID = num_acquisition;


            SystemDefinedSettings.Acquisitions.Add(new_acq_model);

        }
    }



    public void RemoveAcquisition()
    {
        if (SystemDefinedSettings.Acquisitions.Count > 1)
        {
            num_acquisition++;
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

        ExperimentalPanelViewModel exp = new ExperimentalPanelViewModel(SystemDefinedSettings, _stageControl);


        List<string?> exp_names = Experiments.Select(x => x.ExperimentName).ToList();
        string exp_name = "Experiment ";
        int num_experiment = 1;

        while (exp_names.Contains($"{exp_name}{num_experiment}"))
        {
            num_experiment++;
        }
        exp.ExperimentName = $"{exp_name}{num_experiment}";
        Experiments.Add(exp);
    }

    public async Task SaveExperiment()
    {
        var experimentSerializer = new ExperimentSerialization();
        if (SelectedExperiment != null)
        {
            try
            {
                JObject measurement_program = experimentSerializer.SerializeExperiment(SelectedExperiment, _processingViewModel);
                OnProgramStorageRequested?.Invoke(measurement_program.ToString(Newtonsoft.Json.Formatting.None), new RoutedEventArgs());
            }
            catch (Exception ex) {  
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    await ExceptionWindowHandler.ShowDialogAsync(
                        "Error", ex.Message, ex.StackTrace, desktop.MainWindow);
                }
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
        JToken json_program;

        try
        {
            json_program = JToken.Parse(program);
        }
        catch (Exception ex)
        {
            await ShowExceptionDialogAsync("Error in parsing experiment. Invalid .imag file", ex);
            return;
        }

        try
        {
            var acquisitionmodels = JsonConvert.DeserializeObject<List<AcquisitionSettingsDeserializationModel>>(
                program, new DefinedDetectionsConverter());

            acquisitionmodels.ForEach(x =>
            {
                int id = 0;
                string previous_name = x.Name;

                while (SystemDefinedSettings.Acquisitions.Select(acq => acq.Name).Any(y => y == x.Name))
                {
                    x.Name = $"{x.Name}_{id}";
                    experimentSerializer.AcquisitionMaps.Add(previous_name, x.Name);
                    id++;
                }

                var defaultacquisition = AcquisitionSettingsFactory.FromComponents(x.Name, AvailableSources, AvailableFilterWheels, AvailableDetectors);
                var modified_acquisition = new AcquisitionSettingsViewModel(
                    AcquisitionSettingsFactory.CopyFromDeserializedModel(defaultacquisition, x));

                SystemDefinedSettings.Acquisitions.Add(modified_acquisition);
            });
        }
        catch (Exception ex)
        {
            await ShowExceptionDialogAsync("Error in loading acquisitions", ex);
        }

        var expVM = new ExperimentalPanelViewModel(SystemDefinedSettings, _stageControl);

        try
        {
            experimentSerializer.SetExperiment(expVM);
            NodeBase exp_nodes = experimentSerializer.GetDeserializedExperiment(json_program, SystemDefinedSettings);
            expVM.SetExpNodes(new ObservableCollection<NodeBase>() { exp_nodes });
        }
        catch (Exception ex)
        {
            await ShowExceptionDialogAsync("Error in loading experiment tree", ex);
            return;
        }

        List<string?> exp_names = Experiments.Select(x => x.ExperimentName).ToList();
        string exp_name = "Experiment ";
        int num_experiment = 1;

        while (exp_names.Contains($"{exp_name}{num_experiment}"))
        {
            num_experiment++;
        }
        expVM.ExperimentName = $"{exp_name}{num_experiment}";
        Experiments.Add(expVM);
    }

    private static async Task ShowExceptionDialogAsync(string title, Exception ex)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow;
            string message = ex.Message;

            await ExceptionWindowHandler.ShowDialogAsync(title, message, ex.StackTrace, mainWindow);
        }
    }


    public void RemoveExperiment()
    {
        if (Experiments.Count > 0 && SelectedExperiment!=null)
        {
            SelectedExperiment.Dispose();
            Experiments.Remove(SelectedExperiment);
        }
    }
    public override void Dispose()
    {

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







