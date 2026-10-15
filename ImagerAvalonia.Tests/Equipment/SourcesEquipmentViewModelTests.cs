using System.Threading.Channels;
using Autofac;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Workspace;
using ImagerAvalonia.Services.Workspace.ExperimentWorkspace;
using ImagerAvalonia.Services.Workspace.SmartProgramWorkspace;
using ImagerAvalonia.Tests.TestData;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ImagerAvalonia.Tests.Equipment;

/// <summary>
/// Light source changes while a live view is running must stop the live
/// program and restart it with the updated detection configuration, the same
/// way detector (camera) changes do. The backend is a scripted
/// IImagerCommunicationManager, storage a mock IStorageProvider.
/// </summary>
[Collection(AppContainerCollection.Name)]
public sealed class SourcesEquipmentViewModelTests : IDisposable
{
    private readonly TestContainer _container = new();
    private readonly Mock<IImagerCommunicationManager> _comm = new();
    private readonly ImagerWorkspace _workspace;
    private readonly ExperimentManager _experimentManager;
    private readonly List<ExecuteMeasurementProgramRequest> _requests = new();

    /// <summary>What the fake backend does when a program is started.</summary>
    private Func<ChannelWriter<MeasurementEvent>, CancellationToken, Task> _backend =
        async (_, token) => { try { await Task.Delay(Timeout.Infinite, token); } catch (OperationCanceledException) { } };

    public SourcesEquipmentViewModelTests()
    {
        _comm.Setup(c => c.ExecuteMeasurementProgram(
                It.IsAny<ExecuteMeasurementProgramRequest>(),
                It.IsAny<ChannelWriter<MeasurementEvent>>(),
                It.IsAny<CancellationToken>()))
            .Callback<ExecuteMeasurementProgramRequest, ChannelWriter<MeasurementEvent>, CancellationToken>(
                (request, writer, token) => { _requests.Add(request); _ = Task.Run(() => _backend(writer, token)); });

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

        var elementFactory = _container.Container.Resolve<IMeasurementElementViewModelFactory>();
        _experimentManager = new ExperimentManager(
            _container.Settings,
            _container.StageControl.Object,
            new EquipmentWorkspace(),
            new SmartProcessingRegisterViewModel(new SmartProgramRegistry()),
            elementFactory,
            new ExperimentBuilderFactory(elementFactory));
    }

    public void Dispose()
    {
        _workspace.Dispose();
        _experimentManager.Dispose();
        _container.Dispose();
    }

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

    /// <summary>A live view on a detection whose only source is wrapped by the view model.</summary>
    private (DefinedDetection Detection, SourcesEquipmentViewModel ViewModel) LiveLaser(
        string activeChannel = "ch1", bool allowMultipleChannels = true)
    {
        var source = EquipmentFixtures.Laser(allowMultipleChannels: allowMultipleChannels, activeChannels: activeChannel);
        var detection = new DefinedDetection
        {
            Name = "Acq1",
            Settings = EquipmentFixtures.Detection(irradiation: new[] { source })
        };
        var vm = new SourcesEquipmentViewModel(source, _workspace, _experimentManager);
        _experimentManager.SelectedDetection = detection;
        _ = _workspace.StartLiveAsync(detection);
        return (detection, vm);
    }

    private static JToken SerializedSource(ExecuteMeasurementProgramRequest request) =>
        request.DefinedDetections["Acq1"]!["irradiation"]!.Single();

    private static int SerializedPower(ExecuteMeasurementProgramRequest request) =>
        (int)SerializedSource(request)["lightsourcepower"]!.Single();

    private async Task StartLiveAndWaitForFirstRequest()
    {
        await WaitUntilAsync(() => _requests.Count == 1);
        Assert.Equal(WorkspaceState.Acquiring, _workspace.CurrentState);
    }

    // ---------- Restart during live ----------

    [Fact]
    public async Task Changing_a_channel_power_during_live_restarts_it_with_the_updated_power()
    {
        var (_, vm) = LiveLaser();
        await StartLiveAndWaitForFirstRequest();
        Assert.Equal(40, SerializedPower(_requests[0]));

        vm.Channels.Single(c => c.Name == "ch1").PowerLevel = 80;

        await WaitUntilAsync(() => _requests.Count == 2);
        _comm.Verify(c => c.CancelMeasurementProgramAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(WorkspaceState.Acquiring, _workspace.CurrentState);
        Assert.True(_workspace.IsLiveEnabled);
        Assert.Equal(80, SerializedPower(_requests[1]));
    }

    [Fact]
    public async Task Disabling_the_only_channel_during_live_restarts_it_without_the_source()
    {
        var (_, vm) = LiveLaser();
        await StartLiveAndWaitForFirstRequest();

        vm.Channels.Single(c => c.Name == "ch1").IsEnabled = false;

        await WaitUntilAsync(() => _requests.Count == 2);
        // A source without active channels is disabled and enable-gated
        // serialization drops it from the detection configuration.
        Assert.Empty(_requests[1].DefinedDetections["Acq1"]!["irradiation"]!);
    }

    [Fact]
    public async Task Activating_a_channel_when_only_one_is_allowed_restarts_live_once()
    {
        var (_, vm) = LiveLaser(activeChannel: "ch1", allowMultipleChannels: false);
        await StartLiveAndWaitForFirstRequest();

        // Enabling ch2 deactivates ch1 in the same user action; both changes
        // must end up in one restarted program, not two restarts.
        vm.Channels.Single(c => c.Name == "ch2").IsEnabled = true;

        await WaitUntilAsync(() => _requests.Count == 2);
        _comm.Verify(c => c.CancelMeasurementProgramAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("ch2", SerializedSource(_requests[1])["lightsourcechannel"]!.Single().ToString());
    }

    [Fact]
    public async Task Changing_a_channel_while_idle_does_not_start_a_live_view()
    {
        var source = EquipmentFixtures.Laser(activeChannels: "ch1");
        var vm = new SourcesEquipmentViewModel(source, _workspace, _experimentManager);
        Assert.Equal(WorkspaceState.Idle, _workspace.CurrentState);

        vm.Channels.Single(c => c.Name == "ch1").PowerLevel = 80;
        await Task.Delay(100);

        Assert.Equal(WorkspaceState.Idle, _workspace.CurrentState);
        Assert.False(_workspace.IsLiveEnabled);
        _comm.Verify(c => c.CancelMeasurementProgramAsync(It.IsAny<CancellationToken>()), Times.Never);
        _comm.Verify(c => c.ExecuteMeasurementProgram(
            It.IsAny<ExecuteMeasurementProgramRequest>(),
            It.IsAny<ChannelWriter<MeasurementEvent>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(80, vm.LightSource.LightsourcePower.Single());
    }
}
