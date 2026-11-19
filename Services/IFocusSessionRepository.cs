using MyClock.Models;

namespace MyClock.Services;

public interface IFocusSessionRepository
{
    // Saves a new session and returns its id.
    long Add(FocusSession session);

    void Update(FocusSession session);

    // Returns sessions with from <= StartedAt < to, oldest first.
    IReadOnlyList<FocusSession> GetSessionsStartedBetween(DateTimeOffset from, DateTimeOffset to);
}
