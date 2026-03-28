using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ChashApp.Views;

public partial class ConfirmCloseWindow : Window
{
    public ConfirmCloseWindow()
    {
        InitializeComponent();
    }

    private void ConfirmClick(object? sender, RoutedEventArgs e) => Close(true);

    private void CancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
