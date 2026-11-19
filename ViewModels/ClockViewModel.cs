using System.Globalization;
using MyClock.Services;

namespace MyClock.ViewModels;

public sealed class ClockViewModel : ViewModelBase, IDisposable
{
    private readonly IClockService _clockService;
    private readonly ISettingsService _settingsService;
    private bool _disposed;

    public ClockViewModel(IClockService clockService, ISettingsService settingsService)
    {
        _clockService = clockService;
        _settingsService = settingsService;

        _clockService.TimeChanged += OnTimeChanged;
        _settingsService.SettingsChanged += OnTimeChanged;
    }

    // 14:05:09, 14:05, 2:05:09 PM, or 2:05 PM, depending on the clock settings.
    public string CurrentTime => _clockService.CurrentTime.ToString(TimeFormat, CultureInfo.InvariantCulture);

    private string TimeFormat
    {
        get
        {
            var settings = _settingsService.Current;
            return (settings.Use24HourClock, settings.ShowSeconds) switch
            {
                (true, true) => "HH:mm:ss",
                (true, false) => "HH:mm",
                (false, true) => "h:mm:ss tt",
                _ => "h:mm tt"
            };
        }
    }

    private void OnTimeChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CurrentTime));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _clockService.TimeChanged -= OnTimeChanged;
        _settingsService.SettingsChanged -= OnTimeChanged;
    }
}
