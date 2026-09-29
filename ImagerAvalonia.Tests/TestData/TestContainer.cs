using Autofac;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Storage;
using ImagerAvalonia.Services.Workspace.SmartProgramWorkspace;
using ImagerAvalonia.Tests.TestData;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using Moq;
using Xunit;

namespace ImagerAvalonia.Tests;

/// <summary>
/// Tests that touch App.Container (every MeasurementElementViewModel resolves
/// SmartProcessingRegisterViewModel from it in its constructor) share global state,
/// so they run in this collection, one at a time.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AppContainerCollection
{
    public const string Name = "App.Container";
}

/// <summary>
/// A small Autofac container with the pieces the experiment view models need, installed
/// as App.Container for the lifetime of one test. Mocks are exposed so tests can set them up.
/// </summary>
public sealed class TestContainer : IDisposable
{
    private readonly IContainer? _previous;

    public IContainer Container { get; }
    public Mock<IStageControl> StageControl { get; } = new();
    public Mock<IStorageProvider> StorageProvider { get; } = new();
    public GlobalDefinedSettingsViewModel Settings { get; } = new();

    public TestContainer(Action<ContainerBuilder>? configure = null)
    {
        var stages = new Stages();
        stages.MotorizedStages.Add(new Stage("Dummy stage", "dStage"));
        StageControl.SetupGet(s => s.AvailableStages).Returns(stages);
        StageControl.SetupProperty(s => s.StageName, "dStage");

        var builder = new ContainerBuilder();
        builder.RegisterType<SmartProgramRegistry>().SingleInstance();
        builder.RegisterType<SmartProcessingRegisterViewModel>().SingleInstance();
        builder.RegisterInstance(Settings);
        builder.RegisterInstance(StageControl.Object).As<IStageControl>();
        builder.Register(_ => StorageProvider.Object).As<IStorageProvider>().InstancePerLifetimeScope();
        builder.RegisterType<MeasurementElementViewModelFactory>().As<IMeasurementElementViewModelFactory>().SingleInstance();

        builder.RegisterType<DetectionElementViewModel>();
        builder.RegisterType<DoTimesViewModel>();
        builder.RegisterType<RelStageViewModel>();
        builder.RegisterType<StageLoopViewModel>();
        builder.RegisterType<WaitViewModel>();
        builder.RegisterType<TimeLapseViewModel>();
        builder.RegisterType<IrradiationPanelViewModel>();
        builder.RegisterType<UpdateAcquisitionViewModel>();

        configure?.Invoke(builder);

        Container = builder.Build();
        _previous = App.Container;
        App.SetTestContainer(Container);
    }

    public T Resolve<T>() where T : notnull => Container.Resolve<T>();

    public IMeasurementElementViewModelFactory Factory => Resolve<IMeasurementElementViewModelFactory>();

    /// <summary>Adds acquisitions (reconciled against the fixture equipment) to Settings.</summary>
    public IReadOnlyDictionary<string, AcquisitionSettingsViewModel> AddAcquisitions(params string[] names) =>
        Settings.ReconcileAcquisitions(
            names.Select(n => (n, EquipmentFixtures.Detection())),
            EquipmentFixtures.Workspace());

    public void Dispose()
    {
        App.SetTestContainer(_previous!);
        Container.Dispose();
    }
}
