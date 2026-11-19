namespace MyClock.Models;

// User preferences, saved as JSON. Instances are not changed after creation;
// SettingsService replaces the whole object on save.
public sealed record AppSettings
{
    public const int MinRetentionDays = 1;
    public const int MaxRetentionDays = 365;
    public const int MaxMinimumRatedSeconds = 3600;

    // Lowest foreground rate, in percent, for each verdict. Must be strictly
    // descending and between 1 and 100.
    public int LockedInPercent { get; init; } = 85;

    public int InTheZonePercent { get; init; } = 65;

    public int HalfHerePercent { get; init; } = 40;

    // Sessions shorter than this are too short to rate.
    public int MinimumRatedSeconds { get; init; } = 60;

    // Days of focus history to keep, including today.
    public int HistoryRetentionDays { get; init; } = 7;

    // Process names, without ".exe", that do not count as switches.
    public IReadOnlyList<string> ExcludedProcessNames { get; init; } = [];

    public CommentLanguage CommentLanguage { get; init; } = CommentLanguage.English;

    public bool Use24HourClock { get; init; } = true;

    public bool ShowSeconds { get; init; } = true;

    // Returns a message describing the first invalid value, or null when all are valid.
    public string? GetValidationError()
    {
        if (LockedInPercent is < 1 or > 100 || InTheZonePercent is < 1 or > 100 || HalfHerePercent is < 1 or > 100)
        {
            return "Rating thresholds must be between 1 and 100.";
        }

        if (!(LockedInPercent > InTheZonePercent && InTheZonePercent > HalfHerePercent))
        {
            return "Thresholds must go down: LOCKED IN > IN THE ZONE > HALF HERE.";
        }

        if (MinimumRatedSeconds is < 0 or > MaxMinimumRatedSeconds)
        {
            return $"Minimum session length must be between 0 and {MaxMinimumRatedSeconds} seconds.";
        }

        if (HistoryRetentionDays is < MinRetentionDays or > MaxRetentionDays)
        {
            return $"History must be kept for {MinRetentionDays} to {MaxRetentionDays} days.";
        }

        return null;
    }

    // Returns a valid copy: invalid groups of values fall back to their defaults
    // and excluded process names are cleaned up. Used for settings read from disk.
    public AppSettings Normalized()
    {
        var defaults = new AppSettings();
        var result = this with { ExcludedProcessNames = NormalizeProcessNames(ExcludedProcessNames ?? []) };

        // Check only the thresholds: the other numbers are clamped below.
        var thresholdsOnly = result with
        {
            MinimumRatedSeconds = defaults.MinimumRatedSeconds,
            HistoryRetentionDays = defaults.HistoryRetentionDays
        };
        if (thresholdsOnly.GetValidationError() is not null)
        {
            result = result with
            {
                LockedInPercent = defaults.LockedInPercent,
                InTheZonePercent = defaults.InTheZonePercent,
                HalfHerePercent = defaults.HalfHerePercent
            };
        }

        return result with
        {
            MinimumRatedSeconds = Math.Clamp(result.MinimumRatedSeconds, 0, MaxMinimumRatedSeconds),
            HistoryRetentionDays = Math.Clamp(result.HistoryRetentionDays, MinRetentionDays, MaxRetentionDays),
            CommentLanguage = Enum.IsDefined(result.CommentLanguage) ? result.CommentLanguage : defaults.CommentLanguage
        };
    }

    // True when every value matches, comparing process names without case.
    public bool HasSameValues(AppSettings other) =>
        // Record equality compares lists by reference, so compare them separately.
        this with { ExcludedProcessNames = Array.Empty<string>() } ==
        other with { ExcludedProcessNames = Array.Empty<string>() } &&
        ExcludedProcessNames.SequenceEqual(other.ExcludedProcessNames, StringComparer.OrdinalIgnoreCase);

    // "Spotify.exe " -> "Spotify"
    public static string NormalizeProcessName(string name)
    {
        var trimmed = name.Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4].Trim()
            : trimmed;
    }

    private static IReadOnlyList<string> NormalizeProcessNames(IEnumerable<string> names) =>
        names
            .Where(name => name is not null)
            .Select(NormalizeProcessName)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
