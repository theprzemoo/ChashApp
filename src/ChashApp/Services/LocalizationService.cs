using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ChashApp.Models;

namespace ChashApp.Services;

public sealed class LocalizationService : INotifyPropertyChanged
{
    private readonly Dictionary<string, Dictionary<string, string>> _translations = new(StringComparer.OrdinalIgnoreCase);
    private string _currentLanguage = "en";

    public event PropertyChangedEventHandler? PropertyChanged;

    public LocalizationService()
    {
        LoadTranslations("en");
        LoadTranslations("pl");
        LoadTranslations("es");
        LoadTranslations("de");
        Languages = new ObservableCollection<LocalizedOption>
        {
            new("en", "English"),
            new("pl", "Polski"),
            new("es", "Español"),
            new("de", "Deutsch")
        };
    }

    public ObservableCollection<LocalizedOption> Languages { get; }

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage == value)
            {
                return;
            }

            _currentLanguage = value;
            OnPropertyChanged();
            RefreshLanguageLabels();
            OnPropertyChanged(nameof(AllKeysVersion));
        }
    }

    public int AllKeysVersion => CurrentLanguage.GetHashCode(StringComparison.Ordinal);

    public string this[string key] => Translate(key);

    public string Translate(string key)
    {
        if (_translations.TryGetValue(CurrentLanguage, out var values) && values.TryGetValue(key, out var translated))
        {
            return translated;
        }

        if (_translations.TryGetValue("en", out var fallback) && fallback.TryGetValue(key, out var fallbackValue))
        {
            return fallbackValue;
        }

        return key;
    }

    private void LoadTranslations(string language)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Resources", $"Strings.{language}.json");
        if (!File.Exists(file))
        {
            file = Path.Combine(AppContext.BaseDirectory, $"Strings.{language}.json");
        }

        if (!File.Exists(file))
        {
            _translations[language] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        var content = File.ReadAllText(file);
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(content)
                     ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _translations[language] = values;
    }

    private void RefreshLanguageLabels()
    {
        foreach (var item in Languages)
        {
            item.UpdateLabel(item.Key switch
            {
                "pl" => "Polski",
                "es" => "Español",
                "de" => "Deutsch",
                _ => "English"
            });
        }

        OnPropertyChanged(nameof(Languages));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
