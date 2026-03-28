using Avalonia.Headless.XUnit;
using ChashApp.Services;
using ChashApp.ViewModels;
using ChashApp.Views;
using Xunit;

namespace ChashApp.Tests;

public sealed class UiSmokeTests
{
    [AvaloniaFact]
    public void MainWindow_CanBeConstructed_WithViewModel()
    {
        var vm = new MainWindowViewModel(
            new LocalizationService(),
            new CryptoService(),
            new HistoryService(),
            new FilePickerService(),
            new SettingsService(),
            new RecentFilesService());

        var window = new MainWindow
        {
            DataContext = vm
        };

        Assert.NotNull(window);
        Assert.Equal(vm, window.DataContext);
    }

    [AvaloniaFact]
    public void ViewModel_AddDroppedFiles_LoadsSelection()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "ChashApp.UiSmoke", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var sampleFile = Path.Combine(tempDirectory, "dragged.txt");
        File.WriteAllText(sampleFile, "drag me");

        var vm = new MainWindowViewModel(
            new LocalizationService(),
            new CryptoService(),
            new HistoryService(),
            new FilePickerService(),
            new SettingsService(),
            new RecentFilesService());

        vm.AddDroppedPaths(new[] { sampleFile });

        Assert.Single(vm.SelectedFiles);
        Assert.Contains("dragged.txt", vm.SelectedFilesDisplay);
    }
}
