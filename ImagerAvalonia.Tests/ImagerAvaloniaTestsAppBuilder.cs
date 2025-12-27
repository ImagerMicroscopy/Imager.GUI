using Avalonia;
using Avalonia.Headless;
using ImagerAvalonia;
using ImagerAvalonia.Tests;


[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace ImagerAvalonia.Tests
{
 // Your main app namespace

    public class TestAppBuilder
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
