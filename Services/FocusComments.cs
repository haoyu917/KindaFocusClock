using MyClock.Models;

namespace MyClock.Services;

// The wording of focus summary comments and tips, one instance per language.
// FocusSummaryBuilder decides which comment or tip applies; this class only words it.
internal sealed class FocusComments
{
    public static FocusComments For(CommentLanguage language) =>
        language == CommentLanguage.Chinese ? Chinese : English;

    // Comments on a single session, by verdict.
    public required string[] WarmingUp { get; init; }
    public required string[] LockedIn { get; init; }
    public required string[] InTheZone { get; init; }
    public required string[] HalfHere { get; init; }
    public required string[] EscapeArtist { get; init; }

    // Comments on a whole day, by verdict. Kept short enough for one line.
    public required string NoSessionsToday { get; init; }
    public required string[] DayWarmingUp { get; init; }
    public required string[] DayLockedIn { get; init; }
    public required string[] DayInTheZone { get; init; }
    public required string[] DayHalfHere { get; init; }
    public required string[] DayEscapeArtist { get; init; }

    // Tips. Placeholders are filled by the functions' arguments.
    public required string TipLabel { get; init; }
    public required string WarmingUpTip { get; init; }
    public required string ZeroSwitchesTip { get; init; }
    public required string NoDetailsTip { get; init; }
    public required string CalmTip { get; init; }

    // (application, visit count, time spent there)
    public required Func<string, int, TimeSpan, string> FrequentDistractionTip { get; init; }

    // (average time away per switch)
    public required Func<TimeSpan, string> LongAverageAwayTip { get; init; }

    // (switches per hour)
    public required Func<double, string> HighSwitchRateTip { get; init; }

    public static readonly FocusComments English = new()
    {
        WarmingUp = ["Blink and it's over. Run a longer session for a real verdict."],
        LockedIn =
        [
            "Laser focus. Your other apps are filing a missing-person report.",
            "You and this window were basically inseparable. Very wholesome.",
            "Distractions knocked. Nobody answered."
        ],
        InTheZone =
        [
            "Solid focus with a few sightseeing trips. Respectable.",
            "Mostly on task. Your attention only stepped out for snacks.",
            "Good rhythm. A couple of detours, nothing dramatic."
        ],
        HalfHere =
        [
            "Your attention was on a part-time contract this session.",
            "Half here, half somewhere more exciting, apparently.",
            "A coin flip between working and wandering. The coin was busy."
        ],
        EscapeArtist =
        [
            "Your focus went for a walk and took the scenic route.",
            "The target window feels a little ignored. Maybe say hi next time?",
            "Houdini would be proud. The work, less so."
        ],
        NoSessionsToday = "Nothing tracked yet today. The day is young.",
        DayWarmingUp = ["Barely warmed up. The real work is still loading."],
        DayLockedIn =
        [
            "Focus machine mode. Productivity wants your autograph.",
            "Distractions called all day. Straight to voicemail."
        ],
        DayInTheZone =
        [
            "A good day's work with a few scenic detours.",
            "Your focus took coffee breaks, not vacations."
        ],
        DayHalfHere =
        [
            "A group project between you and your distractions.",
            "Half the day was yours. The other half had plans."
        ],
        DayEscapeArtist =
        [
            "Your attention went on tour. Book it a return ticket.",
            "Focus was mostly a rumor today. Tomorrow is a fresh start."
        ],
        TipLabel = "Tip: ",
        WarmingUpTip = "Sessions longer than a minute get the full report.",
        ZeroSwitchesTip = "Zero switches. Not a single detour. Frame this one.",
        NoDetailsTip = "This session was recorded before detour details existed, so there's no breakdown.",
        CalmTip = "Detours were short and rare. Keep that energy.",
        FrequentDistractionTip = (application, count, time) =>
            $"{application} pulled you away {FocusSummaryBuilder.FormatTimes(count)}. " +
            $"Muting it next session could win back {FocusSummaryBuilder.FormatDuration(time)}.",
        LongAverageAwayTip = average =>
            $"Your average detour lasted {FocusSummaryBuilder.FormatDuration(average)}. " +
            "Quick breaks are fine; long ones snowball.",
        HighSwitchRateTip = perHour =>
            $"{perHour:0} switches an hour. Try batching messages into planned breaks."
    };

    public static readonly FocusComments Chinese = new()
    {
        WarmingUp = ["眨眼就结束了。来一段长一点的，再给你正式打分。"],
        LockedIn =
        [
            "专注力拉满，其他软件已经在报警找你了。",
            "你和这个窗口形影不离，感人。",
            "干扰敲了敲门，可惜没人在家。"
        ],
        InTheZone =
        [
            "整体很稳，中途出门看了几眼风景。",
            "大部分时间都在线，注意力只是溜出去买了个零食。",
            "节奏不错，绕了几个小弯，问题不大。"
        ],
        HalfHere =
        [
            "这次的注意力是在打零工。",
            "一半在这儿，另一半去了更好玩的地方。",
            "工作和摸鱼五五开，硬币都转累了。"
        ],
        EscapeArtist =
        [
            "注意力出门散步，还特意挑了最远的那条路。",
            "目标窗口觉得自己被冷落了，下次记得打个招呼。",
            "逃脱大师本师，工作进度表示很无奈。"
        ],
        NoSessionsToday = "今天还没开始记录，时间还早。",
        DayWarmingUp = ["刚热完身，正片还在加载。"],
        DayLockedIn =
        [
            "专注机器模式，效率都想找你签名。",
            "干扰打了一天电话，全部转进了语音信箱。"
        ],
        DayInTheZone =
        [
            "扎实的一天，中间顺路看了几处风景。",
            "注意力只是去喝了几杯咖啡，没去度假。"
        ],
        DayHalfHere =
        [
            "今天是你和干扰的小组作业。",
            "一半时间属于你，另一半另有安排。"
        ],
        DayEscapeArtist =
        [
            "注意力今天出去巡演了，记得给它买张回程票。",
            "今天的专注基本只存在于传说中，明天重新开始。"
        ],
        TipLabel = "建议：",
        WarmingUpTip = "超过一分钟的会话才有完整报告。",
        ZeroSwitchesTip = "零切换，一次都没跑神，建议裱起来。",
        NoDetailsTip = "这个会话录制时还没有切换明细，所以没有细分数据。",
        CalmTip = "离开得少，回来得快，保持住。",
        FrequentDistractionTip = (application, count, time) =>
            $"{application} 把你拉走了 {count} 次。下次先把它静音，能多出 {FormatChineseDuration(time)}。",
        LongAverageAwayTip = average =>
            $"平均每次离开 {FormatChineseDuration(average)}。短暂休息没问题，就怕越歇越久。",
        HighSwitchRateTip = perHour =>
            $"每小时切换 {perHour:0} 次。试试把消息集中到固定的休息时间处理。"
    };

    // 45 秒, 4 分 05 秒, 1 小时 02 分
    private static string FormatChineseDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours} 小时 {duration.Minutes:00} 分";
        }

        return duration.TotalMinutes >= 1
            ? $"{duration.Minutes} 分 {duration.Seconds:00} 秒"
            : $"{duration.Seconds} 秒";
    }
}
