namespace ChashApp.Models;

public sealed class HistoryEntry
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public string Category { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
    public string Algorithm { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;

    public string DisplayTimestamp => TimestampUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

    public string DisplayTarget
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Target))
            {
                return "(empty)";
            }

            var trimmed = Target.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(trimmed);
            return string.IsNullOrWhiteSpace(name) ? Target : name;
        }
    }

    public string StatusLabel => Success ? "Success" : "Failed";
    public string StatusBorderHex => Success ? "#2DF0D0" : "#F39AAE";
    public string StatusForegroundHex => Success ? "#D9F7F0" : "#FFD5DD";
}
