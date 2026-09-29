using System.Threading.Channels;
using Autofac;
using ImagerAvalonia.Services.ImagerModels.MeasurementElementsModels;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Storage;
using ImagerAvalonia.Services.Workspace;
using ImagerAvalonia.Services.Workspace.SmartProgramWorkspace;
using ImagerAvalonia.Tests.TestData;
using ImagerAvalonia.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ImagerAvalonia.Tests.Workspace;

/// <summary>
/// Live / experiment state machine of ImagerWorkspace, with the backend replaced by a
/// scripted IImagerCommunicationManager and storage by a mock IStorageProvider.
/// </summary>
[Collection(AppContainerCollection.Name)]
public sealed class ImagerWorkspaceTests : IDisposable
{
    private readonly TestContainer _container = new();
    private readonly Mock<IImagerCommunicationManager> _comm = new();
    private readonly ImagerWorkspace _workspace;

    /// <summary>What the fake backend does when a program is started.</summary>
    private Func<ChannelWriter<MeasurementEvent>, CancellationToken, Task> _backend =
        async (w, _) => { await w.WriteAsync(new MeasurementCompletedEvent()); w.TryComplete(); };

    public ImagerWorkspaceTests()
    {
        _comm.Setup(c => c.ExecuteMeasurementProgram(
                It.IsAny<ExecuteMeasurementProgramRequest>(),
                It.IsAny<ChannelWriter<MeasurementEvent>>(),
                It.IsAny<CancellationToken>()))
            .Callback<ExecuteMeasurementProgramRequest, ChannelWriter<MeasurementEvent>, CancellationToken>(
                (request, writer, token) => _ = Task.Run(() => _backend(writer, token)));

        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());

        _workspace = new ImagerWorkspace(
            _container.Container,
            loggerFactory.Object,
            Mock.Of<IImagerConnectionHandler>(),
            _comm.Object,
            new SmartProgramRegistry())
        {
            StorageReleaseDelay = TimeSpan.Zero
        };
    }

    public void Dispose()
    {
        _workspace.Dispose();
        _container.Dispose();
    }

    private Mock<IStorageProvider> Storage => _container.StorageProvider;

    private static DefinedDetection Detection(string name = "Acq1") =>
        new() { Name = name, Settings = EquipmentFixtures.Detection() };

    private static MeasurementElementBase Program(int repeats = 3)
    {
        var loop = new DoTimesElement { ElementId = "root", NTotal = repeats };
        loop.AddChild(new DetectionElement { ElementId = "d", DetectionNames = { "Acq1" } });
        return loop;
    }

    private Task RunExperimentAsync(bool storage = true) =>
        _workspace.StartExperimentAsync(Program(), "/tmp/out", storage, new List<DefinedDetection> { Detection() }, "{}");

    private static ChannelMessage Image(ulong index) =>
        new(index, new AsyncMeasurementMessage("acquireddatamessage",
            new AcquiredData("DummyCam1", new byte[] { 1, 2, 3, 4 }, 2, 1, 0, 0.5f),
            new AcquisitionMetaData("Acq1", 0, "d", 1, new StagePosition(0, false, 1, 2, 3), "p1"),
            null!));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException();
            await Task.Delay(10);
        }
    }

    // ---------- Experiment ----------

    [Fact]
    public async Task Experiment_runs_to_completion_and_returns_to_idle()
    {
        var states = new List<WorkspaceState>();
        int finished = 0;
        _workspace.ExperimentFinished += (_, _) =>
        {
            finished++;
            states.Add(_workspace.CurrentState);
        };

        await RunExperimentAsync();

        Assert.Equal(1, finished); // used to fire two to three times per run
        Assert.Equal(new[] { WorkspaceState.Idle }, states);
        Assert.Equal(WorkspaceState.Idle, _workspace.CurrentState);
        Assert.False(_workspace.IsExperimentEnabled);
    }

    [Fact]
    public async Task Experiment_configures_storage_and_opens_it_once()
    {
        await RunExperimentAsync();

        Storage.Verify(s => s.SetEnabledStorage(true));
        Storage.Verify(s => s.SetStoragePath("/tmp/out"));
        Storage.Verify(s => s.SetMeasurementProgram("{}"));
        Storage.Verify(s => s.SetMaxFrameNumber(3));
        Storage.Verify(s => s.SetAcqDetPairs(It.Is<List<Tuple<string, string>>>(p =>
            p.Count == 1 && p[0].Item1 == "Acq1" && p[0].Item2 == "DummyCam1")));

        // Used to be opened twice (ImagerWorkspace and ImageHandler), leaking a storage.
        Storage.Verify(s => s.OpenWriteStream(), Times.Once);
        Storage.Verify(s => s.CloseReadWriteStream(), Times.Once);
        Storage.Verify(s => s.OpenReadStream(), Times.Once);
    }

    [Fact]
    public async Task Experiment_without_storage_does_not_reopen_for_reading()
    {
        await RunExperimentAsync(storage: false);

        Storage.Verify(s => s.SetEnabledStorage(false));
        Storage.Verify(s => s.CloseReadWriteStream(), Times.Once);
        Storage.Verify(s => s.OpenReadStream(), Times.Never);
    }

    [Fact]
    public async Task Experiment_sends_the_program_and_detections_to_the_backend()
    {
        ExecuteMeasurementProgramRequest? sent = null;
        _comm.Setup(c => c.ExecuteMeasurementProgram(It.IsAny<ExecuteMeasurementProgramRequest>(),
                It.IsAny<ChannelWriter<MeasurementEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<ExecuteMeasurementProgramRequest, ChannelWriter<MeasurementEvent>, CancellationToken>((r, w, t) =>
            {
                sent = r;
                _ = Task.Run(() => _backend(w, t));
            });

        await RunExperimentAsync();

        Assert.NotNull(sent);
        Assert.Equal("dotimes", sent!.Program["elementtype"]!.ToString());
        Assert.Equal(3, (int)sent.Program["ntotal"]!);
        Assert.NotNull(sent.DefinedDetections["Acq1"]);
    }

    [Fact]
    public async Task Experiment_saves_acquired_images()
    {
        _backend = async (w, _) =>
        {
            await w.WriteAsync(new MeasurementStartedEvent());
            await w.WriteAsync(new MeasurementDataEvent(new[] { Image(0), Image(1) }));
            await w.WriteAsync(new MeasurementStatusTextEvent(new[] { "done" }));
            await w.WriteAsync(new MeasurementCompletedEvent());
            w.TryComplete();
        };

        await RunExperimentAsync();

        Storage.Verify(s => s.SavePlanes(
            It.Is<List<byte[]>>(images => images.Count == 2),
            It.Is<List<TiffPlaneMetadata>>(m => m.Count == 2 && m[0].AcquisitionName == "Acq1" && m[0].Width == 2)),
            Times.Once);
    }

    private static ChannelMessage Decision(ulong index, string? payload) =>
        new(index, new AsyncMeasurementMessage("smartprogramdecisionmessage", null!, null!, payload!));

    [Fact]
    public async Task Experiment_saves_smart_program_decisions()
    {
        // Regression: decisions were collected by ProcessMessages but never stored.
        const string stop = """{"decision":"stop","programid":"p1","timestamp":3.5}""";
        const string go = """{"decision":"continue","programid":"p1","timestamp":4.0}""";
        _backend = async (w, _) =>
        {
            await w.WriteAsync(new MeasurementDataEvent(new[] { Image(0), Decision(1, stop) }));
            await w.WriteAsync(new MeasurementDataEvent(new[] { Decision(2, go) }));
            await w.WriteAsync(new MeasurementCompletedEvent());
            w.TryComplete();
        };

        await RunExperimentAsync();

        Storage.Verify(s => s.SaveDecisions(It.Is<List<string>>(d => d.SequenceEqual(new[] { stop }))));
        Storage.Verify(s => s.SaveDecisions(It.Is<List<string>>(d => d.SequenceEqual(new[] { go }))));
        Storage.Verify(s => s.SaveDecisions(It.IsAny<List<string>>()), Times.Exactly(2));
        // The decision-only batch must not produce an empty SavePlanes call.
        Storage.Verify(s => s.SavePlanes(It.IsAny<List<byte[]>>(), It.IsAny<List<TiffPlaneMetadata>>()), Times.Once);
    }

    [Fact]
    public async Task Empty_decisions_and_image_only_batches_do_not_save_decisions()
    {
        _backend = async (w, _) =>
        {
            await w.WriteAsync(new MeasurementDataEvent(new[] { Image(0), Decision(1, null) }));
            await w.WriteAsync(new MeasurementDataEvent(new[] { Image(2) }));
            await w.WriteAsync(new MeasurementCompletedEvent());
            w.TryComplete();
        };

        await RunExperimentAsync();

        Storage.Verify(s => s.SaveDecisions(It.IsAny<List<string>>()), Times.Never);
        Storage.Verify(s => s.SavePlanes(It.IsAny<List<byte[]>>(), It.IsAny<List<TiffPlaneMetadata>>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Backend_error_event_still_finishes_the_experiment()
    {
        _backend = async (w, _) =>
        {
            await w.WriteAsync(new MeasurementErrorEvent("Failed to start: no detector"));
            w.TryComplete();
        };
        int finished = 0;
        _workspace.ExperimentFinished += (_, _) => finished++;

        await RunExperimentAsync();

        Assert.Equal(1, finished);
        Assert.Equal(WorkspaceState.Idle, _workspace.CurrentState);
    }

    [Fact]
    public async Task Failure_while_starting_resets_state_and_allows_a_new_run()
    {
        Storage.Setup(s => s.SetStoragePath(It.IsAny<string>())).Throws(new IOException("disk full"));
        int finished = 0;
        _workspace.ExperimentFinished += (_, _) => finished++;

        await Assert.ThrowsAsync<IOException>(() => RunExperimentAsync());

        Assert.Equal(1, finished);
        Assert.False(_workspace.IsExperimentEnabled);
        Assert.Equal(WorkspaceState.Idle, _workspace.CurrentState);
        _comm.Verify(c => c.CancelMeasurementProgramAsync(It.IsAny<CancellationToken>()));

        Storage.Setup(s => s.SetStoragePath(It.IsAny<string>()));
        await RunExperimentAsync();
        Assert.Equal(2, finished);
    }

    [Fact]
    public async Task Stopping_an_experiment_cancels_it_and_closes_storage()
    {
        _backend = async (w, token) =>
        {
            await w.WriteAsync(new MeasurementStartedEvent());
            try { await Task.Delay(Timeout.Infinite, token); } catch (OperationCanceledException) { }
            w.TryComplete();
        };
        bool destroyed = false;
        _workspace.HandlerDestroyed += (_, _) => destroyed = true;

        var run = RunExperimentAsync();
        await WaitUntilAsync(() => _workspace.CurrentState == WorkspaceState.Acquiring);

        await _workspace.StopExperimentAsync();
        Assert.Equal(WorkspaceState.Idle, _workspace.CurrentState);
        await run;

        Assert.True(destroyed);
        Assert.False(_workspace.IsExperimentEnabled);
        Assert.Null(_workspace.ActiveStorageProvider);
        _comm.Verify(c => c.CancelMeasurementProgramAsync(It.IsAny<CancellationToken>()));

        // Regression: Stop cleared ActiveStorageProvider before the run's finally block,
        // which then skipped closing the write stream.
        Storage.Verify(s => s.CloseReadWriteStream(), Times.Once);
    }

    [Fact]
    public async Task Toggle_experiment_starts_and_then_stops()
    {
        _backend = async (w, token) =>
        {
            try { await Task.Delay(Timeout.Infinite, token); } catch (OperationCanceledException) { }
            w.TryComplete();
        };

        var run = _workspace.ToggleExperimentAsync(Program(), "/tmp/out", true, new List<DefinedDetection> { Detection() }, "{}");
        await WaitUntilAsync(() => _workspace.IsExperimentEnabled && _workspace.CurrentState == WorkspaceState.Acquiring);

        await _workspace.ToggleExperimentAsync(Program(), "/tmp/out", true, new List<DefinedDetection>(), "{}");
        await run;

        Assert.False(_workspace.IsExperimentEnabled);
    }

    // ---------- Live ----------

    private Func<ChannelWriter<MeasurementEvent>, CancellationToken, Task> RunUntilCancelled =>
        async (w, token) =>
        {
            await w.WriteAsync(new MeasurementStartedEvent());
            try { await Task.Delay(Timeout.Infinite, token); } catch (OperationCanceledException) { }
            w.TryComplete();
        };

    [Fact]
    public async Task Live_starts_acquiring_and_stops_back_to_idle()
    {
        _backend = RunUntilCancelled;
        ILifetimeScope? liveScope = null;
        _workspace.LiveScopeCreated += (_, s) => liveScope = s;

        var live = _workspace.StartLiveAsync(Detection());
        await WaitUntilAsync(() => liveScope != null);

        Assert.True(_workspace.IsLiveEnabled);
        Assert.Equal(WorkspaceState.Acquiring, _workspace.CurrentState);
        Assert.NotNull(_workspace.ActiveImageHandler);

        await _workspace.StopLiveAsync();
        await live;

        Assert.False(_workspace.IsLiveEnabled);
        Assert.Equal(WorkspaceState.Idle, _workspace.CurrentState);
        Assert.Null(_workspace.ActiveImageHandler);
        _comm.Verify(c => c.CancelMeasurementProgramAsync(It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Live_sends_a_long_repeat_of_the_selected_detection()
    {
        _backend = RunUntilCancelled;
        ExecuteMeasurementProgramRequest? sent = null;
        _comm.Setup(c => c.ExecuteMeasurementProgram(It.IsAny<ExecuteMeasurementProgramRequest>(),
                It.IsAny<ChannelWriter<MeasurementEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<ExecuteMeasurementProgramRequest, ChannelWriter<MeasurementEvent>, CancellationToken>((r, w, t) =>
            {
                sent = r;
                _ = Task.Run(() => _backend(w, t));
            });

        var live = _workspace.StartLiveAsync(Detection("Live acq"));
        await WaitUntilAsync(() => sent != null);
        await _workspace.StopLiveAsync();
        await live;

        Assert.Equal(10000000, (int)sent!.Program["ntotal"]!);
        Assert.Equal("Live acq", sent.Program["elements"]![0]!["detectionnames"]![0]!.ToString());
        Assert.NotNull(sent.DefinedDetections["Live acq"]);
    }

    [Fact]
    public async Task Failure_while_starting_live_resets_state_and_allows_a_retry()
    {
        // Regression: an exception before the try block left IsLiveEnabled = true forever.
        Storage.Setup(s => s.SetAcqDetPairs(It.IsAny<List<Tuple<string, string>>>())).Throws(new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _workspace.StartLiveAsync(Detection()));

        Assert.False(_workspace.IsLiveEnabled);
        Assert.Equal(WorkspaceState.Idle, _workspace.CurrentState);

        Storage.Setup(s => s.SetAcqDetPairs(It.IsAny<List<Tuple<string, string>>>()));
        _backend = RunUntilCancelled;
        var live = _workspace.StartLiveAsync(Detection());
        await WaitUntilAsync(() => _workspace.ActiveImageHandler != null);
        Assert.True(_workspace.IsLiveEnabled);

        await _workspace.StopLiveAsync();
        await live;
    }

    [Fact]
    public async Task Experiment_cannot_start_while_live_is_running()
    {
        _backend = RunUntilCancelled;
        var live = _workspace.StartLiveAsync(Detection());
        // The program is sent from a Task.Run inside ImageHandler, so wait for the call itself.
        await WaitUntilAsync(() => _comm.Invocations.Any(i => i.Method.Name == nameof(IImagerCommunicationManager.ExecuteMeasurementProgram)));

        await RunExperimentAsync();

        Assert.False(_workspace.IsExperimentEnabled);
        Assert.True(_workspace.IsLiveEnabled);
        _comm.Verify(c => c.ExecuteMeasurementProgram(It.IsAny<ExecuteMeasurementProgramRequest>(),
            It.IsAny<ChannelWriter<MeasurementEvent>>(), It.IsAny<CancellationToken>()), Times.Once);

        await _workspace.StopLiveAsync();
        await live;
    }

    [Fact]
    public async Task Stop_live_when_idle_does_nothing()
    {
        await _workspace.StopLiveAsync();

        _comm.Verify(c => c.CancelMeasurementProgramAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Only_enabled_detectors_become_acquisition_detector_pairs()
    {
        _backend = RunUntilCancelled;
        var detection = Detection();
        detection.Settings.Detectors.Add(EquipmentFixtures.Camera("DummyCam2", enabled: false));
        detection.Settings.Detectors.Add(EquipmentFixtures.Camera("DummyCam3"));

        var live = _workspace.StartLiveAsync(detection);
        await WaitUntilAsync(() => _workspace.ActiveImageHandler != null);
        await _workspace.StopLiveAsync();
        await live;

        Storage.Verify(s => s.SetAcqDetPairs(It.Is<List<Tuple<string, string>>>(p =>
            p.Select(x => x.Item2).SequenceEqual(new[] { "DummyCam1", "DummyCam3" }))));
    }
}
