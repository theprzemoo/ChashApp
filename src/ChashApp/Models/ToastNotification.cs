namespace ChashApp.Models;

public sealed class ToastNotification
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string BorderHex { get; init; } = "#2DF0D0";
    public string BackgroundHex { get; init; } = "#10202D";
}
