using Avalonia;
using System;
using System.Threading;
using ChashApp.Services;

namespace ChashApp;

internal static class Program
{
    private static Mutex? _singleInstanceMutex;

    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && IsCliCommand(args[0]))
        {
            var exitCode = new CliRunner(new CryptoService()).RunAsync(args).GetAwaiter().GetResult();
            Environment.Exit(exitCode);
            return;
        }

        _singleInstanceMutex = new Mutex(true, "ChashApp.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        _singleInstanceMutex.ReleaseMutex();
        _singleInstanceMutex.Dispose();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static bool IsCliCommand(string value)
        => value.Equals("encrypt", StringComparison.OrdinalIgnoreCase)
           || value.Equals("decrypt", StringComparison.OrdinalIgnoreCase)
           || value.Equals("verify", StringComparison.OrdinalIgnoreCase);
}
