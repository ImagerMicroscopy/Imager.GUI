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

    /// <summary>
    /// The session currently acquiring, if any. The workspace releases its reference when
    /// the session stops or finishes; Experiment sessions stay alive afterwards, owned by
    /// the UI tab that shows their data.
    /// </summary>
    public AcquisitionSession? ActiveSession { get; private set; }
    public ImageHandler? ActiveImageHandler => ActiveSession?.Handler;
    public IStorageProvider? ActiveStorageProvider => ActiveSession?.StorageProvider;

    /// <summary>Raised when a live or experiment session starts acquiring.</summary>
    public event EventHandler<AcquisitionSession>? SessionStarted;
    /// <summary>Raised when the user stops a session.</summary>
    public event EventHandler<AcquisitionSession>? SessionStopped;
    /// <summary>
    /// Raised when an experiment run ends (completed or stopped), after the measurement
    /// file has been finalized and reopened for reading.
    /// </summary>
    public event EventHandler<AcquisitionSession>? SessionFinished;

    public bool IsLiveEnabled { get; private set; }
    public bool IsExperimentEnabled { get; private set; }

    /// <summary>How long to wait after closing the write stream before reopening it for reading.</summary>
    internal TimeSpan StorageReleaseDelay { get; set; } = TimeSpan.FromSeconds(2);

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
        CurrentState = WorkspaceState.Acquiring;

        try {
            await RunLiveAsync(selectedDetection);
        } catch (Exception) {
            CurrentState = WorkspaceState.Idle;
            await StopLiveInternalAsync();
            throw;
        }
    }

    private async Task RunLiveAsync(DefinedDetection selectedDetection) {
        var scope = _lifetimeScope.BeginLifetimeScope();
        AcquisitionSession? session = null;
        try {
            var storageProvider = scope.Resolve<IStorageProvider>();


            var experiment =  new DoTimesElement() { ElementId = Guid.NewGuid().ToString(), NTotal = 10000000,
                Elements = new List<MeasurementElementBase>() {
                    new DetectionElement() { ElementId = Guid.NewGuid().ToString(),DetectionNames =
                    new List<string>(){ selectedDetection.Name } }
                }
            };

            var acq_det_pairs = ResolveAcqDetPairs(selectedDetection);
            storageProvider.SetAcqDetPairs(acq_det_pairs);

            var smartprograms =  new SmartProgramRegistry();
            var detections =     new Dictionary<string,DetectionParams>() { { selectedDetection.Name, selectedDetection.Settings } };
            var handler = new ImageHandler(storageProvider, _logger, _connectionHandler, _communicationManager);

            session = new AcquisitionSession(
                AcquisitionSessionKind.Live, scope, storageProvider, handler, new CancellationTokenSource());
            ActiveSession = session;

            var program = new MeasurementProgram(experiment,
                detections
            );

            SessionStarted?.Invoke(this, session);

            await session.Handler.ParseProgramAndShowData(session.Cancellation, program, smartprograms);
        } finally {
            // Live data is not saved, so the session has no reason to outlive live mode.
            // Disposed here - after the run has unwound - rather than in StopLiveInternalAsync,
            // whose cancellation-token disposal would race the run still using the token.
            if (session != null) session.Dispose();
            else scope.Dispose();
        }
    }

    public async Task StopLiveAsync() {
        if (CurrentState != WorkspaceState.Acquiring) return;
        CurrentState = WorkspaceState.Idle;
        await StopLiveInternalAsync();
    }

    private async Task StopLiveInternalAsync() {
        IsLiveEnabled = false;
        var session = ActiveSession;
        ActiveSession = null;

        if (session == null) return;

        if (!session.IsDisposed) session.Cancellation.Cancel();
        try {
            await _communicationManager.CancelMeasurementProgramAsync();
        } catch {}

        SessionStopped?.Invoke(this, session);
        // The session itself is disposed by RunLiveAsync once the run has unwound.
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

        var scope = _lifetimeScope.BeginLifetimeScope();
        var storageProvider = scope.Resolve<IStorageProvider>();
        var session = new AcquisitionSession(
            AcquisitionSessionKind.Experiment,
            scope,
            storageProvider,
            new ImageHandler(storageProvider, _logger, _connectionHandler, _communicationManager),
            new CancellationTokenSource());

        var sessionStarted = false;
        try
        {
            var acq_det_pairs = detections.Select(x => ResolveAcqDetPairs(x))
                .SelectMany(list => list)
                .Distinct()
                .ToList();

            var program = new MeasurementProgram(
                    experiment,
                    detections.ToDictionary(d => d.Name,
                    d => d.Settings)
            );

            storageProvider.SetEnabledStorage(isstorageenabeld);
            storageProvider.SetMeasurementProgram(fullEquipmentStateJson);
            storageProvider.SetMaxFrameNumber((int)experiment.CountTotalDetections());
            storageProvider.SetAcqDetPairs(acq_det_pairs);
            storageProvider.SetStoragePath(storagepath);

            ActiveSession = session;
            SessionStarted?.Invoke(this, session);
            sessionStarted = true;
            CurrentState = WorkspaceState.Acquiring;

            await session.Handler.ParseProgramAndShowData(session.Cancellation, program, _smartProgramRegistry);
        }
        catch (Exception)
        {
            try
            {
                await _communicationManager.CancelMeasurementProgramAsync();
            } catch {}
            throw;
        }
        finally
        {
            IsExperimentEnabled = false;
            CurrentState = WorkspaceState.Idle;
            if (ReferenceEquals(ActiveSession, session)) ActiveSession = null;

            if (!sessionStarted)
            {
                // The session never reached a tab (startup failure) - nothing will dispose it.
                session.Dispose();
            }
            else
            {
                // Finalize the measurement file so the owning tab can browse what was
                // saved, whether the run completed or was stopped by the user.
                session.StorageProvider.CloseReadWriteStream();
                await Task.Delay(StorageReleaseDelay);
                if (isstorageenabeld) {
                    session.StorageProvider.OpenReadStream();
                }
            }

            SessionFinished?.Invoke(this, session);
        }
    }

    public async Task StopExperimentAsync() {
        if (!IsExperimentEnabled) return;
        IsExperimentEnabled = false;
        CurrentState = WorkspaceState.Idle;

        var session = ActiveSession;
        ActiveSession = null;
        session?.Cancellation.Cancel();

        try {
            await _communicationManager.CancelMeasurementProgramAsync();
        } catch {}

        if (session != null) {
            // The workspace releases its reference; the session itself stays alive,
            // owned by the tab that shows the measurement.
            SessionStopped?.Invoke(this, session);
        }
    }

    public async Task LoadHistoricalDataAsync(string directoryPath) {
        if (CurrentState == WorkspaceState.Acquiring)
            throw new InvalidOperationException("Cannot load data while acquiring.");
    }



    public void Dispose() {
        ActiveSession?.Cancellation.Dispose();
        ActiveSession?.Scope.Dispose();
    }
}
