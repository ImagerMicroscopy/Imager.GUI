using Autofac;
using Avalonia;
using Avalonia.ReactiveUI;
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

            // Fire-and-forget-for-now: starts equipment communication on a
            // threadpool thread immediately. Nothing here blocks the STA
            // thread, so it doesn't interfere with StartWithClassicDesktopLifetime
            // below. App awaits this Task later, once its dispatcher is running.



            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
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