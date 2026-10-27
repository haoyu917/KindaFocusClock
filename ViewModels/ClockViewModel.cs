using MyClock.Services;

namespace MyClock.ViewModels;

public sealed class ClockViewModel : ViewModelBase, IDisposable
{
    private readonly IClockService _clockService;
    private bool _disposed;

    public ClockViewModel(IClockService clockService)
    {
        _clockService = clockService;

        _clockService.TimeChanged += OnTimeChanged;
    }

    public string CurrentTime => _clockService.CurrentTime.ToString("HH:mm:ss");

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
    }
}
