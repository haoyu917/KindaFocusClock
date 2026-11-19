namespace MyClock.Models;

// One application visited while away from the target during a session.
public sealed class FocusDistraction
{
    // Process name, or "Unknown app" when it could not be read.
    public required string Name { get; init; }

    // The window title of this application that took the most time, if any had a title.
    public string? TopWindowTitle { get; init; }

    // Number of times focus moved to this application while away from the target.
    public int VisitCount { get; init; }

    public TimeSpan TimeSpent { get; init; }

    // Share of all time attributed to visited applications in the session, from 0 to 1.
    public double ShareOfAwayTime { get; init; }
}
