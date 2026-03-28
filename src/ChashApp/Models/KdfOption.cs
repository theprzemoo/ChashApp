namespace ChashApp.Models;

public sealed class KdfOption
{
    public string Key { get; }
    public string Label { get; }
    public KdfAlgorithm Algorithm { get; }

    public KdfOption(string key, string label, KdfAlgorithm algorithm)
    {
        Key = key;
        Label = label;
        Algorithm = algorithm;
    }
}
