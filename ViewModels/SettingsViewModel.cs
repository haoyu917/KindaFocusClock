using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MyClock.Commands;
using MyClock.Models;
using MyClock.Services;

namespace MyClock.ViewModels;

// Edits a draft of the settings; nothing applies until Save. Clearing history and
// opening the data folder act immediately.
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IFocusSessionRepository _repository;
    private readonly IFocusSessionRecorder _focusSessionRecorder;
    private readonly IWindowTrackingService _windowTrackingService;
    private readonly IShellService _shellService;
    private readonly RelayCommand _saveCommand;
    private readonly RelayCommand _addExcludedAppCommand;

    // Numbers are edited as text so invalid input can be explained instead of rejected.
    private string _lockedInPercent = string.Empty;
    private string _inTheZonePercent = string.Empty;
    private string _halfHerePercent = string.Empty;
    private string _minimumRatedSeconds = string.Empty;
    private string _historyRetentionDays = string.Empty;
    private CommentLanguage _commentLanguage;
    private bool _use24HourClock;
    private bool _showSeconds;
    private string _newExcludedApp = string.Empty;
    private string _statusMessage = string.Empty;

    // The settings the form describes, or null while the form has invalid values.
    private AppSettings? _draft;
    private string? _validationError;

    // The saved settings the form was last loaded from. Edits are measured against it.
    private AppSettings _baseline = new();

    public SettingsViewModel(
        ISettingsService settingsService,
        IFocusSessionRepository repository,
        IFocusSessionRecorder focusSessionRecorder,
        IWindowTrackingService windowTrackingService,
        IShellService shellService)
    {
        _settingsService = settingsService;
        _repository = repository;
        _focusSessionRecorder = focusSessionRecorder;
        _windowTrackingService = windowTrackingService;
        _shellService = shellService;

        _saveCommand = new RelayCommand(Save, () => _draft is not null && HasChanges);
        _addExcludedAppCommand = new RelayCommand(AddExcludedApp, () => !string.IsNullOrWhiteSpace(NewExcludedApp));
        RemoveExcludedAppCommand = new RelayCommand<string>(RemoveExcludedApp);
        RestoreDefaultsCommand = new RelayCommand(RestoreDefaults);
        ClearHistoryCommand = new RelayCommand(ClearHistory);
        OpenDataFolderCommand = new RelayCommand(OpenDataFolder);

        LoadForm(_settingsService.Current);
    }

    public string LockedInPercent
    {
        get => _lockedInPercent;
        set => SetFormField(ref _lockedInPercent, value);
    }

    public string InTheZonePercent
    {
        get => _inTheZonePercent;
        set => SetFormField(ref _inTheZonePercent, value);
    }

    public string HalfHerePercent
    {
        get => _halfHerePercent;
        set => SetFormField(ref _halfHerePercent, value);
    }

    public string MinimumRatedSeconds
    {
        get => _minimumRatedSeconds;
        set => SetFormField(ref _minimumRatedSeconds, value);
    }

    public string HistoryRetentionDays
    {
        get => _historyRetentionDays;
        set => SetFormField(ref _historyRetentionDays, value);
    }

    public IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new(CommentLanguage.English, "English"),
        new(CommentLanguage.Chinese, "中文")
    ];

    public CommentLanguage CommentLanguage
    {
        get => _commentLanguage;
        set => SetFormField(ref _commentLanguage, value);
    }

    public bool Use24HourClock
    {
        get => _use24HourClock;
        set => SetFormField(ref _use24HourClock, value);
    }

    public bool ShowSeconds
    {
        get => _showSeconds;
        set => SetFormField(ref _showSeconds, value);
    }

    // Process names that do not count as switches.
    public ObservableCollection<string> ExcludedApps { get; } = [];

    public bool HasNoExcludedApps => ExcludedApps.Count == 0;

    // Process names of open windows, offered when adding an excluded app.
    public ObservableCollection<string> RunningApps { get; } = [];

    public string NewExcludedApp
    {
        get => _newExcludedApp;
        set
        {
            if (SetProperty(ref _newExcludedApp, value ?? string.Empty))
            {
                _addExcludedAppCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ValidationMessage => _validationError ?? string.Empty;

    public bool HasValidationError => _validationError is not null;

    public bool HasNoValidationError => !HasValidationError;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    // True when the form differs from the saved settings.
    public bool HasChanges => _draft is null || !_draft.HasSameValues(_settingsService.Current);

    public ICommand SaveCommand => _saveCommand;

    public ICommand RestoreDefaultsCommand { get; }

    public ICommand AddExcludedAppCommand => _addExcludedAppCommand;

    public ICommand RemoveExcludedAppCommand { get; }

    public ICommand ClearHistoryCommand { get; }

    public ICommand OpenDataFolderCommand { get; }

    // Called each time the settings page is shown.
    public void OnPageLoaded()
    {
        // Unsaved edits survive switching pages; otherwise show the saved settings.
        if (!HasUnsavedEdits)
        {
            LoadForm(_settingsService.Current);
        }

        RefreshRunningApps();
    }

    private void SetFormField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!SetProperty(ref field, value, propertyName))
        {
            return;
        }

        StatusMessage = string.Empty;
        OnFormChanged();
    }

    // True when the form differs from the saved settings it was loaded from.
    private bool HasUnsavedEdits => _draft is null || !_draft.HasSameValues(_baseline);

    // Fills the form. A load from saved settings also resets the edit baseline;
    // restoring defaults does not, because the defaults are not saved yet.
    private void LoadForm(AppSettings settings, bool isSaved = true)
    {
        if (isSaved)
        {
            _baseline = settings;
        }

        _lockedInPercent = settings.LockedInPercent.ToString(CultureInfo.InvariantCulture);
        _inTheZonePercent = settings.InTheZonePercent.ToString(CultureInfo.InvariantCulture);
        _halfHerePercent = settings.HalfHerePercent.ToString(CultureInfo.InvariantCulture);
        _minimumRatedSeconds = settings.MinimumRatedSeconds.ToString(CultureInfo.InvariantCulture);
        _historyRetentionDays = settings.HistoryRetentionDays.ToString(CultureInfo.InvariantCulture);
        _commentLanguage = settings.CommentLanguage;
        _use24HourClock = settings.Use24HourClock;
        _showSeconds = settings.ShowSeconds;

        ExcludedApps.Clear();
        foreach (var name in settings.ExcludedProcessNames)
        {
            ExcludedApps.Add(name);
        }

        // Every form field changed at once.
        OnPropertyChanged(string.Empty);
        OnFormChanged();
    }

    private void OnFormChanged()
    {
        _draft = TryBuildDraft(out _validationError);
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(HasValidationError));
        OnPropertyChanged(nameof(HasNoValidationError));
        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(HasNoExcludedApps));
        _saveCommand.RaiseCanExecuteChanged();
    }

    private AppSettings? TryBuildDraft(out string? error)
    {
        if (!TryParse(LockedInPercent, out var lockedIn) ||
            !TryParse(InTheZonePercent, out var inTheZone) ||
            !TryParse(HalfHerePercent, out var halfHere))
        {
            error = "Rating thresholds must be whole numbers.";
            return null;
        }

        if (!TryParse(MinimumRatedSeconds, out var minimumSeconds))
        {
            error = "Minimum session length must be a whole number of seconds.";
            return null;
        }

        if (!TryParse(HistoryRetentionDays, out var retentionDays))
        {
            error = "Days of history must be a whole number.";
            return null;
        }

        var draft = new AppSettings
        {
            LockedInPercent = lockedIn,
            InTheZonePercent = inTheZone,
            HalfHerePercent = halfHere,
            MinimumRatedSeconds = minimumSeconds,
            HistoryRetentionDays = retentionDays,
            ExcludedProcessNames = ExcludedApps.ToList(),
            CommentLanguage = CommentLanguage,
            Use24HourClock = Use24HourClock,
            ShowSeconds = ShowSeconds
        };

        error = draft.GetValidationError();
        return error is null ? draft : null;
    }

    private static bool TryParse(string text, out int value) =>
        int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private void Save()
    {
        if (_draft is null)
        {
            return;
        }

        var keptDays = _draft.HistoryRetentionDays;
        if (keptDays < _settingsService.Current.HistoryRetentionDays &&
            !_shellService.Confirm(
                "Keep less history?",
                $"Sessions older than {keptDays} {(keptDays == 1 ? "day" : "days")} will be deleted now. " +
                "This can't be undone.\n\nSave anyway?"))
        {
            return;
        }

        try
        {
            _settingsService.Save(_draft);
            LoadForm(_settingsService.Current);
            StatusMessage = "Settings saved.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StatusMessage = $"Couldn't save settings: {exception.Message}";
        }
    }

    private void RestoreDefaults()
    {
        LoadForm(new AppSettings(), isSaved: false);
        StatusMessage = HasChanges ? "Defaults restored. Select Save to apply them." : "Already using the defaults.";
    }

    private void AddExcludedApp()
    {
        var name = AppSettings.NormalizeProcessName(NewExcludedApp);
        NewExcludedApp = string.Empty;
        if (name.Length == 0)
        {
            return;
        }

        if (ExcludedApps.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            StatusMessage = $"{name} is already ignored.";
            return;
        }

        ExcludedApps.Add(name);
        RemoveRunningApp(name);
        StatusMessage = string.Empty;
        OnFormChanged();
    }

    private void RemoveExcludedApp(string? name)
    {
        if (name is null || !ExcludedApps.Remove(name))
        {
            return;
        }

        StatusMessage = string.Empty;
        OnFormChanged();
        RefreshRunningApps();
    }

    private void RefreshRunningApps()
    {
        var names = _windowTrackingService.OpenWindows
            .Select(window => _windowTrackingService.GetProcessName(window.Handle))
            .OfType<string>()
            .Where(name => !ExcludedApps.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

        RunningApps.Clear();
        foreach (var name in names)
        {
            RunningApps.Add(name);
        }
    }

    private void RemoveRunningApp(string name)
    {
        var match = RunningApps.FirstOrDefault(app => string.Equals(app, name, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            RunningApps.Remove(match);
        }
    }

    private void ClearHistory()
    {
        if (!_shellService.Confirm(
                "Clear focus history?",
                "Every saved session, switch, and visit will be deleted. " +
                "A session being recorded right now is kept.\n\nThis can't be undone. Clear history?"))
        {
            return;
        }

        try
        {
            // Save the open session first so it has an id and is kept.
            _focusSessionRecorder.Flush();
            var deleted = _repository.DeleteAllSessions(_focusSessionRecorder.CurrentSessionId);
            StatusMessage = deleted == 1 ? "Deleted 1 session." : $"Deleted {deleted} sessions.";
        }
        catch (Exception exception) when (exception is DbException or IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Couldn't clear history: {exception.Message}";
        }
    }

    private void OpenDataFolder()
    {
        try
        {
            _shellService.RevealInExplorer(_repository.DatabasePath);
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Couldn't open the data folder: {exception.Message}";
        }
    }
}
