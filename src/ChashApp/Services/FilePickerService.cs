using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace ChashApp.Services;

public sealed class FilePickerService
{
    public async Task<IReadOnlyList<string>> PickFilesAsync(Window? parent)
    {
        if (parent?.StorageProvider is null)
        {
            return Array.Empty<string>();
        }

        var results = await parent.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = "Select files"
        });

        return results.Select(file => file.TryGetLocalPath() ?? string.Empty)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();
    }

    public async Task<string?> PickFolderAsync(Window? parent)
    {
        if (parent?.StorageProvider is null)
        {
            return null;
        }

        var results = await parent.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Select folder"
        });

        return results.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> SaveTextAsync(Window? parent, string suggestedName, string content)
    {
        if (parent?.StorageProvider is null)
        {
            return null;
        }

        var file = await parent.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save file",
            SuggestedFileName = suggestedName
        });

        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        await File.WriteAllTextAsync(path, content);
        return path;
    }
}
