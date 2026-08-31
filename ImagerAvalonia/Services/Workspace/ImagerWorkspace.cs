using Autofac;
using ImagerAvalonia.Services.ImagerModels.MeasurementElementsModels;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Storage;
using ImagerAvalonia.Services.Workspace.SmartProgramWorkspace;
using ImagerAvalonia.Utils;

using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ImagerAvalonia.Services.Workspace;

public enum WorkspaceState {
    Idle,
    Acquiring,
    Processing,
    Error
}

public class ImagerWorkspace : IDisposable {
    private readonly ILifetimeScope _lifetimeScope;
    private readonly ILogger _logger;
    private readonly IImagerConnectionHandler _connectionHandler;
    private readonly IImagerCommunicationManager _communicationManager;
    private readonly SmartProgramRegistry _smartProgramRegistry;


    public WorkspaceState CurrentState { get; private set; } = WorkspaceState.Idle;
    public event EventHandler<WorkspaceState>? StateChanged;

    public ImageHandler? ActiveImageHandler { get; private set; }
    public IStorageProvider? ActiveStorageProvider { get; private set; }

    public event EventHandler<ILifetimeScope>? LiveScopeCreated;
    public event EventHandler<ILifetimeScope>? ExperimentScopeCreated;
    public event EventHandler? HandlerDestroyed;
    public event EventHandler? ExperimentFinished;

    public bool IsLiveEnabled { get; private set; }
    public bool IsExperimentEnabled { get; private set; }

    private CancellationTokenSource? _cancelToken;
    private ILifetimeScope? _activeScope;

    public ImagerWorkspace(

        ILifetimeScope lifetimeScope,
        ILoggerFactory loggerFactory,
        IImagerConnectionHandler connectionHandler,
        IImagerCommunicationManager communicationManager,
        SmartProgramRegistry smartProgramRegistry) {

        _lifetimeScope = lifetimeScope;
        _logger = loggerFactory.CreateLogger("ImagerWorkspace");
        _connectionHandler = connectionHandler;
        _communicationManager = communicationManager;
        _smartProgramRegistry = smartProgramRegistry;   


    }

    public async Task ToggleLiveAsync(DefinedDetection detection) {
        if (IsLiveEnabled) await StopLiveAsync();
        else await StartLiveAsync(detection);
    }

    private List<Tuple<string,string>> ResolveAcqDetPairs(DefinedDetection selectedDetection)
    {
        var acq_det_pairs = selectedDetection.Settings.Detectors
            .Where(x => x.IsEnabled)
            .Select(x => new Tuple<string, string>(
                 selectedDetection.Name,
                x.Detectorname
            ))
            .ToList();
        return acq_det_pairs;
    }

    public async Task StartLiveAsync(DefinedDetection selectedDetection) {
        if (IsLiveEnabled || IsExperimentEnabled) return;
        IsLiveEnabled = true;
        //_acquisitionState.SetLiveState();
        CurrentState = WorkspaceState.Acquiring;
        _activeScope = _lifetimeScope.BeginLifetimeScope();
        ActiveStorageProvider = _activeScope.Resolve<IStorageProvider>();


        var experiment =  new DoTimesElement() { ElementId = Guid.NewGuid().ToString(), NTotal = 10000000,
            Elements = new List<MeasurementElementBase>() {
                new DetectionElement() { ElementId = Guid.NewGuid().ToString(),DetectionNames =
                new List<string>(){ selectedDetection.Name } }
            }
        };

        var acq_det_pairs = ResolveAcqDetPairs(selectedDetection);
        ActiveStorageProvider.SetAcqDetPairs(acq_det_pairs);
        
        var smartprograms =  new SmartProgramRegistry();
        var detections =     new Dictionary<string,DetectionParams>() { { selectedDetection.Name, selectedDetection.Settings } };
        ActiveImageHandler = new ImageHandler(ActiveStorageProvider, _logger, _connectionHandler);
        _cancelToken =       new CancellationTokenSource();


        var program = new MeasurementProgram(experiment,
            detections
        );

        LiveScopeCreated?.Invoke(this, _activeScope);

        try {
            await ActiveImageHandler.ParseProgramAndShowData(_cancelToken, program, smartprograms);
        } catch (Exception) {

            CurrentState = WorkspaceState.Idle;
            await StopLiveInternalAsync();
            throw;
        }
    }

    public async Task StopLiveAsync() {

        if (CurrentState!=WorkspaceState.Acquiring) return;
        CurrentState = WorkspaceState.Idle;
        await StopLiveInternalAsync();
    }

    private async Task StopLiveInternalAsync() {
        IsLiveEnabled = false;
        _cancelToken?.Cancel();

        try {
            await _communicationManager.CancelMeasurementProgramAsync();
        } catch {}

        HandlerDestroyed?.Invoke(this, EventArgs.Empty);
        
        CleanUpActiveSession();
    }

    public async Task ToggleExperimentAsync(MeasurementElementBase experiment,
        string storagepath,
        bool isstorageenabeld,
        List<DefinedDetection> detections,
        string fullEquipmentStateJson) {
        if (IsExperimentEnabled) await StopExperimentAsync();
        else await StartExperimentAsync(experiment,
            storagepath,
            isstorageenabeld,
            detections,
            fullEquipmentStateJson);
    }

    public async Task StartExperimentAsync(
        MeasurementElementBase experiment,
        string storagepath,
        bool isstorageenabeld,
        List<DefinedDetection> detections,
        string fullEquipmentStateJson
        ) {
        if (IsLiveEnabled || IsExperimentEnabled) return;
        IsExperimentEnabled = true;

        _activeScope = _lifetimeScope.BeginLifetimeScope();
        ActiveStorageProvider = _activeScope.Resolve<IStorageProvider>();
        var acq_det_pairs = detections.Select(x => ResolveAcqDetPairs(x))
            .SelectMany(list => list)
            .Distinct()
            .ToList();

        var program = new MeasurementProgram(
                experiment,
                detections.ToDictionary(d => d.Name,
                d => d.Settings)
        );

        ActiveStorageProvider.SetEnabledStorage(isstorageenabeld);
        ActiveStorageProvider.SetMeasurementProgram(fullEquipmentStateJson);
        ActiveStorageProvider.SetMaxFrameNumber((int)experiment.CountTotalDetections());

        ActiveStorageProvider.SetAcqDetPairs(acq_det_pairs);
        ActiveStorageProvider.SetStoragePath(storagepath);
        ActiveStorageProvider.OpenWriteStream();

        ActiveImageHandler = new ImageHandler(ActiveStorageProvider, _logger, _connectionHandler);
        _cancelToken = new CancellationTokenSource();

        ExperimentScopeCreated?.Invoke(this, _activeScope);
        CurrentState = WorkspaceState.Acquiring;

        try
        {
            bool success = await ActiveImageHandler.ParseProgramAndShowData(_cancelToken, program, _smartProgramRegistry);
            if (success) {
                ExperimentFinished?.Invoke(this, EventArgs.Empty);
                CurrentState = WorkspaceState.Idle;
            }
        } catch (Exception) {
            CurrentState = WorkspaceState.Idle;
            IsExperimentEnabled = false;
            ExperimentFinished?.Invoke(this, EventArgs.Empty);
            try
            {
                await _communicationManager.CancelMeasurementProgramAsync();
            } catch {}
            throw;
        } finally {
            IsExperimentEnabled = false;
            CurrentState = WorkspaceState.Idle;
            ExperimentFinished?.Invoke(this, EventArgs.Empty);

            if (ActiveStorageProvider != null) {
                ActiveStorageProvider.CloseReadWriteStream();
                await Task.Delay(2000);
                if(isstorageenabeld) {
                    ActiveStorageProvider.OpenReadStream();
                }
            }
        }
    }

    public async Task StopExperimentAsync() {
        if (!IsExperimentEnabled) return;
        IsExperimentEnabled = false;
        _cancelToken?.Cancel();

        try {
            await _communicationManager.CancelMeasurementProgramAsync();
        } catch {}

        HandlerDestroyed?.Invoke(this, EventArgs.Empty);
        
        CleanUpActiveSession();
    }

    private void CleanUpActiveSession() {
        ActiveImageHandler = null;
        ActiveStorageProvider = null;
        _activeScope?.Dispose();
        _activeScope = null;
    }

    public async Task LoadHistoricalDataAsync(string directoryPath) {
        if (CurrentState == WorkspaceState.Acquiring)
            throw new InvalidOperationException("Cannot load data while acquiring.");
    }



    public void Dispose() {
        _cancelToken?.Dispose();
        _activeScope?.Dispose();
    }
}
