namespace ChashApp.Models;

public sealed class LocalizedOption
{
    public string Key { get; }
    public string Label { get; private set; }

    public LocalizedOption(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public void UpdateLabel(string label) => Label = label;
}
