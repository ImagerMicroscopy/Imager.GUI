using Autofac;
using Autofac.Extensions.DependencyInjection;
using ImagerAvalonia.Data;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.Logging;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Storage;
using ImagerAvalonia.Services.Workspace;
using ImagerAvalonia.Services.Workspace.SmartProgramWorkspace;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using ImagerAvalonia.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace ImagerAvalonia;

public static class CompositionRoot
{
    public static IContainer BuildContainer()
    {
        var builder = new ContainerBuilder();

        builder.RegisterType<MainViewModel>().AsSelf().SingleInstance();
        builder.RegisterType<ImagerConnectionHandler>().As<IImagerConnectionHandler>().SingleInstance();
        builder.RegisterInstance(ImagerCommunicationManager.Instance).As<IImagerCommunicationManager>();
        builder.RegisterType<EquipmentCoordinator>().SingleInstance();
        builder.RegisterType<MainView>().AsSelf();
        builder.RegisterType<MainWindow>().AsSelf();

        builder.RegisterType<MISStorageProvider>().As<IStorageProvider>().InstancePerLifetimeScope();
        builder.RegisterType<StageControl>().As<IStageControl>().SingleInstance();
        builder.RegisterType<StageControlPanelViewModel>().SingleInstance();
        builder.RegisterType<MessagePackAcquisitionHandler>().AsSelf().InstancePerDependency();

        builder.RegisterType<ImageDisplayViewModel>().InstancePerDependency().AsSelf().PropertiesAutowired();
        builder.RegisterType<FieldViewerViewModel>().InstancePerDependency().AsSelf().PropertiesAutowired();
        builder.RegisterType<ExperimentalPanelViewModel>().AsSelf().PropertiesAutowired();
        builder.RegisterType<ImageControlPanelViewModel>().AsSelf().PropertiesAutowired();
        builder.RegisterType<StatusWindowViewModel>().AsSelf().PropertiesAutowired();
        builder.RegisterType<ImageRegionDisplayViewModel>().AsSelf().InstancePerLifetimeScope();

        var observableLoggerProvider = new ObservableLoggerProvider();
        var loggerFactory = LoggerFactory.Create(lb => lb.AddProvider(observableLoggerProvider));

        builder.RegisterInstance(observableLoggerProvider).As<ObservableLoggerProvider>().SingleInstance();
        builder.RegisterInstance(observableLoggerProvider).As<ILoggerProvider>().SingleInstance();
        builder.RegisterInstance(loggerFactory).As<ILoggerFactory>().SingleInstance();
        builder.RegisterGeneric(typeof(Logger<>)).As(typeof(ILogger<>)).SingleInstance();

        builder.RegisterType<ExperimentManager>().AsSelf().SingleInstance();

        builder.RegisterType<EquipmentWorkspace>().AsSelf().SingleInstance();
        builder.RegisterType<ExperimentBuilder>().AsSelf().InstancePerDependency();
        builder.RegisterType<ExperimentBuilderFactory>().As<ExperimentBuilderFactory>().SingleInstance();

        builder.RegisterType<ObservableLoggerProvider>().SingleInstance().As<ILoggerProvider>();
        builder.RegisterType<LoggerService>().SingleInstance().As<ILogger>();
        builder.RegisterType<ImageDisplayViewModelFactory>().As<IImageDisplayViewModelFactory>().SingleInstance();
        builder.RegisterType<ImagerWorkspace>().SingleInstance();

        builder.RegisterType<GlobalDefinedSettingsViewModel>().SingleInstance();
        builder.RegisterType<EquipmentState>().SingleInstance();
        builder.RegisterType<SmartProgramViewModel>().InstancePerDependency().PropertiesAutowired();
        builder.RegisterType<PythonEditorWindowViewModel>().InstancePerDependency().PropertiesAutowired();
        builder.RegisterType<SmartProcessingRegisterViewModel>().SingleInstance().PropertiesAutowired();

        builder.RegisterType<PythonHttpComService>().SingleInstance().As<IPythonCom>();
        builder.RegisterType<PythonLintingService>().SingleInstance().As<IPythonLinting>();

        builder.RegisterType<MeasurementElementViewModelFactory>().As<IMeasurementElementViewModelFactory>().SingleInstance();

        builder.RegisterType<SmartProgramRegistry>().SingleInstance();

        builder.RegisterType<ExperimentalPanelViewModel>();
        builder.RegisterType<DoTimesView>();
        builder.RegisterType<RelStageView>();
        builder.RegisterType<StageLoopView>();
        builder.RegisterType<WaitView>();
        builder.RegisterType<TimeLapseView>();
        builder.RegisterType<IrradiationPanelView>();
        builder.RegisterType<UpdateAcquisitionView>();
        builder.RegisterType<RobotControlView>();
        builder.RegisterType<AcquisitionPanelView>();

        builder.RegisterType<DoTimesViewModel>();
        builder.RegisterType<RelStageViewModel>();
        builder.RegisterType<StageLoopViewModel>();
        builder.RegisterType<WaitViewModel>();
        builder.RegisterType<RobotViewModel>();
        builder.RegisterType<TimeLapseViewModel>();
        builder.RegisterType<IrradiationPanelViewModel>();
        builder.RegisterType<UpdateAcquisitionViewModel>();
        builder.RegisterType<RobotControlViewModel>();
        builder.RegisterType<DetectionElementViewModel>();

        builder.Register(ctx =>
        {
            var optionsBuilder = new DbContextOptionsBuilder<LogBookContext>();

            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ImagerAvalonia",
                "logbook.db");

            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            optionsBuilder.UseSqlite($"Data Source={dbPath}");

            return new LogBookContext(optionsBuilder.Options);
        })
        .AsSelf()
        .InstancePerLifetimeScope();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddHttpClient<IPythonCom, PythonHttpComService>();
        serviceCollection.AddHttpClient<IPythonLinting, PythonLintingService>();
        builder.Populate(serviceCollection);

        return builder.Build();
    }
}