namespace MyClock.Models;

// One switch away from the target: from the moment focus reached another
// application window until it came back to the target.
public sealed class FocusSwitch
{
    public long Id { get; set; }

    public DateTimeOffset LeftAt { get; init; }

    // Null when the session ended before focus returned to the target.
    public DateTimeOffset? ReturnedAt { get; set; }

    // Monitored time away from the target. Time while monitoring was paused
    // is excluded, so this can be shorter than ReturnedAt - LeftAt.
    public TimeSpan AwayDuration { get; set; }

    // Windows visited while away, in order. Time in MyClock itself or with no
    // foreground window is part of AwayDuration but not of any visit.
    public List<FocusSwitchVisit> Visits { get; } = [];
}
