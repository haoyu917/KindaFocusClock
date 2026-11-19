using System.Data.Common;
using System.Diagnostics;
using System.IO;

namespace MyClock.Services;

public sealed class FocusHistoryCleanupService : IFocusHistoryCleanupService
{
    // Today plus the previous six days are kept.
    public const int RetentionDays = 7;

    private readonly IFocusSessionRepository _repository;
    private readonly IClockService _clockService;
    private DateTime _lastCleanupDay;
    private bool _isStarted;
    private bool _disposed;

    public FocusHistoryCleanupService(IFocusSessionRepository repository, IClockService clockService)
    {
        _repository = repository;
        _clockService = clockService;
    }

    // Local midnight at the start of the oldest kept day.
    public static DateTimeOffset GetRetentionStart(DateTime today) =>
        new(today.Date.AddDays(-(RetentionDays - 1)));

    public void Start()
    {
        if (_isStarted || _disposed)
        {
            return;
        }

        _isStarted = true;
        CleanUp();
        _clockService.TimeChanged += OnTimeChanged;
    }

    private void OnTimeChanged(object? sender, EventArgs e)
    {
        if (_clockService.CurrentTime.Date != _lastCleanupDay)
        {
            CleanUp();
        }
    }

    private void CleanUp()
    {
        var today = _clockService.CurrentTime.Date;

        // Recorded before trying, so a failure is retried on the next day or the
        // next start instead of every clock tick.
        _lastCleanupDay = today;

        try
        {
            var deleted = _repository.DeleteSessionsStartedBefore(GetRetentionStart(today));
            if (deleted > 0)
            {
                Debug.WriteLine($"Deleted {deleted} focus sessions older than {RetentionDays} days.");
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
        }
    }
}
