namespace ChashApp.Models;

public sealed class AccentPreset
{
    public string Key { get; }
    public string Label { get; }
    public string Hex { get; }

    public AccentPreset(string key, string label, string hex)
    {
        Key = key;
        Label = label;
        Hex = hex;
    }
}
