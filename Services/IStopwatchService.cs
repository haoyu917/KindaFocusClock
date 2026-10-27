namespace MyClock.Services;


public interface IStopwatchService
{
    
    TimeSpan Elapsed { get; }

    
    bool IsRunning { get; }

    
    void Start();

    
    void Pause();

    
    void Resume();

    
    void Reset();
}
