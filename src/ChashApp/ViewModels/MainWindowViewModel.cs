using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ChashApp.Models;
using ChashApp.Services;

namespace ChashApp.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly LocalizationService _localization;
    private readonly CryptoService _cryptoService;
    private readonly HistoryService _historyService;
    private readonly FilePickerService _filePickerService;
    private readonly SettingsService _settingsService;
    private readonly RecentFilesService _recentFilesService;
    private readonly DispatcherTimer _autoLockTimer;
    private Window? _hostWindow;
    private string _statusMessage = "Ready.";
    private string _selectedFilesDisplay = "No files selected";
    private string _selectedFolderDisplay = "No folder selected";
    private string _selectedFolderPath = string.Empty;
    private string _lastOutputPath = string.Empty;
    private string _filePassword = string.Empty;
    private string _notePassword = string.Empty;
    private string _noteInput = string.Empty;
    private string _noteOutput = string.Empty;
    private string _historyStatus = "History panel ready";
    private double _progressValue;
    private bool _isBusy;
    private string _selectedLanguage = "en";
    private string _selectedKdfKey = "pbkdf2-sha256";
    private string _passwordStrengthLabel = "Very weak";
    private double _passwordStrengthScore = 8;
    private string _settingsStatus = "Settings ready";
    private bool _saveHistoryOnExit = true;
    private bool _useAcrylic = true;
    private string _accentColor = "mint";
    private bool _secureDeleteAfterProcessing;
    private bool _privacyModeEnabled = true;
    private int _autoLockMinutes = 3;
    private bool _showFilePassword;
    private bool _showNotePassword;
    private string _historySearch = string.Empty;
    private int _currentHistoryPage = 1;
    private int _pageSize = 12;
    private bool _closeConfirmed;

    public MainWindowViewModel(
        LocalizationService localization,
        CryptoService cryptoService,
        HistoryService historyService,
        FilePickerService filePickerService,
        SettingsService settingsService,
        RecentFilesService recentFilesService)
    {
        _localization = localization;
        _cryptoService = cryptoService;
        _historyService = historyService;
        _filePickerService = filePickerService;
        _settingsService = settingsService;
        _recentFilesService = recentFilesService;
        _historyService.CollectionChanged += (_, _) => RaiseHistoryProperties();

        SelectedFiles = new ObservableCollection<string>();
        AvailableLanguages = localization.Languages;
        AvailableKdfs = new ObservableCollection<KdfOption>();
        RefreshKdfLabels();
        PageSizes = new ObservableCollection<int> { 8, 12, 25, 50 };
        HistoryStatus = $"{_historyService.Entries.Count} items";

        SelectFilesCommand = new AsyncRelayCommand(SelectFilesAsync);
        SelectFolderCommand = new AsyncRelayCommand(SelectFolderAsync);
        EncryptFilesCommand = new AsyncRelayCommand(() => ProcessFilesAsync(OperationKind.Encrypt), () => SelectedFiles.Count > 0 && !string.IsNullOrWhiteSpace(FilePassword));
        DecryptFilesCommand = new AsyncRelayCommand(() => ProcessFilesAsync(OperationKind.Decrypt), () => SelectedFiles.Count > 0 && !string.IsNullOrWhiteSpace(FilePassword));
        VerifyFilesCommand = new AsyncRelayCommand(VerifyFilesAsync, () => SelectedFiles.Count > 0 && !string.IsNullOrWhiteSpace(FilePassword));
        EncryptFolderCommand = new AsyncRelayCommand(EncryptFolderAsync, () => !string.IsNullOrWhiteSpace(SelectedFolderPath) && !string.IsNullOrWhiteSpace(FilePassword));
        SecureEraseSelectedCommand = new AsyncRelayCommand(SecureEraseSelectedAsync, () => SelectedFiles.Count > 0 || !string.IsNullOrWhiteSpace(SelectedFolderPath));
        EncryptNoteCommand = new RelayCommand(EncryptNote, () => !string.IsNullOrWhiteSpace(NoteInput) && !string.IsNullOrWhiteSpace(NotePassword));
        DecryptNoteCommand = new RelayCommand(DecryptNote, () => !string.IsNullOrWhiteSpace(NoteInput) && !string.IsNullOrWhiteSpace(NotePassword));
        SaveNoteCommand = new AsyncRelayCommand(SaveNoteAsync, () => !string.IsNullOrWhiteSpace(NoteOutput));
        CopyNoteOutputCommand = new AsyncRelayCommand(CopyNoteOutputAsync, () => !string.IsNullOrWhiteSpace(NoteOutput));
        OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder, () => !string.IsNullOrWhiteSpace(LastOutputPath));
        OpenOutputFileCommand = new RelayCommand(OpenOutputFile, () => !string.IsNullOrWhiteSpace(LastOutputPath) && File.Exists(LastOutputPath));
        CopyOutputPathCommand = new AsyncRelayCommand(CopyOutputPathAsync, () => !string.IsNullOrWhiteSpace(LastOutputPath));
        ExportHistoryJsonCommand = new AsyncRelayCommand(() => ExportHistoryAsync("json"));
        ExportHistoryCsvCommand = new AsyncRelayCommand(() => ExportHistoryAsync("csv"));
        ClearHistoryCommand = new RelayCommand(ClearHistory, () => _historyService.Entries.Count > 0);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ResetSettingsCommand = new AsyncRelayCommand(ResetSettingsAsync);
        ToggleFilePasswordCommand = new RelayCommand(() => ShowFilePassword = !ShowFilePassword);
        ToggleNotePasswordCommand = new RelayCommand(() => ShowNotePassword = !ShowNotePassword);
        NextHistoryPageCommand = new RelayCommand(() => CurrentHistoryPage++, () => CurrentHistoryPage < TotalHistoryPages);
        PreviousHistoryPageCommand = new RelayCommand(() => CurrentHistoryPage--, () => CurrentHistoryPage > 1);

        _autoLockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(_autoLockMinutes)
        };
        _autoLockTimer.Tick += (_, _) => AutoLockSession();
        ResetAutoLock();

        _localization.PropertyChanged += (_, _) => RaiseLocalizationProperties();
        RaiseLocalizationProperties();
        RaiseHistoryProperties();
        _ = LoadSettingsAsync();
    }

    public ObservableCollection<string> SelectedFiles { get; }
    public ObservableCollection<ToastNotification> Toasts { get; } = new();
    public ObservableCollection<HistoryEntry> VisibleHistoryEntries { get; } = new();
    public ObservableCollection<LocalizedOption> AvailableLanguages { get; }
    public ObservableCollection<KdfOption> AvailableKdfs { get; }
    public ObservableCollection<AccentPreset> AvailableAccentPresets { get; } = new()
    {
        new("mint", "Mint", "#2DF0D0"),
        new("cyan", "Cyan", "#22D3EE"),
        new("steel", "Steel", "#93C5FD"),
        new("amber", "Amber", "#FBBF24"),
        new("rose", "Rose", "#FB7185"),
        new("emerald", "Emerald", "#34D399"),
        new("slate", "Slate", "#94A3B8")
    };
    public ObservableCollection<int> PageSizes { get; }
    public ReadOnlyObservableCollection<RecentItem> RecentItems => _recentFilesService.Items;

    public AsyncRelayCommand SelectFilesCommand { get; }
    public AsyncRelayCommand SelectFolderCommand { get; }
    public AsyncRelayCommand EncryptFilesCommand { get; }
    public AsyncRelayCommand DecryptFilesCommand { get; }
    public AsyncRelayCommand VerifyFilesCommand { get; }
    public AsyncRelayCommand EncryptFolderCommand { get; }
    public AsyncRelayCommand SecureEraseSelectedCommand { get; }
    public RelayCommand EncryptNoteCommand { get; }
    public RelayCommand DecryptNoteCommand { get; }
    public AsyncRelayCommand SaveNoteCommand { get; }
    public AsyncRelayCommand CopyNoteOutputCommand { get; }
    public RelayCommand OpenOutputFolderCommand { get; }
    public RelayCommand OpenOutputFileCommand { get; }
    public AsyncRelayCommand CopyOutputPathCommand { get; }
    public AsyncRelayCommand ExportHistoryJsonCommand { get; }
    public AsyncRelayCommand ExportHistoryCsvCommand { get; }
    public RelayCommand ClearHistoryCommand { get; }
    public AsyncRelayCommand SaveSettingsCommand { get; }
    public AsyncRelayCommand ResetSettingsCommand { get; }
    public RelayCommand ToggleFilePasswordCommand { get; }
    public RelayCommand ToggleNotePasswordCommand { get; }
    public RelayCommand NextHistoryPageCommand { get; }
    public RelayCommand PreviousHistoryPageCommand { get; }

    public Window? HostWindow
    {
        get => _hostWindow;
        set => SetProperty(ref _hostWindow, value);
    }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value))
            {
                _localization.CurrentLanguage = value;
                ResetAutoLock();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string HistoryStatus
    {
        get => _historyStatus;
        set => SetProperty(ref _historyStatus, value);
    }

    public string SelectedFilesDisplay
    {
        get => _selectedFilesDisplay;
        set => SetProperty(ref _selectedFilesDisplay, value);
    }

    public string SelectedFolderDisplay
    {
        get => _selectedFolderDisplay;
        set => SetProperty(ref _selectedFolderDisplay, value);
    }

    public string SelectedFolderPath
    {
        get => _selectedFolderPath;
        set
        {
            if (SetProperty(ref _selectedFolderPath, value))
            {
                SelectedFolderDisplay = string.IsNullOrWhiteSpace(value)
                    ? _localization["files.noFolder"]
                    : Path.GetFileName(value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                EncryptFolderCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string FilePassword
    {
        get => _filePassword;
        set
        {
            if (SetProperty(ref _filePassword, value))
            {
                EncryptFilesCommand.RaiseCanExecuteChanged();
                DecryptFilesCommand.RaiseCanExecuteChanged();
                VerifyFilesCommand.RaiseCanExecuteChanged();
                EncryptFolderCommand.RaiseCanExecuteChanged();
                UpdatePasswordStrength();
                ResetAutoLock();
            }
        }
    }

    public string NotePassword
    {
        get => _notePassword;
        set
        {
            if (SetProperty(ref _notePassword, value))
            {
                EncryptNoteCommand.RaiseCanExecuteChanged();
                DecryptNoteCommand.RaiseCanExecuteChanged();
                UpdatePasswordStrength();
                ResetAutoLock();
            }
        }
    }

    public string NoteInput
    {
        get => _noteInput;
        set
        {
            if (SetProperty(ref _noteInput, value))
            {
                EncryptNoteCommand.RaiseCanExecuteChanged();
                DecryptNoteCommand.RaiseCanExecuteChanged();
                ResetAutoLock();
            }
        }
    }

    public string NoteOutput
    {
        get => _noteOutput;
        set
        {
            if (SetProperty(ref _noteOutput, value))
            {
                SaveNoteCommand.RaiseCanExecuteChanged();
                CopyNoteOutputCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string LastOutputPath
    {
        get => _lastOutputPath;
        set
        {
            if (SetProperty(ref _lastOutputPath, value))
            {
                OpenOutputFolderCommand.RaiseCanExecuteChanged();
                OpenOutputFileCommand.RaiseCanExecuteChanged();
                CopyOutputPathCommand.RaiseCanExecuteChanged();
                RaisePropertyChanged(nameof(CanOpenOutputFolder));
                RaisePropertyChanged(nameof(CanOpenOutputFile));
                RaisePropertyChanged(nameof(CanCopyOutputPath));
            }
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string SelectedKdfKey
    {
        get => _selectedKdfKey;
        set => SetProperty(ref _selectedKdfKey, value);
    }

    public string PasswordStrengthLabel
    {
        get => _passwordStrengthLabel;
        set => SetProperty(ref _passwordStrengthLabel, value);
    }

    public double PasswordStrengthScore
    {
        get => _passwordStrengthScore;
        set => SetProperty(ref _passwordStrengthScore, value);
    }

    public string SettingsStatus
    {
        get => _settingsStatus;
        set => SetProperty(ref _settingsStatus, value);
    }

    public bool SaveHistoryOnExit
    {
        get => _saveHistoryOnExit;
        set => SetProperty(ref _saveHistoryOnExit, value);
    }

    public bool UseAcrylic
    {
        get => _useAcrylic;
        set => SetProperty(ref _useAcrylic, value);
    }

    public string AccentColor
    {
        get => _accentColor;
        set
        {
            if (SetProperty(ref _accentColor, value))
            {
                RaisePropertyChanged(nameof(AccentPreviewHex));
            }
        }
    }

    public bool SecureDeleteAfterProcessing
    {
        get => _secureDeleteAfterProcessing;
        set => SetProperty(ref _secureDeleteAfterProcessing, value);
    }

    public bool PrivacyModeEnabled
    {
        get => _privacyModeEnabled;
        set => SetProperty(ref _privacyModeEnabled, value);
    }

    public int AutoLockMinutes
    {
        get => _autoLockMinutes;
        set
        {
            if (SetProperty(ref _autoLockMinutes, value))
            {
                _autoLockTimer.Interval = TimeSpan.FromMinutes(Math.Max(1, value));
                ResetAutoLock();
            }
        }
    }

    public bool ShowFilePassword
    {
        get => _showFilePassword;
        set => SetProperty(ref _showFilePassword, value);
    }

    public bool ShowNotePassword
    {
        get => _showNotePassword;
        set => SetProperty(ref _showNotePassword, value);
    }

    public string HistorySearch
    {
        get => _historySearch;
        set
        {
            if (SetProperty(ref _historySearch, value))
            {
                CurrentHistoryPage = 1;
                RaiseHistoryProperties();
            }
        }
    }

    public int CurrentHistoryPage
    {
        get => _currentHistoryPage;
        set
        {
            var next = Math.Max(1, Math.Min(value, TotalHistoryPages));
            if (SetProperty(ref _currentHistoryPage, next))
            {
                RaiseHistoryProperties();
            }
        }
    }

    public int PageSize
    {
        get => _pageSize;
        set
        {
            if (SetProperty(ref _pageSize, value))
            {
                CurrentHistoryPage = 1;
                RaiseHistoryProperties();
            }
        }
    }

    public IReadOnlyList<HistoryEntry> FilteredHistoryEntries
        => _historyService.Entries
            .Where(entry => string.IsNullOrWhiteSpace(HistorySearch)
                            || entry.Target.Contains(HistorySearch, StringComparison.OrdinalIgnoreCase)
                            || entry.Action.Contains(HistorySearch, StringComparison.OrdinalIgnoreCase)
                            || entry.Category.Contains(HistorySearch, StringComparison.OrdinalIgnoreCase)
                            || entry.Message.Contains(HistorySearch, StringComparison.OrdinalIgnoreCase))
            .ToList();

    public int TotalHistoryPages => Math.Max(1, (int)Math.Ceiling((double)FilteredHistoryEntries.Count / PageSize));
    public string CurrentPageLabel => $"{CurrentHistoryPage}/{TotalHistoryPages}";

    public string AppTitle => _localization["app.title"];
    public string AppSubtitle => _localization["app.subtitle"];
    public string FilesTab => _localization["tab.files"];
    public string NotesTab => _localization["tab.notes"];
    public string HistoryTab => _localization["tab.history"];
    public string SettingsTab => _localization["tab.settings"];
    public string AboutTab => _localization["tab.about"];
    public string FilesIntro => _localization["files.intro"];
    public string NotesIntro => _localization["notes.intro"];
    public string HistoryIntro => _localization["history.intro"];
    public string PasswordLabel => _localization["common.password"];
    public string SelectFilesLabel => _localization["files.select"];
    public string SelectFolderLabel => _localization["files.selectFolder"];
    public string EncryptFolderLabel => _localization["files.encryptFolder"];
    public string SecureEraseActionLabel => _localization["files.secureErase"];
    public string SelectedFolderLabel => _localization["files.selectedFolder"];
    public string EncryptLabel => _localization["common.encrypt"];
    public string DecryptLabel => _localization["common.decrypt"];
    public string IntegrityLabel => _localization["common.verify"];
    public string SaveNoteLabel => _localization["notes.save"];
    public string CopyNoteLabel => _localization["notes.copy"];
    public string NoteInputLabel => _localization["notes.input"];
    public string NoteOutputLabel => _localization["notes.output"];
    public string ExportJsonLabel => _localization["history.exportJson"];
    public string ExportCsvLabel => _localization["history.exportCsv"];
    public string ClearHistoryLabel => _localization["history.clear"];
    public string LanguageLabel => _localization["common.language"];
    public string StatusLabel => _localization["common.status"];
    public string OpenOutputFolderLabel => _localization["common.openOutputFolder"];
    public string OpenOutputFileLabel => _localization["common.openOutputFile"];
    public string CopyPathLabel => _localization["common.copyPath"];
    public string HistoryOpenLabel => _localization["history.open"];
    public string SelectedFilesLabel => _localization["files.selected"];
    public string KdfLabel => _localization["common.kdf"];
    public string KdfHint => _localization["common.kdfHint"];
    public string PasswordStrengthText => $"{_localization["common.passwordStrength"]}: {PasswordStrengthLabel}";
    public string PasswordStrengthColor => _cryptoService.EvaluatePassword(string.IsNullOrWhiteSpace(NotePassword) ? FilePassword : NotePassword).AccentHex;
    public double PasswordStrengthWidth => Math.Max(12, PasswordStrengthScore * 2.4);
    public int PasswordStrengthStepCount => Math.Clamp((int)Math.Ceiling(PasswordStrengthScore / 20d), 1, 5);
    public string PasswordStrengthSegment1 => PasswordStrengthStepCount >= 1 ? PasswordStrengthColor : "#163046";
    public string PasswordStrengthSegment2 => PasswordStrengthStepCount >= 2 ? PasswordStrengthColor : "#163046";
    public string PasswordStrengthSegment3 => PasswordStrengthStepCount >= 3 ? PasswordStrengthColor : "#163046";
    public string PasswordStrengthSegment4 => PasswordStrengthStepCount >= 4 ? PasswordStrengthColor : "#163046";
    public string PasswordStrengthSegment5 => PasswordStrengthStepCount >= 5 ? PasswordStrengthColor : "#163046";
    public string SettingsIntro => _localization["settings.intro"];
    public string SaveSettingsLabel => _localization["settings.save"];
    public string ResetSettingsLabel => _localization["settings.reset"];
    public string SaveHistoryLabel => _localization["settings.saveHistory"];
    public string UseAcrylicLabel => _localization["settings.useAcrylic"];
    public string AccentLabel => _localization["settings.accent"];
    public string AccentPreviewHex => AvailableAccentPresets.FirstOrDefault(item => item.Key == AccentColor)?.Hex ?? "#2DF0D0";
    public string SettingsStatusLabel => _localization["settings.status"];
    public string UninstallAppLabel => _localization["settings.uninstallApp"];
    public string DropHint => _localization["files.dropHint"];
    public string RecentFilesLabel => _localization["files.recent"];
    public string QuickActionsLabel => _localization["files.quickActions"];
    public string SecureDeleteLabel => _localization["settings.secureDelete"];
    public string PrivacyModeLabel => _localization["settings.privacyMode"];
    public string AutoLockLabel => _localization["settings.autoLock"];
    public string HistorySearchLabel => _localization["history.search"];
    public string PreviousPageLabel => _localization["history.previous"];
    public string NextPageLabel => _localization["history.next"];
    public string PageSizeLabel => _localization["history.pageSize"];
    public string PortableModeHint => _localization["settings.portableMode"];
    public string RecoveryHint => _localization["settings.recoveryHint"];
    public string PrivacyHint => _localization["settings.privacyHint"];
    public string ShowHidePasswordLabel => _localization["common.showHide"];
    public string ActionRequiresPasswordHint => _localization["common.actionNeedsPassword"];
    public string SecurityStackTitle => _localization["files.securityTitle"];
    public string SecurityStackBody => _localization["files.securityBody"];
    public string ProgressTitle => _localization["files.progressTitle"];
    public string NotesPreviewHint => _localization["notes.previewHint"];
    public string ReleasePrepTitle => _localization["settings.releasePrepTitle"];
    public string ReleasePrepBody => _localization["settings.releasePrepBody"];
    public string AppShellSubtitle => $"{_localization["app.shellSubtitle"]}  -  by theprzemoo";
    public string QuickMetricFiles => _localization["files.metricFiles"];
    public string QuickMetricExport => _localization["files.metricExport"];
    public string QuickMetricCipher => _localization["files.metricCipher"];
    public string QuickMetricHistory => _localization["files.metricHistory"];
    public string TotalOperationsMetric => RecentItems.Count.ToString();
    public string SuccessOperationsMetric => _historyService.Entries.Count(entry => entry.Success).ToString();
    public string FailedOperationsMetric => _historyService.Entries.Count(entry => !entry.Success).ToString();
    public string RecentItemsMetric => RecentItems.Count.ToString();
    public string CloseConfirmTitle => _localization["close.title"];
    public string CloseConfirmMessage => _localization["close.message"];
    public string CloseConfirmYes => _localization["close.yes"];
    public string CloseConfirmNo => _localization["close.no"];
    public string UninstallNowMessage => _localization["dialog.uninstallNow"];
    public string UninstallConfirmLabel => _localization["dialog.uninstallConfirm"];
    public string SecureEraseNowMessage => _localization["dialog.secureEraseNow"];
    public string AboutTitle => _localization["about.title"];
    public string AboutBody => _localization["about.body"];
    public string AboutHowTitle => _localization["about.howTitle"];
    public string AboutHowBody => _localization["about.howBody"];
    public string AboutPrivacyTitle => _localization["about.privacyTitle"];
    public string AboutPrivacyBody => _localization["about.privacyBody"];
    public string PanicHint => _localization["about.panicHint"];
    public bool IsSecureEraseAvailable => SelectedFiles.Count > 0 || !string.IsNullOrWhiteSpace(SelectedFolderPath);
    public bool CanOpenOutputFolder => !string.IsNullOrWhiteSpace(LastOutputPath);
    public bool CanOpenOutputFile => !string.IsNullOrWhiteSpace(LastOutputPath) && File.Exists(LastOutputPath);
    public bool CanCopyOutputPath => !string.IsNullOrWhiteSpace(LastOutputPath);
    public bool HasHistoryEntries => FilteredHistoryEntries.Count > 0;
    public string HistoryEmptyState => _localization["history.empty"];

    private KdfAlgorithm SelectedKdf
        => AvailableKdfs.FirstOrDefault(option => option.Key == SelectedKdfKey)?.Algorithm ?? KdfAlgorithm.Pbkdf2Sha256;

    private string SelectedAlgorithmLabel
        => $"AES-256-GCM + {AvailableKdfs.FirstOrDefault(option => option.Key == SelectedKdfKey)?.Label ?? "PBKDF2-SHA256"}";

    private async Task SelectFilesAsync()
    {
        var files = await _filePickerService.PickFilesAsync(HostWindow);
        ApplySelectedFiles(files);
    }

    private async Task SelectFolderAsync()
    {
        var folder = await _filePickerService.PickFolderAsync(HostWindow);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        SelectedFolderPath = folder;
        StatusMessage = _localization["status.folderSelected"];
        _recentFilesService.Track(folder, "Select folder");
        RaisePropertyChanged(nameof(IsSecureEraseAvailable));
        ResetAutoLock();
    }

    public void AddDroppedPaths(IEnumerable<string> paths)
    {
        var files = paths.SelectMany(path =>
        {
            if (File.Exists(path))
            {
                return new[] { path };
            }

            return Directory.Exists(path)
                ? Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                : Array.Empty<string>();
        });

        ApplySelectedFiles(files);
        StatusMessage = _localization["status.filesLoaded"];
    }

    public async Task PanicModeAsync()
    {
        if (TopLevel.GetTopLevel(HostWindow)?.Clipboard is not null)
        {
            await TopLevel.GetTopLevel(HostWindow)!.Clipboard!.SetTextAsync(string.Empty);
        }

        FilePassword = string.Empty;
        NotePassword = string.Empty;
        NoteInput = string.Empty;
        NoteOutput = string.Empty;
        SelectedFiles.Clear();
        SelectedFilesDisplay = _localization["files.none"];
        SelectedFolderPath = string.Empty;
        StatusMessage = _localization["status.panicTriggered"];
        EncryptFilesCommand.RaiseCanExecuteChanged();
        DecryptFilesCommand.RaiseCanExecuteChanged();
        VerifyFilesCommand.RaiseCanExecuteChanged();
        EncryptFolderCommand.RaiseCanExecuteChanged();
        SecureEraseSelectedCommand.RaiseCanExecuteChanged();
        RaisePropertyChanged(nameof(IsSecureEraseAvailable));
    }

    private void ApplySelectedFiles(IEnumerable<string> files)
    {
        SelectedFiles.Clear();
        foreach (var file in files.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(file) && File.Exists(file))
            {
                SelectedFiles.Add(file);
                _recentFilesService.Track(file, "Selected");
            }
        }

        SelectedFilesDisplay = SelectedFiles.Count == 0
            ? _localization["files.none"]
            : string.Join(Environment.NewLine, SelectedFiles.Select(Path.GetFileName));

        EncryptFilesCommand.RaiseCanExecuteChanged();
        DecryptFilesCommand.RaiseCanExecuteChanged();
        VerifyFilesCommand.RaiseCanExecuteChanged();
        EncryptFolderCommand.RaiseCanExecuteChanged();
        SecureEraseSelectedCommand.RaiseCanExecuteChanged();
        RaisePropertyChanged(nameof(IsSecureEraseAvailable));
    }

    private async Task ProcessFilesAsync(OperationKind kind)
    {
        IsBusy = true;
        ProgressValue = 0;
        var progress = new Progress<double>(value => ProgressValue = value * 100d);

        try
        {
            if (kind == OperationKind.Encrypt)
            {
                await _cryptoService.EncryptFilesAsync(SelectedFiles, FilePassword, SelectedKdf, progress);
                LastOutputPath = _cryptoService.GetEncryptedOutputPath(SelectedFiles.Last());
                if (SecureDeleteAfterProcessing)
                {
                    await SecureDeleteSelectedAsync();
                }

                StatusMessage = $"{_localization["status.filesEncrypted"]} {LastOutputPath}";
                TrackFileBatch("Encrypt", true, StatusMessage);
                PushToast(_localization["common.encrypt"], StatusMessage);
            }
            else
            {
                await _cryptoService.DecryptFilesAsync(SelectedFiles, FilePassword, progress);
                LastOutputPath = _cryptoService.GetPredictedDecryptedOutputPath(SelectedFiles.Last());
                if (SecureDeleteAfterProcessing)
                {
                    await SecureDeleteSelectedAsync();
                }

                StatusMessage = $"{_localization["status.filesDecrypted"]} {LastOutputPath}";
                TrackFileBatch("Decrypt", true, StatusMessage);
                PushToast(_localization["common.decrypt"], StatusMessage);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or FileNotFoundException or CryptographicException)
        {
            StatusMessage = exception.Message;
            TrackFileBatch(kind.ToString(), false, exception.Message);
            PushToast("Error", exception.Message, true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task EncryptFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedFolderPath))
        {
            return;
        }

        IsBusy = true;
        ProgressValue = 100;

        try
        {
            var outputPath = await _cryptoService.EncryptDirectoryAsSingleFileAsync(SelectedFolderPath, FilePassword, SelectedKdf);
            LastOutputPath = outputPath;
            StatusMessage = $"{_localization["status.folderEncrypted"]}: {Path.GetFileName(outputPath)}";
            HistoryStatus = StatusMessage;
            _historyService.Add("Folder", "Encrypt", outputPath, true, StatusMessage, SelectedAlgorithmLabel);
            _recentFilesService.Track(outputPath, "Encrypt folder");
            PushToast(_localization["common.encrypt"], StatusMessage);
            RaiseHistoryProperties();
        }
        catch (Exception exception) when (exception is InvalidOperationException or FileNotFoundException or DirectoryNotFoundException or CryptographicException)
        {
            StatusMessage = exception.Message;
            HistoryStatus = exception.Message;
            _historyService.Add("Folder", "Encrypt", SelectedFolderPath, false, exception.Message, SelectedAlgorithmLabel);
            PushToast("Error", exception.Message, true);
            RaiseHistoryProperties();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task VerifyFilesAsync()
    {
        try
        {
            foreach (var file in SelectedFiles)
            {
                if (!file.EndsWith(".chash", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(_localization["status.verifyRequiresEncrypted"]);
                }

                _cryptoService.VerifyEncryptedPayload(file, FilePassword);
                var verifyMessage = $"{_localization["status.integrityOk"]}: {Path.GetFileName(file)}";
                _historyService.Add("File", "Verify", file, true, verifyMessage, SelectedAlgorithmLabel);
                _recentFilesService.Track(file, "Verify");
                StatusMessage = verifyMessage;
                LastOutputPath = file;
                PushToast(_localization["common.verify"], verifyMessage);
            }

            HistoryStatus = StatusMessage;
            RaiseHistoryProperties();
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            HistoryStatus = exception.Message;
            PushToast("Error", exception.Message, true);
        }

        return Task.CompletedTask;
    }

    private async Task SecureEraseSelectedAsync()
    {
        try
        {
            foreach (var file in SelectedFiles.ToList())
            {
                await _cryptoService.SecureDeleteAsync(file);
                _historyService.Add("File", "SecureErase", file, true, _localization["status.secureEraseDone"], "Secure erase");
            }

            SelectedFiles.Clear();
            SelectedFilesDisplay = _localization["files.none"];
            SecureEraseSelectedCommand.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(IsSecureEraseAvailable));
            EncryptFilesCommand.RaiseCanExecuteChanged();
            DecryptFilesCommand.RaiseCanExecuteChanged();
            VerifyFilesCommand.RaiseCanExecuteChanged();
            StatusMessage = _localization["status.secureEraseDone"];
            HistoryStatus = StatusMessage;
            PushToast(SecureEraseActionLabel, StatusMessage);
            RaiseHistoryProperties();
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            HistoryStatus = exception.Message;
            PushToast("Error", exception.Message, true);
        }
    }

    private void EncryptNote()
    {
        try
        {
            var result = _cryptoService.EncryptNote(NoteInput, NotePassword, SelectedKdf);
            NoteOutput = result.CipherText;
            StatusMessage = _localization["status.noteEncrypted"];
            HistoryStatus = StatusMessage;
            _historyService.Add("Note", "Encrypt", Preview(NoteInput), true, StatusMessage, SelectedAlgorithmLabel);
            _recentFilesService.Track("In-app note", "Encrypt");
            PushToast(_localization["common.encrypt"], StatusMessage);
            RaiseHistoryProperties();
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            HistoryStatus = exception.Message;
            _historyService.Add("Note", "Encrypt", Preview(NoteInput), false, exception.Message, SelectedAlgorithmLabel);
            PushToast("Error", exception.Message, true);
            RaiseHistoryProperties();
        }
    }

    private void DecryptNote()
    {
        try
        {
            var result = _cryptoService.DecryptNote(NoteInput, NotePassword);
            NoteOutput = result.PlainText;
            StatusMessage = _localization["status.noteDecrypted"];
            HistoryStatus = StatusMessage;
            _historyService.Add("Note", "Decrypt", Preview(NoteInput), true, StatusMessage, SelectedAlgorithmLabel);
            PushToast(_localization["common.decrypt"], StatusMessage);
            RaiseHistoryProperties();
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            HistoryStatus = exception.Message;
            _historyService.Add("Note", "Decrypt", Preview(NoteInput), false, exception.Message, SelectedAlgorithmLabel);
            PushToast("Error", exception.Message, true);
            RaiseHistoryProperties();
        }
    }

    private async Task SaveNoteAsync()
    {
        var path = await _filePickerService.SaveTextAsync(HostWindow, "chash-note.txt", NoteOutput);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        LastOutputPath = path;
        StatusMessage = $"{_localization["status.noteSaved"]}: {Path.GetFileName(path)}";
        HistoryStatus = StatusMessage;
        _historyService.Add("Note", "Save", path, true, StatusMessage, "Saved output");
        _recentFilesService.Track(path, "Save note");
        PushToast(SaveNoteLabel, StatusMessage);
        RaiseHistoryProperties();
    }

    private async Task CopyNoteOutputAsync()
    {
        if (TopLevel.GetTopLevel(HostWindow)?.Clipboard is null || string.IsNullOrWhiteSpace(NoteOutput))
        {
            return;
        }

        await TopLevel.GetTopLevel(HostWindow)!.Clipboard!.SetTextAsync(NoteOutput);
        StatusMessage = _localization["status.noteCopied"];
        HistoryStatus = StatusMessage;
        PushToast(CopyNoteLabel, StatusMessage);
    }

    private async Task ExportHistoryAsync(string format)
    {
        try
        {
            var exportDirectory = Path.Combine(AppContext.BaseDirectory, "HistoryExport");
            Directory.CreateDirectory(exportDirectory);
            var extension = format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "json" : "csv";
            var path = Path.Combine(exportDirectory, $"history-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}");

            if (extension == "json")
            {
                await _historyService.ExportJsonAsync(path);
            }
            else
            {
                await _historyService.ExportCsvAsync(path);
            }

            HistoryStatus = $"{_localization["status.historyExported"]}: {path}";
            _recentFilesService.Track(path, "Export history");
            PushToast(_localization[$"history.export{(extension == "json" ? "Json" : "Csv")}"], HistoryStatus);
        }
        catch (Exception exception)
        {
            HistoryStatus = exception.Message;
            PushToast("Error", exception.Message, true);
        }
    }

    private async Task SaveSettingsAsync()
    {
        var settings = new AppSettings
        {
            Language = SelectedLanguage,
            DefaultKdf = SelectedKdfKey,
            SaveHistoryOnExit = SaveHistoryOnExit,
            UseAcrylic = UseAcrylic,
            AccentColor = AccentColor,
            SecureDeleteAfterProcessing = SecureDeleteAfterProcessing,
            PrivacyModeEnabled = PrivacyModeEnabled,
            AutoLockMinutes = AutoLockMinutes
        };

        await _settingsService.SaveAsync(settings);
        SettingsStatus = $"{_localization["settings.saved"]}: {Path.GetFileName(_settingsService.SettingsPath)}. {_localization["settings.changed"]}";
        PushToast(SaveSettingsLabel, SettingsStatus);
    }

    private async Task ResetSettingsAsync()
    {
        await _settingsService.ResetAsync();
        SelectedLanguage = "en";
        SelectedKdfKey = "pbkdf2-sha256";
        SaveHistoryOnExit = true;
        UseAcrylic = true;
        AccentColor = "mint";
        SecureDeleteAfterProcessing = false;
        PrivacyModeEnabled = true;
        AutoLockMinutes = 3;
        SettingsStatus = _localization["settings.resetDone"];
        RaiseLocalizationProperties();
        PushToast(ResetSettingsLabel, SettingsStatus);
    }

    private async Task LoadSettingsAsync()
    {
        var settings = await _settingsService.LoadAsync();
        SelectedLanguage = settings.Language;
        SelectedKdfKey = settings.DefaultKdf;
        SaveHistoryOnExit = settings.SaveHistoryOnExit;
        UseAcrylic = settings.UseAcrylic;
        AccentColor = settings.AccentColor;
        SecureDeleteAfterProcessing = settings.SecureDeleteAfterProcessing;
        PrivacyModeEnabled = settings.PrivacyModeEnabled;
        AutoLockMinutes = settings.AutoLockMinutes;
        SettingsStatus = $"{_localization["settings.loaded"]}: {Path.GetFileName(_settingsService.SettingsPath)}";
        RaiseHistoryProperties();
    }

    private async Task SecureDeleteSelectedAsync()
    {
        if (SelectedFiles.Count > 0)
        {
            foreach (var file in SelectedFiles.ToList())
            {
                await _cryptoService.SecureDeleteAsync(file);
                _historyService.Add("File", "SecureDelete", file, true, _localization["status.secureDeleteDone"], "Secure delete");
            }
        }
        else if (!string.IsNullOrWhiteSpace(SelectedFolderPath) && Directory.Exists(SelectedFolderPath))
        {
            foreach (var file in Directory.GetFiles(SelectedFolderPath, "*", SearchOption.AllDirectories))
            {
                await _cryptoService.SecureDeleteAsync(file);
            }

            Directory.Delete(SelectedFolderPath, true);
            _historyService.Add("Folder", "SecureDelete", SelectedFolderPath, true, _localization["status.secureDeleteDone"], "Secure delete");
            SelectedFolderPath = string.Empty;
        }

        RaiseHistoryProperties();
    }

    private void TrackFileBatch(string action, bool success, string message)
    {
        foreach (var file in SelectedFiles)
        {
            _historyService.Add("File", action, file, success, message, SelectedAlgorithmLabel);
            _recentFilesService.Track(file, action);
        }

        RaiseHistoryProperties();
    }

    private void AutoLockSession()
    {
        FilePassword = string.Empty;
        NotePassword = string.Empty;
        StatusMessage = _localization["status.autoLocked"];
    }

    private void ResetAutoLock()
    {
        _autoLockTimer.Stop();
        _autoLockTimer.Interval = TimeSpan.FromMinutes(Math.Max(1, AutoLockMinutes));
        _autoLockTimer.Start();
    }

    private void UpdatePasswordStrength()
    {
        var source = string.IsNullOrWhiteSpace(NotePassword) ? FilePassword : NotePassword;
        var result = _cryptoService.EvaluatePassword(source);
        PasswordStrengthLabel = _localization[$"password.{result.Key}"];
        PasswordStrengthScore = result.Score;
        RaisePropertyChanged(nameof(PasswordStrengthText));
        RaisePropertyChanged(nameof(PasswordStrengthColor));
        RaisePropertyChanged(nameof(PasswordStrengthWidth));
        RaisePropertyChanged(nameof(PasswordStrengthSegment1));
        RaisePropertyChanged(nameof(PasswordStrengthSegment2));
        RaisePropertyChanged(nameof(PasswordStrengthSegment3));
        RaisePropertyChanged(nameof(PasswordStrengthSegment4));
        RaisePropertyChanged(nameof(PasswordStrengthSegment5));
    }

    private void OpenOutputFolder()
    {
        if (string.IsNullOrWhiteSpace(LastOutputPath))
        {
            return;
        }

        var targetDirectory = Directory.Exists(LastOutputPath)
            ? LastOutputPath
            : Path.GetDirectoryName(LastOutputPath);

        if (string.IsNullOrWhiteSpace(targetDirectory) || !Directory.Exists(targetDirectory))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = targetDirectory,
            UseShellExecute = true
        });
    }

    private void OpenOutputFile()
    {
        if (string.IsNullOrWhiteSpace(LastOutputPath) || !File.Exists(LastOutputPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{LastOutputPath}\"",
            UseShellExecute = true
        });
    }

    private async Task CopyOutputPathAsync()
    {
        if (TopLevel.GetTopLevel(HostWindow)?.Clipboard is null || string.IsNullOrWhiteSpace(LastOutputPath))
        {
            return;
        }

        await TopLevel.GetTopLevel(HostWindow)!.Clipboard!.SetTextAsync(LastOutputPath);
        StatusMessage = $"{_localization["status.pathCopied"]}: {LastOutputPath}";
        PushToast(CopyPathLabel, StatusMessage);
    }

    public void OpenHistoryEntry(HistoryEntry entry)
    {
        if (entry is null)
        {
            return;
        }

        var fullPath = ResolveHistoryTarget(entry.Target);
        if (!string.IsNullOrWhiteSpace(fullPath))
        {
            LastOutputPath = fullPath;
            OpenOutputFolder();
            PushToast(HistoryOpenLabel, fullPath);
            return;
        }

        PushToast("Info", entry.Target, true);
    }

    private void ClearHistory()
    {
        _historyService.Clear();
        HistoryStatus = _localization["history.cleared"];
        RaiseHistoryProperties();
    }

    private string Preview(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(empty)";
        }

        return value.Length <= 42 ? value : $"{value[..42]}...";
    }

    private void RaiseLocalizationProperties()
    {
        RefreshKdfLabels();
        RaisePropertyChanged(nameof(AppTitle));
        RaisePropertyChanged(nameof(AppSubtitle));
        RaisePropertyChanged(nameof(FilesTab));
        RaisePropertyChanged(nameof(NotesTab));
        RaisePropertyChanged(nameof(HistoryTab));
        RaisePropertyChanged(nameof(SettingsTab));
        RaisePropertyChanged(nameof(AboutTab));
        RaisePropertyChanged(nameof(FilesIntro));
        RaisePropertyChanged(nameof(NotesIntro));
        RaisePropertyChanged(nameof(HistoryIntro));
        RaisePropertyChanged(nameof(PasswordLabel));
        RaisePropertyChanged(nameof(SelectFilesLabel));
        RaisePropertyChanged(nameof(SelectFolderLabel));
        RaisePropertyChanged(nameof(EncryptFolderLabel));
        RaisePropertyChanged(nameof(SecureEraseActionLabel));
        RaisePropertyChanged(nameof(SelectedFolderLabel));
        RaisePropertyChanged(nameof(EncryptLabel));
        RaisePropertyChanged(nameof(DecryptLabel));
        RaisePropertyChanged(nameof(IntegrityLabel));
        RaisePropertyChanged(nameof(SaveNoteLabel));
        RaisePropertyChanged(nameof(CopyNoteLabel));
        RaisePropertyChanged(nameof(NoteInputLabel));
        RaisePropertyChanged(nameof(NoteOutputLabel));
        RaisePropertyChanged(nameof(ExportJsonLabel));
        RaisePropertyChanged(nameof(ExportCsvLabel));
        RaisePropertyChanged(nameof(ClearHistoryLabel));
        RaisePropertyChanged(nameof(LanguageLabel));
        RaisePropertyChanged(nameof(StatusLabel));
        RaisePropertyChanged(nameof(OpenOutputFolderLabel));
        RaisePropertyChanged(nameof(OpenOutputFileLabel));
        RaisePropertyChanged(nameof(CopyPathLabel));
        RaisePropertyChanged(nameof(SelectedFilesLabel));
        RaisePropertyChanged(nameof(KdfLabel));
        RaisePropertyChanged(nameof(KdfHint));
        RaisePropertyChanged(nameof(ActionRequiresPasswordHint));
        RaisePropertyChanged(nameof(SettingsIntro));
        RaisePropertyChanged(nameof(SaveSettingsLabel));
        RaisePropertyChanged(nameof(ResetSettingsLabel));
        RaisePropertyChanged(nameof(SaveHistoryLabel));
        RaisePropertyChanged(nameof(UseAcrylicLabel));
        RaisePropertyChanged(nameof(AccentLabel));
        RaisePropertyChanged(nameof(AccentPreviewHex));
        RaisePropertyChanged(nameof(SettingsStatusLabel));
        RaisePropertyChanged(nameof(UninstallAppLabel));
        RaisePropertyChanged(nameof(DropHint));
        RaisePropertyChanged(nameof(RecentFilesLabel));
        RaisePropertyChanged(nameof(QuickActionsLabel));
        RaisePropertyChanged(nameof(SecureDeleteLabel));
        RaisePropertyChanged(nameof(PrivacyModeLabel));
        RaisePropertyChanged(nameof(AutoLockLabel));
        RaisePropertyChanged(nameof(HistorySearchLabel));
        RaisePropertyChanged(nameof(PreviousPageLabel));
        RaisePropertyChanged(nameof(NextPageLabel));
        RaisePropertyChanged(nameof(PageSizeLabel));
        RaisePropertyChanged(nameof(HistoryEmptyState));
        RaisePropertyChanged(nameof(PortableModeHint));
        RaisePropertyChanged(nameof(RecoveryHint));
        RaisePropertyChanged(nameof(PrivacyHint));
        RaisePropertyChanged(nameof(ShowHidePasswordLabel));
        RaisePropertyChanged(nameof(SecurityStackTitle));
        RaisePropertyChanged(nameof(SecurityStackBody));
        RaisePropertyChanged(nameof(ProgressTitle));
        RaisePropertyChanged(nameof(NotesPreviewHint));
        RaisePropertyChanged(nameof(ReleasePrepTitle));
        RaisePropertyChanged(nameof(ReleasePrepBody));
        RaisePropertyChanged(nameof(AppShellSubtitle));
        RaisePropertyChanged(nameof(QuickMetricFiles));
        RaisePropertyChanged(nameof(QuickMetricExport));
        RaisePropertyChanged(nameof(QuickMetricCipher));
        RaisePropertyChanged(nameof(QuickMetricHistory));
        RaisePropertyChanged(nameof(TotalOperationsMetric));
        RaisePropertyChanged(nameof(SuccessOperationsMetric));
        RaisePropertyChanged(nameof(FailedOperationsMetric));
        RaisePropertyChanged(nameof(RecentItemsMetric));
        RaisePropertyChanged(nameof(CloseConfirmTitle));
        RaisePropertyChanged(nameof(CloseConfirmMessage));
        RaisePropertyChanged(nameof(CloseConfirmYes));
        RaisePropertyChanged(nameof(CloseConfirmNo));
        RaisePropertyChanged(nameof(UninstallNowMessage));
        RaisePropertyChanged(nameof(UninstallConfirmLabel));
        RaisePropertyChanged(nameof(SecureEraseNowMessage));
        RaisePropertyChanged(nameof(AboutTitle));
        RaisePropertyChanged(nameof(AboutBody));
        RaisePropertyChanged(nameof(AboutHowTitle));
        RaisePropertyChanged(nameof(AboutHowBody));
        RaisePropertyChanged(nameof(AboutPrivacyTitle));
        RaisePropertyChanged(nameof(AboutPrivacyBody));
        RaisePropertyChanged(nameof(PanicHint));
        RaisePropertyChanged(nameof(PasswordStrengthText));
        RaisePropertyChanged(nameof(PasswordStrengthColor));
        RaisePropertyChanged(nameof(PasswordStrengthWidth));
        RaisePropertyChanged(nameof(PasswordStrengthStepCount));
        RaisePropertyChanged(nameof(PasswordStrengthSegment1));
        RaisePropertyChanged(nameof(PasswordStrengthSegment2));
        RaisePropertyChanged(nameof(PasswordStrengthSegment3));
        RaisePropertyChanged(nameof(PasswordStrengthSegment4));
        RaisePropertyChanged(nameof(PasswordStrengthSegment5));
        SelectedFilesDisplay = SelectedFiles.Count == 0 ? _localization["files.none"] : SelectedFilesDisplay;
        SelectedFolderDisplay = string.IsNullOrWhiteSpace(SelectedFolderPath)
            ? _localization["files.noFolder"]
            : Path.GetFileName(SelectedFolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    private void RaiseHistoryProperties()
    {
        RaisePropertyChanged(nameof(FilteredHistoryEntries));
        RaisePropertyChanged(nameof(TotalHistoryPages));
        RaisePropertyChanged(nameof(CurrentPageLabel));
        RaisePropertyChanged(nameof(HasHistoryEntries));
        RaisePropertyChanged(nameof(TotalOperationsMetric));
        RaisePropertyChanged(nameof(SuccessOperationsMetric));
        RaisePropertyChanged(nameof(FailedOperationsMetric));
        RaisePropertyChanged(nameof(RecentItemsMetric));
        VisibleHistoryEntries.Clear();
        foreach (var entry in FilteredHistoryEntries.Skip((CurrentHistoryPage - 1) * PageSize).Take(PageSize))
        {
            VisibleHistoryEntries.Add(entry);
        }
        HistoryStatus = $"{FilteredHistoryEntries.Count} {(_selectedLanguage == "pl" ? "wpisow" : _selectedLanguage == "de" ? "Eintraege" : _selectedLanguage == "es" ? "registros" : "items")}";
        ClearHistoryCommand.RaiseCanExecuteChanged();
        PreviousHistoryPageCommand.RaiseCanExecuteChanged();
        NextHistoryPageCommand.RaiseCanExecuteChanged();
    }

    private void PushToast(string title, string message, bool isError = false)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var toast = new ToastNotification
        {
            Title = title,
            Message = message,
            BorderHex = isError ? "#F87171" : AccentPreviewHex,
            BackgroundHex = isError ? "#241315" : "#10202D"
        };

        Toasts.Insert(0, toast);
        _ = DismissToastAsync(toast.Id);
    }

    private async Task DismissToastAsync(Guid toastId)
    {
        await Task.Delay(3600);
        var toast = Toasts.FirstOrDefault(item => item.Id == toastId);
        if (toast is not null)
        {
            Toasts.Remove(toast);
        }
    }

    private string ResolveHistoryTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(target) && (File.Exists(target) || Directory.Exists(target)))
        {
            return target;
        }

        var publishCandidate = Path.Combine(AppContext.BaseDirectory, target);
        if (File.Exists(publishCandidate) || Directory.Exists(publishCandidate))
        {
            return publishCandidate;
        }

        var localDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChashApp");
        var localCandidate = Path.Combine(localDataRoot, target);
        if (File.Exists(localCandidate) || Directory.Exists(localCandidate))
        {
            return localCandidate;
        }

        return string.Empty;
    }

    private void RefreshKdfLabels()
    {
        AvailableKdfs.Clear();
        AvailableKdfs.Add(new KdfOption("pbkdf2-sha256", _localization["kdf.pbkdf2sha256"], KdfAlgorithm.Pbkdf2Sha256));
        AvailableKdfs.Add(new KdfOption("pbkdf2-sha512", _localization["kdf.pbkdf2sha512"], KdfAlgorithm.Pbkdf2Sha512));
        AvailableKdfs.Add(new KdfOption("argon2id", _localization["kdf.argon2id"], KdfAlgorithm.Argon2Id));
        if (AvailableKdfs.All(option => option.Key != SelectedKdfKey))
        {
            SelectedKdfKey = "pbkdf2-sha256";
        }
    }

    public bool CloseConfirmed
    {
        get => _closeConfirmed;
        set => SetProperty(ref _closeConfirmed, value);
    }
}
