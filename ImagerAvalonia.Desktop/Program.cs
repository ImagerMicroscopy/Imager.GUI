using Autofac;
using Avalonia;
using Avalonia.ReactiveUI;
using ImagerAvalonia.Utils;
using ImagerAvalonia;
using System;
using System.Threading.Tasks;

namespace ImagerAvalonia.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("STARTING");
            System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

            App.Container = CompositionRoot.BuildContainer();

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            if(ImagerStartup.ImagerProcess is not null)
            {
                ImagerStartup.ImagerProcess.Kill();
            }
            Console.WriteLine($"{ex.Message}{ex.StackTrace}");
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseReactiveUI()
            .LogToTrace();
}