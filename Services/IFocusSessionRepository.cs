using MyClock.Models;

namespace MyClock.Services;

public interface IFocusSessionRepository
{
    // Inserts or updates the session and the given switches with all their visits,
    // in one transaction. Rows with Id 0 are inserted and receive their ids only
    // after the transaction commits, so a failed save can simply be retried.
    void Save(FocusSession session, IEnumerable<FocusSwitch> changedSwitches);

    // Returns sessions with from <= StartedAt < to, oldest first, including their
    // switches and visits.
    IReadOnlyList<FocusSession> GetSessionsStartedBetween(DateTimeOffset from, DateTimeOffset to);
}
