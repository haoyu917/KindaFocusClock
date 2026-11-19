namespace MyClock.Models;

public sealed class FocusSessionSummary
{
    public required FocusSession Session { get; init; }

    public FocusVerdict Verdict { get; init; }

    // Short verdict label, such as "LOCKED IN".
    public required string Headline { get; init; }

    // A light-hearted comment on the session.
    public required string Message { get; init; }

    // A suggestion based on the session's data.
    public required string Tip { get; init; }

    // False for sessions recorded before switch details were saved.
    public bool HasSwitchDetails { get; init; }

    public double SwitchesPerHour { get; init; }

    public TimeSpan AverageAwayDuration { get; init; }

    public TimeSpan LongestAwayDuration { get; init; }

    // Applications visited while away, most visited first.
    public required IReadOnlyList<FocusDistraction> Distractions { get; init; }

    public FocusDistraction? TopDistraction => Distractions.Count > 0 ? Distractions[0] : null;
}
