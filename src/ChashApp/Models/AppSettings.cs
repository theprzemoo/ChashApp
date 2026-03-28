namespace ChashApp.Models;

public sealed class AppSettings
{
    public string Language { get; set; } = "en";
    public string AccentColor { get; set; } = "mint";
    public string DefaultKdf { get; set; } = "pbkdf2-sha256";
    public bool UseAcrylic { get; set; } = true;
    public bool SaveHistoryOnExit { get; set; } = true;
    public bool SecureDeleteAfterProcessing { get; set; }
    public bool PrivacyModeEnabled { get; set; } = true;
    public int AutoLockMinutes { get; set; } = 3;
}
