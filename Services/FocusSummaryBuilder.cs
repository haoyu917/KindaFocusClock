using System.Globalization;
using MyClock.Models;

namespace MyClock.Services;

// Turns a saved focus session into a verdict, a comment, a tip, and a distraction breakdown.
public static class FocusSummaryBuilder
{
    public const string UnknownApplicationName = "Unknown app";

    // Sessions shorter than this are too short to rate.
    public static readonly TimeSpan MinimumRatedDuration = TimeSpan.FromMinutes(1);

    // Foreground rate thresholds for each verdict.
    private const double LockedInRate = 0.85;
    private const double InTheZoneRate = 0.65;
    private const double HalfHereRate = 0.40;

    // Tip thresholds.
    private const int FrequentDistractionVisits = 3;
    private static readonly TimeSpan LongAverageAway = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MinimumDurationForSwitchRate = TimeSpan.FromMinutes(10);
    private const double HighSwitchesPerHour = 20;

    private static readonly string[] WarmingUpMessages =
    [
        "Blink and it's over. Run a longer session for a real verdict."
    ];

    private static readonly string[] LockedInMessages =
    [
        "Laser focus. Your other apps are filing a missing-person report.",
        "You and this window were basically inseparable. Very wholesome.",
        "Distractions knocked. Nobody answered."
    ];

    private static readonly string[] InTheZoneMessages =
    [
        "Solid focus with a few sightseeing trips. Respectable.",
        "Mostly on task. Your attention only stepped out for snacks.",
        "Good rhythm. A couple of detours, nothing dramatic."
    ];

    private static readonly string[] HalfHereMessages =
    [
        "Your attention was on a part-time contract this session.",
        "Half here, half somewhere more exciting, apparently.",
        "A coin flip between working and wandering. The coin was busy."
    ];

    private static readonly string[] EscapeArtistMessages =
    [
        "Your focus went for a walk and took the scenic route.",
        "The target window feels a little ignored. Maybe say hi next time?",
        "Houdini would be proud. The work, less so."
    ];

    // Whole-day comments, kept short enough for one line on the summary page.
    private const string NoSessionsTodayMessage = "Nothing tracked yet today. The day is young.";

    private static readonly string[] DayWarmingUpMessages =
    [
        "Barely warmed up. The real work is still loading."
    ];

    private static readonly string[] DayLockedInMessages =
    [
        "Focus machine mode. Productivity wants your autograph.",
        "Distractions called all day. Straight to voicemail."
    ];

    private static readonly string[] DayInTheZoneMessages =
    [
        "A good day's work with a few scenic detours.",
        "Your focus took coffee breaks, not vacations."
    ];

    private static readonly string[] DayHalfHereMessages =
    [
        "A group project between you and your distractions.",
        "Half the day was yours. The other half had plans."
    ];

    private static readonly string[] DayEscapeArtistMessages =
    [
        "Your attention went on tour. Book it a return ticket.",
        "Focus was mostly a rumor today. Tomorrow is a fresh start."
    ];

    public static FocusSessionSummary Build(FocusSession session)
    {
        var verdict = GetVerdict(session);
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
            Message = PickMessage(verdict, session.Id),
            Tip = GetTip(session, verdict, hasSwitchDetails, distractions, averageAway, switchesPerHour),
            HasSwitchDetails = hasSwitchDetails,
            SwitchesPerHour = switchesPerHour,
            AverageAwayDuration = averageAway,
            LongestAwayDuration = longestAway,
            Distractions = distractions
        };
    }

    // Totals for the sessions that started on the given local day.
    public static FocusDaySummary BuildDay(IEnumerable<FocusSession> sessions, DateTime day)
    {
        var daySessions = sessions
            .Where(session => session.StartedAt.ToLocalTime().Date == day.Date)
            .ToList();

        var monitored = Sum(daySessions.Select(session => session.MonitoredDuration));
        var foreground = Sum(daySessions.Select(session => session.ForegroundDuration));
        var rate = monitored > TimeSpan.Zero ? Math.Clamp(foreground / monitored, 0, 1) : 0;
        var verdict = GetVerdict(monitored, rate);
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
            Message = daySessions.Count == 0 ? NoSessionsTodayMessage : PickDayMessage(verdict, day),
            TopDistraction = distractions.Count > 0 ? distractions[0] : null
        };
    }

    public static FocusVerdict GetVerdict(FocusSession session) =>
        GetVerdict(session.MonitoredDuration, session.ForegroundRate);

    private static FocusVerdict GetVerdict(TimeSpan monitoredDuration, double foregroundRate)
    {
        if (monitoredDuration < MinimumRatedDuration)
        {
            return FocusVerdict.WarmingUp;
        }

        return foregroundRate switch
        {
            >= LockedInRate => FocusVerdict.LockedIn,
            >= InTheZoneRate => FocusVerdict.InTheZone,
            >= HalfHereRate => FocusVerdict.HalfHere,
            _ => FocusVerdict.EscapeArtist
        };
    }

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

    private static string GetHeadline(FocusVerdict verdict) => verdict switch
    {
        FocusVerdict.LockedIn => "LOCKED IN",
        FocusVerdict.InTheZone => "IN THE ZONE",
        FocusVerdict.HalfHere => "HALF HERE",
        FocusVerdict.EscapeArtist => "ESCAPE ARTIST",
        _ => "WARMING UP"
    };

    // The message depends on the session id, so a session always gets the same comment.
    private static string PickMessage(FocusVerdict verdict, long sessionId)
    {
        var messages = verdict switch
        {
            FocusVerdict.LockedIn => LockedInMessages,
            FocusVerdict.InTheZone => InTheZoneMessages,
            FocusVerdict.HalfHere => HalfHereMessages,
            FocusVerdict.EscapeArtist => EscapeArtistMessages,
            _ => WarmingUpMessages
        };

        return messages[(int)(Math.Abs(sessionId) % messages.Length)];
    }

    // The day's comment changes from day to day but stays the same within a day.
    private static string PickDayMessage(FocusVerdict verdict, DateTime day)
    {
        var messages = verdict switch
        {
            FocusVerdict.LockedIn => DayLockedInMessages,
            FocusVerdict.InTheZone => DayInTheZoneMessages,
            FocusVerdict.HalfHere => DayHalfHereMessages,
            FocusVerdict.EscapeArtist => DayEscapeArtistMessages,
            _ => DayWarmingUpMessages
        };

        return messages[day.DayOfYear % messages.Length];
    }

    private static string GetTip(
        FocusSession session,
        FocusVerdict verdict,
        bool hasSwitchDetails,
        IReadOnlyList<FocusDistraction> distractions,
        TimeSpan averageAway,
        double switchesPerHour)
    {
        if (verdict == FocusVerdict.WarmingUp)
        {
            return "Sessions longer than a minute get the full report.";
        }

        if (session.SwitchCount == 0)
        {
            return "Zero switches. Not a single detour. Frame this one.";
        }

        if (!hasSwitchDetails)
        {
            return "This session was recorded before detour details existed, so there's no breakdown.";
        }

        var topDistraction = distractions.Count > 0 ? distractions[0] : null;
        if (topDistraction is not null && topDistraction.VisitCount >= FrequentDistractionVisits)
        {
            return $"{topDistraction.Name} pulled you away {FormatTimes(topDistraction.VisitCount)}. " +
                   $"Muting it next session could win back {FormatDuration(topDistraction.TimeSpent)}.";
        }

        if (averageAway >= LongAverageAway)
        {
            return $"Your average detour lasted {FormatDuration(averageAway)}. " +
                   "Quick breaks are fine; long ones snowball.";
        }

        if (session.MonitoredDuration >= MinimumDurationForSwitchRate && switchesPerHour >= HighSwitchesPerHour)
        {
            return $"{switchesPerHour:0} switches an hour. Try batching messages into planned breaks.";
        }

        return "Detours were short and rare. Keep that energy.";
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
