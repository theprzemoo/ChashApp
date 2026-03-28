using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Controls.ApplicationLifetimes;
using System.ComponentModel;
using System.Diagnostics;
using ChashApp.Models;
using ChashApp.ViewModels;

namespace ChashApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        TitleBarArea.PointerPressed += OnTitleBarPointerPressed;
        Closing += OnClosing;
        Opened += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.HostWindow = this;
            }
        };
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (!e.Data.Contains(DataFormats.Files))
        {
            return;
        }

        var files = e.Data.Get(DataFormats.Files) as IEnumerable<IStorageItem>;
        if (files is null)
        {
            return;
        }

        vm.AddDroppedPaths(files.Select(file => file.TryGetLocalPath() ?? string.Empty));
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void MinimizeWindow(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeWindow(object? sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private async void CloseWindow(object? sender, RoutedEventArgs e)
    {
        await ConfirmAndCloseAsync();
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.CloseConfirmed)
        {
            return;
        }

        e.Cancel = true;
        await ConfirmAndCloseAsync();
    }

    private async Task ConfirmAndCloseAsync()
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            Close();
            return;
        }

        var dialog = new ConfirmCloseWindow
        {
            DataContext = new
            {
                TitleText = vm.CloseConfirmTitle,
                MessageText = vm.CloseConfirmMessage,
                ConfirmText = vm.CloseConfirmYes,
                CancelText = vm.CloseConfirmNo
            }
        };

        var confirmed = await dialog.ShowDialog<bool?>(this);
        if (confirmed == true)
        {
            vm.CloseConfirmed = true;
            Close();
        }
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.P &&
            DataContext is MainWindowViewModel vm)
        {
            e.Handled = true;
            await vm.PanicModeAsync();
            Close();
        }
    }

    private async void UninstallAppClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var uninstallExe = Directory.GetFiles(AppContext.BaseDirectory, "unins*.exe").FirstOrDefault();
        if (string.IsNullOrWhiteSpace(uninstallExe))
        {
            vm.StatusMessage = "Uninstaller was not found in the application folder.";
            return;
        }

        var dialog = new ConfirmCloseWindow
        {
            DataContext = new
            {
                TitleText = vm.UninstallAppLabel,
                MessageText = vm.UninstallNowMessage,
                ConfirmText = vm.UninstallConfirmLabel,
                CancelText = vm.CloseConfirmNo
            }
        };

        var confirmed = await dialog.ShowDialog<bool?>(this);
        if (confirmed == true)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C ping 127.0.0.1 -n 2 > nul & start \"\" \"{uninstallExe}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            vm.CloseConfirmed = true;
            Close();
        }
    }

    private async void SecureEraseClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var dialog = new ConfirmCloseWindow
        {
            DataContext = new
            {
                TitleText = vm.SecureEraseActionLabel,
                MessageText = vm.SecureEraseNowMessage,
                ConfirmText = vm.SecureEraseActionLabel,
                CancelText = vm.CloseConfirmNo
            }
        };

        var confirmed = await dialog.ShowDialog<bool?>(this);
        if (confirmed == true)
        {
            vm.SecureEraseSelectedCommand.Execute(null);
        }
    }

    private void OpenHistoryEntryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || sender is not Button button || button.CommandParameter is not HistoryEntry entry)
        {
            return;
        }

        vm.OpenHistoryEntry(entry);
    }
}
