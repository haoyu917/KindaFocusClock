using MyClock.Models;

namespace MyClock.Services;

public interface IFocusSessionRepository
{
    // Full path of the database file.
    string DatabasePath { get; }

    // Inserts or updates the session and the given switches with all their visits,
    // in one transaction. Rows with Id 0 are inserted and receive their ids only
    // after the transaction commits, so a failed save can simply be retried.
    void Save(FocusSession session, IEnumerable<FocusSwitch> changedSwitches);

    // Returns sessions with from <= StartedAt < to, oldest first, including their
    // switches and visits.
    IReadOnlyList<FocusSession> GetSessionsStartedBetween(DateTimeOffset from, DateTimeOffset to);

    // Deletes sessions with StartedAt < cutoff, with their switches and visits,
    // except the session with exceptSessionId (the one being recorded).
    // Returns the number of sessions deleted.
    int DeleteSessionsStartedBefore(DateTimeOffset cutoff, long? exceptSessionId = null);

    // Deletes every session except the one with exceptSessionId, then compacts the file.
    // Returns the number of sessions deleted.
    int DeleteAllSessions(long? exceptSessionId = null);
}
