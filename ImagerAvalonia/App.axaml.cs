using Autofac;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.Data;
using ImagerAvalonia.Exceptions;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Storage;
using ImagerAvalonia.Settings;
using ImagerAvalonia.Utils;
using ImagerAvalonia.Views;
using ScottPlot;
using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ImagerAvalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }


    public static IContainer Container { get; set; }


    public static Task<EquipmentInitResult>? EquipmentFetchTask { get; set; }


    public static ConfigurationSettings Configuration = new ConfigurationSettings(Path.Combine(AppContext.BaseDirectory, "Config.json"));
    private LogBookConfigurationSettings LogBookConfigurationStart = new LogBookConfigurationSettings(Path.Combine(AppContext.BaseDirectory, "LogBookConfigStart.json"));
    private LogBookConfigurationSettings LogBookConfigurationEnd = new LogBookConfigurationSettings(Path.Combine(AppContext.BaseDirectory, "LogBookConfigEnd.json"));
    private LogBookView logWindow;

    public static void SetTestContainer(IContainer container)
    {
        Container = container;
    }

    public async override void OnFrameworkInitializationCompleted()
    {

        try
        {
            await ImagerStartup.WaitForImagerStartup(
            "Imager.exe",
            "Ready to measure!",
            Configuration.ImagerPath
            );

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during Imager startup: {ex.Message}. \n You can change the imager folder " +
                $"in the configuration file (Config.json) at key 'imagerpath'");

        }



        Container ??= CompositionRoot.BuildContainer();


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
                var commanager = ImagerCommunicationManager.Instance;
                EquipmentInitResult equipment = new EquipmentInitResult();

                try
                {
                    var coordinator = Container.Resolve<EquipmentCoordinator>();
                    equipment = await coordinator.FetchEquipment();
                }
                catch (SocketException socketEx)
                {
                    await ExceptionWindowHandler.ShowDialogAsync("A socket error occurred", socketEx.Message, socketEx.StackTrace, null);
                    commanager.IsInteractionEnabled = false;

                }
                catch (Exception ex)
                {
                    await ExceptionWindowHandler.ShowDialogAsync("Connection Error", ex.Message, ex.StackTrace, null);
                    commanager.IsInteractionEnabled = false;

                }

                var mainWindow = Container.Resolve<MainWindow>();
                mainWindow.ApplyEquipment(equipment);

                desktop.Exit += OnAppExit;
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;


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



                mainWindow.InitializeImageControlPanel();
                mainWindow.Closing += MainWindow_Closing;
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
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    private void IncreaseValue(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.TemplatedParent is NumericUpDown numericUpDown)
        {
            if (numericUpDown.Value == null)
            {
                numericUpDown.Value = numericUpDown.Minimum;
            }
            else
            {
                numericUpDown.Value = Math.Min((decimal)numericUpDown.Value + (decimal)numericUpDown.Increment, (decimal)numericUpDown.Maximum);
            }
        }
    }

    private void DecreaseValue(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.TemplatedParent is NumericUpDown numericUpDown)
        {
            if (numericUpDown.Value == null)
            {
                numericUpDown.Value = numericUpDown.Minimum;
            }
            else
            {
                numericUpDown.Value = Math.Max((decimal)numericUpDown.Value - (decimal)numericUpDown.Increment, (decimal)numericUpDown.Minimum);
            }
        }
    }

    private async void OnAppExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
                OpenStorageIDS.CloseAllImageIDS();

        if(ImagerStartup.ImagerProcess is not null)
        {
            ImagerStartup.ImagerProcess.Kill(entireProcessTree: true );           
        }

        if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            foreach (var window in lifetime.Windows)
            {
                window.Close();
                lifetime.Shutdown();
            }
        }
    }
}