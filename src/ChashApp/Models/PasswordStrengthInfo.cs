namespace ChashApp.Models;

public sealed class PasswordStrengthInfo
{
    public string Key { get; }
    public int Score { get; }
    public string AccentHex { get; }

    public PasswordStrengthInfo(string key, int score, string accentHex)
    {
        Key = key;
        Score = score;
        AccentHex = accentHex;
    }
}
