using System.Text.Json;
using ChashApp.Models;

namespace ChashApp.Services;

public sealed class SettingsService
{
    private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };
    private readonly string _settingsDirectory;
    private readonly string _settingsPath;

    public SettingsService()
    {
        var portableRequested = string.Equals(Environment.GetEnvironmentVariable("CHASHAPP_PORTABLE"), "1", StringComparison.OrdinalIgnoreCase)
                                || File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.mode"));

        _settingsDirectory = portableRequested
            ? Path.Combine(AppContext.BaseDirectory, "PortableData")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChashApp");
        _settingsPath = Path.Combine(_settingsDirectory, "settings.json");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            await using var stream = File.OpenRead(_settingsPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _serializerOptions, cancellationToken);
            return settings ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_settingsDirectory);
        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, _serializerOptions, cancellationToken);
    }

    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                File.Delete(_settingsPath);
            }
        }
        catch
        {
        }

        return Task.CompletedTask;
    }

    public string SettingsPath => _settingsPath;
}
