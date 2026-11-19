using System.Globalization;
using MyClock.Models;

namespace MyClock.Services;

// Turns saved focus sessions into verdicts, comments, tips, and distraction breakdowns.
// Thresholds and the comment language come from FocusSummaryOptions (the user's settings).
public static class FocusSummaryBuilder
{
    public const string UnknownApplicationName = "Unknown app";

    // Tip thresholds.
    private const int FrequentDistractionVisits = 3;
    private static readonly TimeSpan LongAverageAway = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MinimumDurationForSwitchRate = TimeSpan.FromMinutes(10);
    private const double HighSwitchesPerHour = 20;

    public static FocusSessionSummary Build(FocusSession session, FocusSummaryOptions? options = null)
    {
        options ??= FocusSummaryOptions.Default;
        var comments = FocusComments.For(options.Language);
        var verdict = GetVerdict(session, options);
        var switches = session.Switches;

        // Sessions saved before switch details existed have a count but no rows.
        var hasSwitchDetails = switches.Count > 0 || session.SwitchCount == 0;

        var averageAway = TimeSpan.Zero;
        var longestAway = TimeSpan.Zero;
        if (switches.Count > 0)
        {
            averageAway = Sum(switches.Select(focusSwitch => focusSwitch.AwayDuration)) / switches.Count;
            longestAway = switches.Max(focusSwitch => focusSwitch.AwayDuration);
        }

        var switchesPerHour = session.MonitoredDuration > TimeSpan.Zero
            ? session.SwitchCount / session.MonitoredDuration.TotalHours
            : 0;

        var distractions = BuildDistractions(switches);

        return new FocusSessionSummary
        {
            Session = session,
            Verdict = verdict,
            Headline = GetHeadline(verdict),
            // The message depends on the session id, so a session always gets the same comment.
            Message = Pick(GetSessionMessages(comments, verdict), session.Id),
            Tip = GetTip(comments, session, verdict, hasSwitchDetails, distractions, averageAway, switchesPerHour),
            HasSwitchDetails = hasSwitchDetails,
            SwitchesPerHour = switchesPerHour,
            AverageAwayDuration = averageAway,
            LongestAwayDuration = longestAway,
            Distractions = distractions
        };
    }

    // Totals for the sessions that started on the given local day.
    public static FocusDaySummary BuildDay(IEnumerable<FocusSession> sessions, DateTime day, FocusSummaryOptions? options = null)
    {
        options ??= FocusSummaryOptions.Default;
        var comments = FocusComments.For(options.Language);
        var daySessions = sessions
            .Where(session => session.StartedAt.ToLocalTime().Date == day.Date)
            .ToList();

        var monitored = Sum(daySessions.Select(session => session.MonitoredDuration));
        var foreground = Sum(daySessions.Select(session => session.ForegroundDuration));
        var rate = monitored > TimeSpan.Zero ? Math.Clamp(foreground / monitored, 0, 1) : 0;
        var verdict = GetVerdict(monitored, rate, options);
        var distractions = BuildDistractions(daySessions.SelectMany(session => session.Switches).ToList());

        return new FocusDaySummary
        {
            SessionCount = daySessions.Count,
            MonitoredDuration = monitored,
            ForegroundDuration = foreground,
            ForegroundRate = rate,
            SwitchCount = daySessions.Sum(session => session.SwitchCount),
            Verdict = verdict,
            Headline = daySessions.Count == 0 ? "NO DATA YET" : GetHeadline(verdict),
            // The day's comment changes from day to day but stays the same within a day.
            Message = daySessions.Count == 0
                ? comments.NoSessionsToday
                : Pick(GetDayMessages(comments, verdict), day.DayOfYear),
            TopDistraction = distractions.Count > 0 ? distractions[0] : null
        };
    }

    public static FocusVerdict GetVerdict(FocusSession session, FocusSummaryOptions? options = null) =>
        GetVerdict(session.MonitoredDuration, session.ForegroundRate, options ?? FocusSummaryOptions.Default);

    // "Tip: " or "建议：", shown before a tip.
    public static string GetTipLabel(CommentLanguage language) => FocusComments.For(language).TipLabel;

    // 45s, 4m 05s, 1h 02m
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes:00}m";
        }

        return duration.TotalMinutes >= 1
            ? $"{duration.Minutes}m {duration.Seconds:00}s"
            : $"{duration.Seconds}s";
    }

    // Today 10:32, Yesterday 18:05, 11-17 09:40
    public static string FormatStartTime(DateTimeOffset startedAt)
    {
        var local = startedAt.ToLocalTime();
        var time = local.ToString("HH:mm", CultureInfo.InvariantCulture);
        var days = (DateTime.Today - local.Date).Days;
        return days switch
        {
            0 => $"Today {time}",
            1 => $"Yesterday {time}",
            _ => local.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture)
        };
    }

    public static string FormatTimes(int count) => count == 1 ? "1 time" : $"{count} times";

    private static FocusVerdict GetVerdict(TimeSpan monitoredDuration, double foregroundRate, FocusSummaryOptions options)
    {
        if (monitoredDuration < options.MinimumRatedDuration || monitoredDuration <= TimeSpan.Zero)
        {
            return FocusVerdict.WarmingUp;
        }

        if (foregroundRate >= options.LockedInRate)
        {
            return FocusVerdict.LockedIn;
        }

        if (foregroundRate >= options.InTheZoneRate)
        {
            return FocusVerdict.InTheZone;
        }

        return foregroundRate >= options.HalfHereRate ? FocusVerdict.HalfHere : FocusVerdict.EscapeArtist;
    }

    private static string GetHeadline(FocusVerdict verdict) => verdict switch
    {
        FocusVerdict.LockedIn => "LOCKED IN",
        FocusVerdict.InTheZone => "IN THE ZONE",
        FocusVerdict.HalfHere => "HALF HERE",
        FocusVerdict.EscapeArtist => "ESCAPE ARTIST",
        _ => "WARMING UP"
    };

    private static string[] GetSessionMessages(FocusComments comments, FocusVerdict verdict) => verdict switch
    {
        FocusVerdict.LockedIn => comments.LockedIn,
        FocusVerdict.InTheZone => comments.InTheZone,
        FocusVerdict.HalfHere => comments.HalfHere,
        FocusVerdict.EscapeArtist => comments.EscapeArtist,
        _ => comments.WarmingUp
    };

    private static string[] GetDayMessages(FocusComments comments, FocusVerdict verdict) => verdict switch
    {
        FocusVerdict.LockedIn => comments.DayLockedIn,
        FocusVerdict.InTheZone => comments.DayInTheZone,
        FocusVerdict.HalfHere => comments.DayHalfHere,
        FocusVerdict.EscapeArtist => comments.DayEscapeArtist,
        _ => comments.DayWarmingUp
    };

    private static string Pick(string[] messages, long seed) => messages[(int)(Math.Abs(seed) % messages.Length)];

    private static string GetTip(
        FocusComments comments,
        FocusSession session,
        FocusVerdict verdict,
        bool hasSwitchDetails,
        IReadOnlyList<FocusDistraction> distractions,
        TimeSpan averageAway,
        double switchesPerHour)
    {
        if (verdict == FocusVerdict.WarmingUp)
        {
            return comments.WarmingUpTip;
        }

        if (session.SwitchCount == 0)
        {
            return comments.ZeroSwitchesTip;
        }

        if (!hasSwitchDetails)
        {
            return comments.NoDetailsTip;
        }

        var topDistraction = distractions.Count > 0 ? distractions[0] : null;
        if (topDistraction is not null && topDistraction.VisitCount >= FrequentDistractionVisits)
        {
            return comments.FrequentDistractionTip(topDistraction.Name, topDistraction.VisitCount, topDistraction.TimeSpent);
        }

        if (averageAway >= LongAverageAway)
        {
            return comments.LongAverageAwayTip(averageAway);
        }

        if (session.MonitoredDuration >= MinimumDurationForSwitchRate && switchesPerHour >= HighSwitchesPerHour)
        {
            return comments.HighSwitchRateTip(switchesPerHour);
        }

        return comments.CalmTip;
    }

    private static IReadOnlyList<FocusDistraction> BuildDistractions(IReadOnlyList<FocusSwitch> switches)
    {
        var visits = switches.SelectMany(focusSwitch => focusSwitch.Visits).ToList();
        var totalVisitTime = Sum(visits.Select(visit => visit.Duration));

        return visits
            .GroupBy(
                visit => string.IsNullOrEmpty(visit.ProcessName) ? UnknownApplicationName : visit.ProcessName,
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var timeSpent = Sum(group.Select(visit => visit.Duration));
                return new FocusDistraction
                {
                    Name = group.Key,
                    TopWindowTitle = group
                        .Where(visit => visit.WindowTitle.Length > 0)
                        .GroupBy(visit => visit.WindowTitle)
                        .OrderByDescending(titleGroup => Sum(titleGroup.Select(visit => visit.Duration)))
                        .Select(titleGroup => titleGroup.Key)
                        .FirstOrDefault(),
                    VisitCount = group.Count(),
                    TimeSpent = timeSpent,
                    ShareOfAwayTime = totalVisitTime > TimeSpan.Zero ? timeSpent / totalVisitTime : 0
                };
            })
            .OrderByDescending(distraction => distraction.VisitCount)
            .ThenByDescending(distraction => distraction.TimeSpent)
            .ToList();
    }

    private static TimeSpan Sum(IEnumerable<TimeSpan> durations) =>
        durations.Aggregate(TimeSpan.Zero, (total, duration) => total + duration);
}
