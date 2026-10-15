using System.Threading.Channels;
using Autofac;
using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
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
/// Filter wheel (movable component) changes while a live view is running must
/// stop the live program and restart it with the updated detection
/// configuration, the same way detector and light source changes do. The
/// backend is a scripted IImagerCommunicationManager, storage a mock
/// IStorageProvider.
/// </summary>
[Collection(AppContainerCollection.Name)]
public sealed class MovableEquipmentViewModelTests : IDisposable
{
    private readonly TestContainer _container = new();
    private readonly Mock<IImagerCommunicationManager> _comm = new();
    private readonly ImagerWorkspace _workspace;
    private readonly ExperimentManager _experimentManager;
    private readonly List<ExecuteMeasurementProgramRequest> _requests = new();

    /// <summary>What the fake backend does when a program is started.</summary>
    private Func<ChannelWriter<MeasurementEvent>, CancellationToken, Task> _backend =
        async (_, token) => { try { await Task.Delay(Timeout.Infinite, token); } catch (OperationCanceledException) { } };

    public MovableEquipmentViewModelTests()
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

    /// <summary>A live view on a detection whose only wheel is wrapped by the view model.</summary>
    private MovableEquipmentViewModel LiveWheel()
    {
        var wheel = EquipmentFixtures.FilterWheel();
        var detection = new DefinedDetection
        {
            Name = "Acq1",
            Settings = EquipmentFixtures.Detection(filterWheels: new[] { wheel })
        };
        var vm = new MovableEquipmentViewModel(wheel, _workspace, _experimentManager);
        _experimentManager.SelectedDetection = detection;
        _ = _workspace.StartLiveAsync(detection);
        return vm;
    }

    private async Task StartLiveAndWaitForFirstRequest()
    {
        await WaitUntilAsync(() => _requests.Count == 1);
        Assert.Equal(WorkspaceState.Acquiring, _workspace.CurrentState);
    }

    private static JToken SerializedPart(ExecuteMeasurementProgramRequest request, string component) =>
        request.DefinedDetections["Acq1"]!["movablecomponents"]!.Single()
            ["movablecomponentsettings"]!.Single(p => p["componentname"]!.ToString() == component);

    private DiscreteMovableEquipmentViewModel DiscreteFilter(MovableEquipmentViewModel vm) =>
        vm.Properties.OfType<DiscreteMovableEquipmentViewModel>().Single(p => p.ComponentName == "fw");

    private ContinuousMovableEquipmentViewModel ContinuousPart(MovableEquipmentViewModel vm) =>
        vm.Properties.OfType<ContinuousMovableEquipmentViewModel>().Single(p => p.ComponentName == "sl");

    // ---------- Restart during live ----------

    [Fact]
    public async Task Changing_the_filter_during_live_restarts_it_with_the_new_filter()
    {
        var vm = LiveWheel();
        await StartLiveAndWaitForFirstRequest();
        Assert.Equal("GFP", SerializedPart(_requests[0], "fw")["desiredsetting"]!.ToString());

        DiscreteFilter(vm).SelectedMovableComponent = "RFP";

        await WaitUntilAsync(() => _requests.Count == 2);
        _comm.Verify(c => c.CancelMeasurementProgramAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(WorkspaceState.Acquiring, _workspace.CurrentState);
        Assert.True(_workspace.IsLiveEnabled);
        Assert.Equal("RFP", SerializedPart(_requests[1], "fw")["desiredsetting"]!.ToString());
    }

    [Fact]
    public async Task Changing_a_continuous_value_during_live_restarts_it_with_the_new_value()
    {
        var vm = LiveWheel();
        await StartLiveAndWaitForFirstRequest();

        ContinuousPart(vm).SelectedValue = 42.5;

        await WaitUntilAsync(() => _requests.Count == 2);
        Assert.Equal(42.5, SerializedPart(_requests[1], "sl")["desiredsetting"]!.Value<double>());
    }

    [Fact]
    public async Task Changing_a_filter_while_idle_does_not_start_a_live_view()
    {
        var wheel = EquipmentFixtures.FilterWheel();
        var vm = new MovableEquipmentViewModel(wheel, _workspace, _experimentManager);
        Assert.Equal(WorkspaceState.Idle, _workspace.CurrentState);

        DiscreteFilter(vm).SelectedMovableComponent = "RFP";
        await Task.Delay(100);

        Assert.Equal(WorkspaceState.Idle, _workspace.CurrentState);
        Assert.False(_workspace.IsLiveEnabled);
        _comm.Verify(c => c.CancelMeasurementProgramAsync(It.IsAny<CancellationToken>()), Times.Never);
        _comm.Verify(c => c.ExecuteMeasurementProgram(
            It.IsAny<ExecuteMeasurementProgramRequest>(),
            It.IsAny<ChannelWriter<MeasurementEvent>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("RFP", ((DiscreteMovableComponentPartProperties)wheel.movablecomponentsettings[0]!).desiredsetting);
    }
}
