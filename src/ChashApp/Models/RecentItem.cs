namespace ChashApp.Models;

public sealed class RecentItem
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public string Path { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string DisplayName => System.IO.Path.GetFileName(Path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)) switch
    {
        { Length: > 0 } fileName => fileName,
        _ => Path
    };
}
