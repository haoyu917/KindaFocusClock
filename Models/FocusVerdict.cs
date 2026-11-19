namespace MyClock.Models;

// Rating of a focus session, from its foreground rate.
public enum FocusVerdict
{
    // Too short to judge.
    WarmingUp,
    LockedIn,
    InTheZone,
    HalfHere,
    EscapeArtist
}
