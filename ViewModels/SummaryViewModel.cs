using System.Collections.ObjectModel;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using MyClock.Models;
using MyClock.Services;

namespace MyClock.ViewModels;

public sealed class SummaryViewModel : ViewModelBase
{
    // Sessions shorter than this are accidental starts and are not listed.
    private static readonly TimeSpan MinimumListedDuration = TimeSpan.FromSeconds(1);

    private const int MaxDistractionRows = 3;

    private const string NoSessionsMessage =
        "No sessions yet. Start monitoring a window and come back for your verdict.";

    private readonly IFocusSessionRepository _repository;
    private readonly IFocusSessionRecorder _focusSessionRecorder;
    private readonly ISettingsService _settingsService;

    // Rating thresholds and comment language, read from settings each time the page loads.
    private FocusSummaryOptions _options = FocusSummaryOptions.Default;
    private SummarySessionItem? _selectedSession;
    private FocusSessionSummary? _summary;
    private IReadOnlyList<SummaryDistractionItem> _distractions = [];
    private TodaySummaryItem _today = new(FocusSummaryBuilder.BuildDay([], DateTime.Today));
    private string _emptyMessage = NoSessionsMessage;

    public SummaryViewModel(
        IFocusSessionRepository repository,
        IFocusSessionRecorder focusSessionRecorder,
        ISettingsService settingsService)
    {
        _repository = repository;
        _focusSessionRecorder = focusSessionRecorder;
        _settingsService = settingsService;
    }

    // Recent sessions, newest first.
    public ObservableCollection<SummarySessionItem> Sessions { get; } = [];

    public SummarySessionItem? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (!SetProperty(ref _selectedSession, value))
            {
                return;
            }

            _summary = value is null ? null : FocusSummaryBuilder.Build(value.Session, _options);
            _distractions = _summary is null
                ? []
                : _summary.Distractions.Take(MaxDistractionRows).Select(d => new SummaryDistractionItem(d)).ToList();

            // Every displayed value derives from the selected session.
            OnPropertyChanged(string.Empty);
        }
    }

    // Totals for sessions that started today.
    public TodaySummaryItem Today => _today;

    public bool HasSessions => Sessions.Count > 0;

    public bool HasNoSessions => !HasSessions;

    public string EmptyMessage => _emptyMessage;

    public FocusVerdict Verdict => _summary?.Verdict ?? FocusVerdict.WarmingUp;

    public string Headline => _summary?.Headline ?? string.Empty;

    public string Message => _summary?.Message ?? string.Empty;

    public string Tip => _summary is null
        ? string.Empty
        : FocusSummaryBuilder.GetTipLabel(_options.Language) + _summary.Tip;

    // The reviewed session's target window, shown under the page title.
    public string HeaderSubtitle => _summary is null
        ? "How each monitoring session went"
        : $"Target: {_summary.Session.WindowTitle}";

    public string RateText => _summary is null || _summary.Session.MonitoredDuration <= TimeSpan.Zero
        ? "--"
        : $"{_summary.Session.ForegroundRate * 100:0.0}%";

    public string FocusTimeText => _summary is null
        ? "--"
        : FocusSummaryBuilder.FormatDuration(_summary.Session.ForegroundDuration);

    public string FocusTimeDetail => _summary is null
        ? string.Empty
        : $"of {FocusSummaryBuilder.FormatDuration(_summary.Session.MonitoredDuration)} monitored";

    public string SwitchCountText => _summary?.Session.SwitchCount.ToString() ?? "--";

    public string SwitchRateText => _summary is null || _summary.Verdict == FocusVerdict.WarmingUp
        ? "Too short to rate"
        : $"{_summary.SwitchesPerHour:0.#} per hour";

    public string AverageAwayText => _summary is { Session.Switches.Count: > 0 }
        ? FocusSummaryBuilder.FormatDuration(_summary.AverageAwayDuration)
        : "--";

    public string LongestAwayText
    {
        get
        {
            if (_summary is null)
            {
                return string.Empty;
            }

            if (_summary.Session.Switches.Count > 0)
            {
                return $"Longest {FocusSummaryBuilder.FormatDuration(_summary.LongestAwayDuration)}";
            }

            return _summary.HasSwitchDetails ? "No detours" : "No details recorded";
        }
    }

    // "--" when switches happened but their details were not recorded.
    public string TopDistractionName => _summary?.TopDistraction?.Name ??
                                        (_summary is { HasSwitchDetails: false } ? "--" : "None");

    public string TopDistractionDetail
    {
        get
        {
            if (_summary?.TopDistraction is { } top)
            {
                return $"{FocusSummaryBuilder.FormatTimes(top.VisitCount)} · {FocusSummaryBuilder.FormatDuration(top.TimeSpent)}";
            }

            return _summary is { HasSwitchDetails: false } ? "No details recorded" : "Nothing pulled you away";
        }
    }

    public IReadOnlyList<SummaryDistractionItem> Distractions => _distractions;

    public bool HasDistractions => _distractions.Count > 0;

    public bool HasNoDistractions => !HasDistractions;

    public string NoDistractionsMessage => _summary is { HasSwitchDetails: false }
        ? "This session was recorded before detour details were saved."
        : "No detours this session. Nothing to see here.";

    // Called each time the summary page is shown, so it includes the latest data.
    public void OnPageLoaded()
    {
        _focusSessionRecorder.Flush();
        _options = FocusSummaryOptions.FromSettings(_settingsService.Current);
        LoadSessions();
    }

    private void LoadSessions()
    {
        var selectedId = SelectedSession?.Session.Id;

        IReadOnlyList<FocusSession> sessions;
        try
        {
            // Only the days kept by the history cleanup are listed.
            var today = DateTime.Today;
            sessions = _repository.GetSessionsStartedBetween(
                FocusHistoryCleanupService.GetRetentionStart(today, _settingsService.Current.HistoryRetentionDays),
                new DateTimeOffset(today.AddDays(1)));
            _emptyMessage = NoSessionsMessage;
        }
        catch (Exception exception) when (exception is DbException or IOException or UnauthorizedAccessException)
        {
            sessions = [];
            _emptyMessage = "Couldn't read your focus history. Try again in a moment.";
            Debug.WriteLine($"Failed to load focus sessions: {exception.Message}");
        }

        var listedSessions = sessions
            .Where(session => session.MonitoredDuration >= MinimumListedDuration)
            .OrderByDescending(session => session.StartedAt)
            .ThenByDescending(session => session.Id)
            .ToList();

        _today = new TodaySummaryItem(FocusSummaryBuilder.BuildDay(listedSessions, DateTime.Today, _options));

        Sessions.Clear();
        foreach (var session in listedSessions)
        {
            Sessions.Add(new SummarySessionItem(session));
        }

        // Keep showing the same session after a reload, with its refreshed data.
        var selection = Sessions.FirstOrDefault(item => item.Session.Id == selectedId) ?? Sessions.FirstOrDefault();
        if (ReferenceEquals(selection, SelectedSession))
        {
            OnPropertyChanged(string.Empty);
        }
        else
        {
            SelectedSession = selection;
        }

        // SelectedSession may not change (for example when both are null).
        OnPropertyChanged(nameof(HasSessions));
        OnPropertyChanged(nameof(HasNoSessions));
        OnPropertyChanged(nameof(EmptyMessage));
        OnPropertyChanged(nameof(Today));
    }
}
