using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ChashApp.Services;
using ChashApp.ViewModels;
using ChashApp.Views;

namespace ChashApp;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var localization = new LocalizationService();
        var historyService = new HistoryService();
        var filePickerService = new FilePickerService();
        var cryptoService = new CryptoService();
        var settingsService = new SettingsService();
        var recentFilesService = new RecentFilesService();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(localization, cryptoService, historyService, filePickerService, settingsService, recentFilesService)
            };
            var splashWindow = new SplashWindow();

            desktop.MainWindow = mainWindow;
            mainWindow.WindowState = Avalonia.Controls.WindowState.Minimized;
            mainWindow.ShowInTaskbar = false;
            splashWindow.Show();

            DispatcherTimer.RunOnce(() =>
            {
                splashWindow.Close();
                mainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
                mainWindow.ShowInTaskbar = true;
                mainWindow.Activate();
            }, TimeSpan.FromMilliseconds(1400));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
