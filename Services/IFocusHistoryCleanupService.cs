namespace MyClock.Services;

// Keeps the focus history to the most recent days.
public interface IFocusHistoryCleanupService : IDisposable
{
    // Deletes old sessions now, then again each time a new day begins.
    void Start();
}
