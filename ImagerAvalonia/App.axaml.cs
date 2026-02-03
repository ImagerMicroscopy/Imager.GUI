using Autofac;
using Autofac.Extensions.DependencyInjection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.Data;
using ImagerAvalonia.Exceptions;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Settings;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ImagerAvalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
    public static IContainer Container { get; private set; }

    private Process _kestrelServer = new();
    public PythonSmartServerService SmartProgramService;
    public static ConfigurationSettings Configuration = new ConfigurationSettings("Config.json");
    private LogBookConfigurationSettings LogBookConfigurationStart = new LogBookConfigurationSettings("LogBookConfigStart.json");
    private LogBookConfigurationSettings LogBookConfigurationEnd = new LogBookConfigurationSettings("LogBookConfigEnd.json");
    private LogBookView logWindow;

    public static void SetTestContainer(IContainer container)
    {
        Container = container;
    }


    public async override void OnFrameworkInitializationCompleted()
    {



        var builder = new ContainerBuilder();

        builder.RegisterType<MainViewModel>().AsSelf().SingleInstance();  // Register MainViewModel as transient
        builder.RegisterType<ComUtils>().AsSelf().SingleInstance();  // Register ComUtils as singleton

        builder.RegisterType<MainView>().AsSelf();  // Register MainView
        builder.RegisterType<MainWindow>().AsSelf();  // Register MainWindow

        builder.RegisterType<MISStorageProvider>().As<IStorageProvider>().InstancePerLifetimeScope();
        builder.RegisterType<StageControl>().As<IStageControl>().SingleInstance();
        builder.RegisterType<StageControlPanelViewModel>().SingleInstance();
        builder.RegisterType<AcquisitionHandler>().AsSelf().InstancePerDependency();
        builder.RegisterType<MessagePackAcquisitionHandler>().AsSelf().InstancePerDependency();

        builder.RegisterType<ImageDisplayViewModel>().InstancePerDependency().AsSelf().PropertiesAutowired();
        builder.RegisterType<FieldViewerViewModel>().InstancePerDependency().AsSelf().PropertiesAutowired();
        builder.RegisterType<ExperimentalPanelViewModel>().AsSelf().PropertiesAutowired();  // Register the ViewModel without parameters
        builder.RegisterType<ImageControlPanelViewModel>().AsSelf().PropertiesAutowired();  // Register the ViewModel without parameters
        builder.RegisterType<StatusWindowViewModel>().AsSelf().PropertiesAutowired();  // Register the ViewModel without parameters
        builder.RegisterType<ImageRegionDisplayViewModel>().AsSelf().InstancePerLifetimeScope();

        var observableLoggerProvider = new ObservableLoggerProvider();

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(observableLoggerProvider);
        });
        SmartProgramService = new PythonSmartServerService();
        builder.RegisterInstance(observableLoggerProvider).As<ObservableLoggerProvider>().SingleInstance();
        builder.RegisterInstance(observableLoggerProvider).As<ILoggerProvider>().SingleInstance();
        builder.RegisterInstance(loggerFactory).As<ILoggerFactory>().SingleInstance();
        builder.RegisterInstance(SmartProgramService).As<PythonSmartServerService>().SingleInstance();
        builder.RegisterGeneric(typeof(Logger<>)).As(typeof(ILogger<>)).SingleInstance();


        builder.RegisterType<ObservableLoggerProvider>().SingleInstance().As<ILoggerProvider>();
        builder.RegisterType<LoggerService>().SingleInstance().As<ILogger>();
        builder.RegisterType<ImageDisplayViewModelFactory>().As<IImageDisplayViewModelFactory>().SingleInstance();
        builder.RegisterType<UserDefinedAcquisitions>().SingleInstance();
        builder.RegisterType<AcquisitionStateService>().SingleInstance();
        builder.RegisterType<EquipmentState>().SingleInstance();

        builder.RegisterType<SmartProgramOutputViewModel>().SingleInstance();
        builder.RegisterType<SmartProgramViewModel>().InstancePerDependency().PropertiesAutowired();
        builder.RegisterType<PythonEditorWindowViewModel>().InstancePerDependency().PropertiesAutowired();
        builder.RegisterType<StageLoopViewModel>().InstancePerDependency();
        builder.RegisterType<RelStageViewModel>().InstancePerDependency();
        builder.RegisterType<ExperimentSerialization>().As<IExperimentSerialization>().InstancePerLifetimeScope();
        builder.RegisterType<SmartProcessingRegisterViewModel>().SingleInstance().PropertiesAutowired();

        builder.RegisterType<PythonHttpComService>().SingleInstance().As<IPythonCom>();
        builder.RegisterType<PythonLintingService>().SingleInstance().As<IPythonLinting>();

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

        var registry = new MeasurementTypeRegistry();
        registry.Register<Detection>("detection");
        registry.Register<Irradiation>("irradiation");
        registry.Register<WaitForTime>("wait");
        registry.Register<RelativeStageLoop>("relativestageloop");
        registry.Register<DoTimes>("dotimes");
        registry.Register<StageLoop>("stageloop");
        registry.Register<TimeLapse>("timelapse");
        registry.Register<UpdateAcquisition>("updateacquisition");

        builder.RegisterInstance(registry).As<IMeasurementTypeRegistry>().SingleInstance();


        var serviceCollection = new ServiceCollection();
        serviceCollection.AddHttpClient<IPythonCom, PythonHttpComService>();
        serviceCollection.AddHttpClient<IPythonLinting, PythonLintingService>();
        builder.Populate(serviceCollection);
        Container = builder.Build();


        

        using (var scope = Container.BeginLifetimeScope())
        {
            var logbook_context = scope.Resolve<LogBookContext>();
            logbook_context.Database.EnsureCreated();
        }


        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();


            
            desktop.Exit += OnAppExit;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            var mainWindow = Container.Resolve<MainWindow>();
            desktop.MainWindow = mainWindow;

            mainWindow.Show();
            if (Configuration.IsLogBookEnabled)
            {
                using (var scope = Container.BeginLifetimeScope())
                {
                    var db = scope.Resolve<LogBookContext>();
                    logWindow = new LogBookView(LogBookConfigurationStart.LogSettings, LogBookConfigurationEnd.LogSettings, false);
                    logWindow.SetDBContext(db);
                    await logWindow.ShowDialog(mainWindow);
                }
            }

            await TryEstablishImagerCommunication(mainWindow, desktop);


            mainWindow.InitializeUserEquipment();
            mainWindow.InitializeImageControlPanel();
            mainWindow.Closing += MainWindow_Closing;
            //SmartProgramService.StartSmartProgram();



        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            var mainView = Container.Resolve<MainView>();
            singleViewPlatform.MainView = mainView;
        }
      

        base.OnFrameworkInitializationCompleted();
    }

    private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (sender is MainWindow mainWindow)
        {
            if (!mainWindow.ForceClosing)
            {
                if (Configuration.IsLogBookEnabled)
                {
                    using (var scope = Container.BeginLifetimeScope())
                    {
                        e.Cancel = true;
                        var db = scope.Resolve<LogBookContext>();
                        logWindow.SetDBContext(db);
                        logWindow.SetLogOutMode(true);
                        logWindow.SetEndDate();
                        await logWindow.ShowDialog(mainWindow);

                    }
                }
                mainWindow.Close();
            }
        }
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    private async Task TryEstablishImagerCommunication(Window mainWindow, IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            using (ImagerCommunication ImagerCommunicator = new ImagerCommunication())
            {
                ImagerCommunicator.EstablishConnectionStream();
            }
        }
        catch (SocketException socketEx)
        {
            await ExceptionWindowHandler.ShowDialogAsync("A socket error occurred", socketEx.Message, socketEx.StackTrace, mainWindow);
            desktop.Shutdown();


        }
    }


    private async void OnAppExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {

            foreach (var window in lifetime.Windows)
            {
                window.Close();
                lifetime.Shutdown();

            }
        }
        //SmartProgramService.KillSmartProgram();
        OpenStorageIDS.CloseAllImageIDS();
    }
}