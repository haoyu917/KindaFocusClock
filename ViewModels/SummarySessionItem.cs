using MyClock.Models;
using MyClock.Services;

namespace MyClock.ViewModels;

// A session in the summary page's session picker.
public sealed class SummarySessionItem
{
    public SummarySessionItem(FocusSession session)
    {
        Session = session;
        var name = string.IsNullOrEmpty(session.ProcessName) ? session.WindowTitle : session.ProcessName;
        Label = $"{FocusSummaryBuilder.FormatStartTime(session.StartedAt)} · {name} · " +
                FocusSummaryBuilder.FormatDuration(session.MonitoredDuration);
    }

    public FocusSession Session { get; }

    // Today 10:32 · devenv · 27m 50s
    public string Label { get; }
}
