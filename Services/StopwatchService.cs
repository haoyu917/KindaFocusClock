namespace MyClock.Services;

public sealed class StopwatchService : IStopwatchService
{

    private readonly System.Diagnostics.Stopwatch _stopwatch = new();

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public bool IsRunning => _stopwatch.IsRunning;

    public void Start() => _stopwatch.Start();

    public void Pause() => _stopwatch.Stop();

    public void Resume() => _stopwatch.Start();

    public void Reset() => _stopwatch.Reset();
}
