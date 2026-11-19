using MyClock.Models;
using MyClock.Services;

namespace MyClock.ViewModels;

// Display values for the summary page's TODAY card. It exposes Verdict, so the
// card can reuse the verdict badge and color styles.
public sealed class TodaySummaryItem
{
    public TodaySummaryItem(FocusDaySummary day)
    {
        var hasSessions = day.SessionCount > 0;
        Verdict = day.Verdict;
        Headline = day.Headline;
        Message = day.Message;
        SessionCountText = day.SessionCount.ToString();
        FocusTimeText = hasSessions ? FocusSummaryBuilder.FormatDuration(day.ForegroundDuration) : "--";
        RateText = day.MonitoredDuration > TimeSpan.Zero ? $"{day.ForegroundRate * 100:0.0}%" : "--";
        SwitchCountText = hasSessions ? day.SwitchCount.ToString() : "--";
        TopDistractionText = day.TopDistraction is { } top ? $"{top.Name} ({top.VisitCount})" : "--";
    }

    public FocusVerdict Verdict { get; }

    public string Headline { get; }

    public string Message { get; }

    public string SessionCountText { get; }

    public string FocusTimeText { get; }

    public string RateText { get; }

    public string SwitchCountText { get; }

    // WeChat (6): the application and how many times it was visited today.
    public string TopDistractionText { get; }
}
