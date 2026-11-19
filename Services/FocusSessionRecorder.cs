using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using MyClock.Models;

namespace MyClock.Services;

public sealed class FocusSessionRecorder : IFocusSessionRecorder
{
    // Durations are saved periodically instead of on every change, so frequent
    // window-list refreshes do not turn into frequent disk writes.
    private static readonly TimeSpan AutoSaveInterval = TimeSpan.FromSeconds(30);

    private readonly IFocusSessionRepository _repository;
    private readonly IWindowTrackingService _windowTrackingService;
    private readonly DispatcherTimer _autoSaveTimer;
    private readonly Stopwatch _monitoredStopwatch = new();
    private readonly Stopwatch _foregroundStopwatch = new();
    private FocusSession? _currentSession;
    private nint _windowHandle;
    private bool _isAwayFromTarget;
    private bool _hasUnsavedChanges;
    private bool _disposed;

    public FocusSessionRecorder(IFocusSessionRepository repository, IWindowTrackingService windowTrackingService)
    {
        _repository = repository;
        _windowTrackingService = windowTrackingService;
        _autoSaveTimer = new DispatcherTimer { Interval = AutoSaveInterval };
        _autoSaveTimer.Tick += OnAutoSaveTimerTick;
    }

    public TimeSpan MonitoredDuration => _monitoredStopwatch.Elapsed;

    public TimeSpan ForegroundDuration => _foregroundStopwatch.Elapsed;

    public int SwitchCount => _currentSession?.SwitchCount ?? 0;

    public bool IsRecording(nint windowHandle) =>
        _currentSession is not null && _windowHandle == windowHandle;

    public void Begin(OpenWindowInfo window)
    {
        End();

        _windowHandle = window.Handle;
        _isAwayFromTarget = false;
        _currentSession = new FocusSession
        {
            WindowTitle = window.Title,
            ProcessName = _windowTrackingService.GetProcessName(window.Handle),
            StartedAt = DateTimeOffset.Now
        };

        Save();
        _autoSaveTimer.Start();
    }

    public void Update(bool isMonitoring)
    {
        if (_currentSession is null)
        {
            return;
        }

        if (!isMonitoring)
        {
            if (_monitoredStopwatch.IsRunning)
            {
                _monitoredStopwatch.Stop();
                _foregroundStopwatch.Stop();
                _hasUnsavedChanges = true;
            }

            return;
        }

        // The monitored time includes time spent in other windows; only the
        // foreground time pauses when focus moves away from the target.
        _monitoredStopwatch.Start();

        var foregroundHandle = _windowTrackingService.ForegroundWindowHandle;
        if (foregroundHandle == _windowHandle)
        {
            _foregroundStopwatch.Start();
            _isAwayFromTarget = false;
            return;
        }

        _foregroundStopwatch.Stop();

        // No foreground window (for example mid Alt+Tab) and MyClock itself are not
        // switches: using MyClock to pause or change targets is not a distraction.
        // Count only the first window reached after leaving the target, so moving
        // between several other windows before returning counts once.
        if (foregroundHandle == nint.Zero ||
            _windowTrackingService.IsCurrentProcessWindow(foregroundHandle) ||
            _isAwayFromTarget)
        {
            return;
        }

        _isAwayFromTarget = true;
        _currentSession.SwitchCount++;
        _hasUnsavedChanges = true;
    }

    public void End()
    {
        if (_currentSession is null)
        {
            return;
        }

        _autoSaveTimer.Stop();
        _monitoredStopwatch.Stop();
        _foregroundStopwatch.Stop();
        _currentSession.EndedAt = DateTimeOffset.Now;
        Save();

        _currentSession = null;
        _windowHandle = nint.Zero;
        _isAwayFromTarget = false;
        _monitoredStopwatch.Reset();
        _foregroundStopwatch.Reset();
    }

    private void OnAutoSaveTimerTick(object? sender, EventArgs e)
    {
        if (_monitoredStopwatch.IsRunning || _hasUnsavedChanges)
        {
            Save();
        }
    }

    private void Save()
    {
        if (_currentSession is null)
        {
            return;
        }

        _currentSession.MonitoredDuration = _monitoredStopwatch.Elapsed;
        _currentSession.ForegroundDuration = _foregroundStopwatch.Elapsed;

        try
        {
            if (_currentSession.Id == 0)
            {
                _currentSession.Id = _repository.Add(_currentSession);
            }
            else
            {
                _repository.Update(_currentSession);
            }

            _hasUnsavedChanges = false;
        }
        catch (Exception exception) when (exception is DbException or IOException or UnauthorizedAccessException)
        {
            // Saving must never interrupt monitoring. The next save retries,
            // inserting the session if the first insert failed.
            _hasUnsavedChanges = true;
            Debug.WriteLine($"Failed to save focus session: {exception.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        End();
        _autoSaveTimer.Tick -= OnAutoSaveTimerTick;
    }
}
