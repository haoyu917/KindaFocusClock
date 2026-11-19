using MyClock.Models;

namespace MyClock.Services;

// Measures and saves the focus session for the current target window.
public interface IFocusSessionRecorder : IDisposable
{
    // Monitored time of the current session; zero when no session is open.
    TimeSpan MonitoredDuration { get; }

    // Target foreground time of the current session; zero when no session is open.
    TimeSpan ForegroundDuration { get; }

    // Times focus left the target in the current session; zero when no session is open.
    int SwitchCount { get; }

    bool IsRecording(nint windowHandle);

    // Ends any open session and starts a new one for the window.
    void Begin(OpenWindowInfo window);

    // Call whenever monitoring state or the foreground window may have changed.
    // isMonitoring is false while the user has paused or the monitor page is hidden.
    void Update(bool isMonitoring);

    // Saves and closes the current session, if any.
    void End();
}
