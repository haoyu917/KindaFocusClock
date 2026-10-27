namespace MyClock.Services;

public interface IClockService : IDisposable
{

    DateTime CurrentTime { get; }

    event EventHandler? TimeChanged;
}
