using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.Json;
using ChashApp.Models;

namespace ChashApp.Services;

public sealed class HistoryService
{
    private readonly ObservableCollection<HistoryEntry> _entries = new();
    private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };
    private readonly string _storagePath;

    public ReadOnlyObservableCollection<HistoryEntry> Entries { get; }

    public HistoryService()
    {
        var baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChashApp");
        Directory.CreateDirectory(baseDirectory);
        _storagePath = Path.Combine(baseDirectory, "history.json");
        Entries = new ReadOnlyObservableCollection<HistoryEntry>(_entries);
        LoadFromDisk();
    }

    public void Add(string category, string action, string target, bool success, string message, string? algorithm = null)
    {
        _entries.Insert(0, new HistoryEntry
        {
            TimestampUtc = DateTime.UtcNow,
            Category = category,
            Action = action,
            Target = target,
            Algorithm = string.IsNullOrWhiteSpace(algorithm) ? "AES-256-GCM" : algorithm,
            Success = success,
            Message = message
        });
        SaveSnapshot();
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add => _entries.CollectionChanged += value;
        remove => _entries.CollectionChanged -= value;
    }

    public async Task ExportJsonAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, _entries, _serializerOptions, cancellationToken);
    }

    public async Task ExportCsvAsync(string path, CancellationToken cancellationToken = default)
    {
        var lines = new List<string>
        {
            "TimestampUtc,Category,Action,Target,Algorithm,Success,Message"
        };

        lines.AddRange(_entries.Select(entry => string.Join(",",
            Escape(entry.TimestampUtc.ToString("O")),
            Escape(entry.Category),
            Escape(entry.Action),
            Escape(entry.Target),
            Escape(entry.Algorithm),
            Escape(entry.Success.ToString()),
            Escape(entry.Message))));

        await File.WriteAllLinesAsync(path, lines, cancellationToken);
    }

    public void Clear()
    {
        _entries.Clear();
        SaveSnapshot();
    }

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private void LoadFromDisk()
    {
        if (!File.Exists(_storagePath))
        {
            return;
        }

        try
        {
            var entries = JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(_storagePath), _serializerOptions);
            if (entries is null)
            {
                return;
            }

            foreach (var entry in entries.OrderByDescending(item => item.TimestampUtc))
            {
                _entries.Add(entry);
            }
        }
        catch
        {
        }
    }

    private void SaveSnapshot()
    {
        try
        {
            File.WriteAllText(_storagePath, JsonSerializer.Serialize(_entries, _serializerOptions));
        }
        catch
        {
        }
    }
}
