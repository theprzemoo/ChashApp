using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;

[assembly: AvaloniaTestApplication(typeof(ChashApp.Tests.TestAppBuilder))]

namespace ChashApp.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<ChashApp.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
