namespace MyClock.Models;

// Totals for all sessions that started on one day.
public sealed class FocusDaySummary
{
    public int SessionCount { get; init; }

    public TimeSpan MonitoredDuration { get; init; }

    public TimeSpan ForegroundDuration { get; init; }

    // Foreground time over monitored time across the day's sessions, from 0 to 1.
    public double ForegroundRate { get; init; }

    public int SwitchCount { get; init; }

    public FocusVerdict Verdict { get; init; }

    public required string Headline { get; init; }

    public required string Message { get; init; }

    // The application visited most while away during the day, if any switch details exist.
    public FocusDistraction? TopDistraction { get; init; }
}
