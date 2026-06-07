using System;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using ImagerAvalonia.Data;
using ImagerAvalonia.Data.Measurements;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Utils;
using Microsoft.Extensions.Logging;

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
    private readonly AcquisitionStateService _acquisitionState;

    // The three specialized sub-managers
    public ExperimentBuilder ExperimentBuilder { get; }
    public AcquisitionEngine AcquisitionEngine { get; }
    public DataWorkspace DataWorkspace { get; }

    public WorkspaceState CurrentState { get; private set; } = WorkspaceState.Idle;
    public event EventHandler<WorkspaceState>? StateChanged;

    public ImageHandler? ActiveImageHandler { get; private set; }
    public IStorageProvider? ActiveStorageProvider { get; private set; }
    public IExperimentSerialization? ActiveExperimentSerializer { get; private set; }

    public event EventHandler<ILifetimeScope>? LiveScopeCreated;
    public event EventHandler<ILifetimeScope>? ExperimentScopeCreated;
    public event EventHandler? HandlerDestroyed;
    public event EventHandler? ExperimentFinished;

    public bool IsLiveEnabled { get; private set; }
    public bool IsExperimentEnabled { get; private set; }

    private CancellationTokenSource? _cancelToken;
    private ILifetimeScope? _activeScope;

    public ImagerWorkspace(
        ExperimentBuilder experimentBuilder, 
        AcquisitionEngine acquisitionEngine, 
        DataWorkspace dataWorkspace,
        ILifetimeScope lifetimeScope,
        ILoggerFactory loggerFactory,
        IImagerConnectionHandler connectionHandler,
        IImagerCommunicationManager communicationManager,
        AcquisitionStateService acquisitionState) {
        
        ExperimentBuilder = experimentBuilder;
        AcquisitionEngine = acquisitionEngine;
        DataWorkspace = dataWorkspace;
        _lifetimeScope = lifetimeScope;
        _logger = loggerFactory.CreateLogger("ImagerWorkspace");
        _connectionHandler = connectionHandler;
        _communicationManager = communicationManager;
        _acquisitionState = acquisitionState;

        AcquisitionEngine.MeasurementStarted += (s, e) => SetState(WorkspaceState.Acquiring);
        AcquisitionEngine.MeasurementCompleted += (s, e) => SetState(WorkspaceState.Idle);
        AcquisitionEngine.ImageReceived += (s, img) => DataWorkspace.AddImage(img);
    }

    public async Task ToggleLiveAsync() {
        if (IsLiveEnabled) await StopLiveAsync();
        else await StartLiveAsync();
    }

    public async Task StartLiveAsync() {
        if (IsLiveEnabled || IsExperimentEnabled) return;
        IsLiveEnabled = true;
        _acquisitionState.SetLiveState();

        _activeScope = _lifetimeScope.BeginLifetimeScope();
        ActiveStorageProvider = _activeScope.Resolve<IStorageProvider>();
        ActiveExperimentSerializer = _activeScope.Resolve<IExperimentSerialization>();

        ActiveImageHandler = new ImageHandler(ActiveStorageProvider, _logger, _connectionHandler);
        _cancelToken = new CancellationTokenSource();

        LiveScopeCreated?.Invoke(this, _activeScope);

        try {
            await ActiveImageHandler.ParseProgramAndShowData(_cancelToken);
        } catch (Exception) {
            await StopLiveInternalAsync();
            throw;
        }
    }

    public async Task StopLiveAsync() {
        if (!IsLiveEnabled) return;
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
        _acquisitionState.SetIdleState();
    }

    public async Task ToggleExperimentAsync() {
        if (IsExperimentEnabled) await StopExperimentAsync();
        else await StartExperimentAsync();
    }

    public async Task StartExperimentAsync() {
        if (IsLiveEnabled || IsExperimentEnabled) return;
        IsExperimentEnabled = true;
        _acquisitionState.SetExperimentState();

        _activeScope = _lifetimeScope.BeginLifetimeScope();
        ActiveStorageProvider = _activeScope.Resolve<IStorageProvider>();
        ActiveExperimentSerializer = _activeScope.Resolve<IExperimentSerialization>();
        
        ActiveImageHandler = new ImageHandler(ActiveStorageProvider, _logger, _connectionHandler);
        _cancelToken = new CancellationTokenSource();

        ExperimentScopeCreated?.Invoke(this, _activeScope);

        try {
            bool success = await ActiveImageHandler.ParseProgramAndShowData(_cancelToken);
            if (success) {
                ExperimentFinished?.Invoke(this, EventArgs.Empty);
            }
        } catch (Exception) {
            IsExperimentEnabled = false;
            try {
                await _communicationManager.CancelMeasurementProgramAsync();
            } catch {}
            _acquisitionState.SetIdleState();
            throw;
        } finally {
            IsExperimentEnabled = false;
            if (ActiveStorageProvider != null) {
                ActiveStorageProvider.CloseReadWriteStream();
                await Task.Delay(2000);
                ActiveStorageProvider.OpenReadStream();
            }
            _acquisitionState.SetIdleState();
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
        _acquisitionState.SetIdleState();
    }

    private void CleanUpActiveSession() {
        ActiveImageHandler = null;
        ActiveStorageProvider = null;
        ActiveExperimentSerializer = null;
        _activeScope?.Dispose();
        _activeScope = null;
    }

    public async Task LoadHistoricalDataAsync(string directoryPath) {
        if (CurrentState == WorkspaceState.Acquiring)
            throw new InvalidOperationException("Cannot load data while acquiring.");
        DataWorkspace.ClearWorkspace();
    }

    private void SetState(WorkspaceState newState) {
        CurrentState = newState;
        StateChanged?.Invoke(this, newState);
    }

    public void Dispose() {
        _cancelToken?.Dispose();
        _activeScope?.Dispose();
    }
}
