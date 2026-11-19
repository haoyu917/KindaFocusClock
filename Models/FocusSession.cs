namespace MyClock.Models;

// One monitoring session for a single target window selection.
// A session starts when monitoring begins for a target and ends when the user
// selects another target, resets, or closes the app.
public sealed class FocusSession
{
    public long Id { get; set; }

    public required string WindowTitle { get; init; }

    // Executable name of the target (for example "devenv"), used to group sessions
    // whose window titles change over time. Null when it could not be read.
    public string? ProcessName { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    // Null while the session is still open, or if the app exited unexpectedly.
    public DateTimeOffset? EndedAt { get; set; }

    // Time spent monitoring, including time other windows were in the foreground.
    public TimeSpan MonitoredDuration { get; set; }

    // Time the target window was in the foreground while monitoring.
    public TimeSpan ForegroundDuration { get; set; }

    // Number of times focus left the target for another application window.
    // Sessions saved before switch details were recorded have a count but no Switches.
    public int SwitchCount { get; set; }

    // Details of each switch, in order.
    public List<FocusSwitch> Switches { get; } = [];

    // Share of the monitored time the target was in the foreground, from 0 to 1.
    public double ForegroundRate => MonitoredDuration > TimeSpan.Zero
        ? Math.Clamp(ForegroundDuration / MonitoredDuration, 0, 1)
        : 0;
}
