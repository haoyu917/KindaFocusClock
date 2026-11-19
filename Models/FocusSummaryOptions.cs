namespace MyClock.Models;

// Settings that affect how sessions and days are rated and described.
public sealed record FocusSummaryOptions(
    double LockedInRate,
    double InTheZoneRate,
    double HalfHereRate,
    TimeSpan MinimumRatedDuration,
    CommentLanguage Language)
{
    public static FocusSummaryOptions Default { get; } = FromSettings(new AppSettings());

    public static FocusSummaryOptions FromSettings(AppSettings settings) => new(
        settings.LockedInPercent / 100.0,
        settings.InTheZonePercent / 100.0,
        settings.HalfHerePercent / 100.0,
        TimeSpan.FromSeconds(settings.MinimumRatedSeconds),
        settings.CommentLanguage);
}
