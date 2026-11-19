using MyClock.Models;
using MyClock.Services;

namespace MyClock.ViewModels;

// A row in the summary page's distraction breakdown.
public sealed class SummaryDistractionItem
{
    public SummaryDistractionItem(FocusDistraction distraction)
    {
        Name = distraction.Name;
        WindowTitle = distraction.TopWindowTitle ?? "No window title";
        CountText = FocusSummaryBuilder.FormatTimes(distraction.VisitCount);
        TimeText = FocusSummaryBuilder.FormatDuration(distraction.TimeSpent);
        Share = distraction.ShareOfAwayTime;
    }

    public string Name { get; }

    public string WindowTitle { get; }

    public string CountText { get; }

    public string TimeText { get; }

    // Share of the session's time away, from 0 to 1.
    public double Share { get; }
}
