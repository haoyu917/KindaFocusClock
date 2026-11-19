namespace MyClock.Models;

// A continuous period with one non-target window in the foreground during a switch.
public sealed class FocusSwitchVisit
{
    public long Id { get; set; }

    // Title when the visit started; empty for untitled windows such as the taskbar.
    public required string WindowTitle { get; init; }

    public string? ProcessName { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    // Monitored time in this window; excludes time while monitoring was paused.
    public TimeSpan Duration { get; set; }
}
