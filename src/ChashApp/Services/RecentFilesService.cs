using System.Collections.ObjectModel;
using ChashApp.Models;

namespace ChashApp.Services;

public sealed class RecentFilesService
{
    private readonly ObservableCollection<RecentItem> _items = new();

    public ReadOnlyObservableCollection<RecentItem> Items { get; }

    public RecentFilesService()
    {
        Items = new ReadOnlyObservableCollection<RecentItem>(_items);
    }

    public void Track(string path, string action)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var existing = _items.FirstOrDefault(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            _items.Remove(existing);
        }

        _items.Insert(0, new RecentItem
        {
            Path = path,
            Action = action,
            TimestampUtc = DateTime.UtcNow
        });

        while (_items.Count > 8)
        {
            _items.RemoveAt(_items.Count - 1);
        }
    }
}
