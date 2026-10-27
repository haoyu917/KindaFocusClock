using System.Windows.Threading;

namespace MyClock.Services;

public sealed class ClockService : IClockService
{
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    public ClockService()
    {

        CurrentTime = DateTime.Now;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    public DateTime CurrentTime { get; private set; }

    public event EventHandler? TimeChanged;

    private void OnTimerTick(object? sender, EventArgs e)
    {

        CurrentTime = DateTime.Now;
        TimeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {

        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _timer.Stop();
        _timer.Tick -= OnTimerTick;
    }
}
