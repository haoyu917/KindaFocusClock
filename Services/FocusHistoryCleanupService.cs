using System.Data.Common;
using System.Diagnostics;
using System.IO;

namespace MyClock.Services;

public sealed class FocusHistoryCleanupService : IFocusHistoryCleanupService
{
    private readonly IFocusSessionRepository _repository;
    private readonly IClockService _clockService;
    private readonly ISettingsService _settingsService;
    private readonly IFocusSessionRecorder _focusSessionRecorder;
    private DateTime _lastCleanupDay;
    private bool _isStarted;
    private bool _disposed;

    public FocusHistoryCleanupService(
        IFocusSessionRepository repository,
        IClockService clockService,
        ISettingsService settingsService,
        IFocusSessionRecorder focusSessionRecorder)
    {
        _repository = repository;
        _clockService = clockService;
        _settingsService = settingsService;
        _focusSessionRecorder = focusSessionRecorder;
    }

    // Local midnight at the start of the oldest kept day: today plus retentionDays - 1 earlier days.
    public static DateTimeOffset GetRetentionStart(DateTime today, int retentionDays) =>
        new(today.Date.AddDays(-(Math.Max(retentionDays, 1) - 1)));

    public void Start()
    {
        if (_isStarted || _disposed)
        {
            return;
        }

        _isStarted = true;
        CleanUp();
        _clockService.TimeChanged += OnTimeChanged;
        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    private void OnTimeChanged(object? sender, EventArgs e)
    {
        if (_clockService.CurrentTime.Date != _lastCleanupDay)
        {
            CleanUp();
        }
    }

    // The number of days kept may have gone down.
    private void OnSettingsChanged(object? sender, EventArgs e) => CleanUp();

    private void CleanUp()
    {
        var today = _clockService.CurrentTime.Date;
        var retentionDays = _settingsService.Current.HistoryRetentionDays;

        // Recorded before trying, so a failure is retried on the next day or the
        // next start instead of every clock tick.
        _lastCleanupDay = today;

        try
        {
            // The session being recorded is kept even if it started before the
            // cutoff, for example a session running past midnight.
            var deleted = _repository.DeleteSessionsStartedBefore(
                GetRetentionStart(today, retentionDays),
                _focusSessionRecorder.CurrentSessionId);
            if (deleted > 0)
            {
                Debug.WriteLine($"Deleted {deleted} focus sessions older than {retentionDays} days.");
            }
        }
        catch (Exception exception) when (exception is DbException or IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Failed to clean up focus history: {exception.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_isStarted)
        {
            _clockService.TimeChanged -= OnTimeChanged;
            _settingsService.SettingsChanged -= OnSettingsChanged;
        }
    }
}
