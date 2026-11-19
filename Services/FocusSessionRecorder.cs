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

    // All stopwatches run only while monitoring, so paused time is never counted.
    private readonly Stopwatch _monitoredStopwatch = new();
    private readonly Stopwatch _foregroundStopwatch = new();
    private readonly Stopwatch _awayStopwatch = new();
    private readonly Stopwatch _visitStopwatch = new();

    // Switches added or changed since the last successful save.
    private readonly HashSet<FocusSwitch> _unsavedSwitches = [];

    private FocusSession? _currentSession;
    private nint _windowHandle;
    private FocusSwitch? _currentSwitch;
    private FocusSwitchVisit? _currentVisit;
    private nint _currentVisitHandle;
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
                // An open switch and visit stay open; they continue if the same
                // window is still in the foreground when monitoring resumes.
                _monitoredStopwatch.Stop();
                _foregroundStopwatch.Stop();
                _awayStopwatch.Stop();
                _visitStopwatch.Stop();
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
            CloseSwitch(hasReturned: true);
            return;
        }

        _foregroundStopwatch.Stop();

        // No foreground window (for example mid Alt+Tab) and MyClock itself are not
        // switches: using MyClock to pause or change targets is not a distraction.
        var isOtherApplication = foregroundHandle != nint.Zero &&
                                 !_windowTrackingService.IsCurrentProcessWindow(foregroundHandle);

        if (_currentSwitch is null)
        {
            if (!isOtherApplication)
            {
                return;
            }

            // Only the first window reached after leaving the target opens a switch,
            // so moving between several other windows before returning counts once.
            OpenSwitch();
        }

        _awayStopwatch.Start();

        if (!isOtherApplication)
        {
            // Still away from the target, but not in a window worth attributing time to.
            CloseVisit();
            return;
        }

        if (_currentVisit is not null && _currentVisitHandle == foregroundHandle)
        {
            _visitStopwatch.Start();
            return;
        }

        CloseVisit();
        OpenVisit(foregroundHandle);
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
        CloseSwitch(hasReturned: false);
        _currentSession.EndedAt = DateTimeOffset.Now;
        Save();

        // Unsaved switches cannot be retried once the session is gone.
        _unsavedSwitches.Clear();
        _currentSession = null;
        _windowHandle = nint.Zero;
        _monitoredStopwatch.Reset();
        _foregroundStopwatch.Reset();
    }

    public void Flush() => Save();

    private void OpenSwitch()
    {
        _currentSwitch = new FocusSwitch { LeftAt = DateTimeOffset.Now };
        _currentSession!.Switches.Add(_currentSwitch);
        _currentSession.SwitchCount++;
        _awayStopwatch.Restart();
        _unsavedSwitches.Add(_currentSwitch);
        _hasUnsavedChanges = true;
    }

    private void CloseSwitch(bool hasReturned)
    {
        if (_currentSwitch is null)
        {
            return;
        }

        CloseVisit();
        _awayStopwatch.Stop();
        _currentSwitch.AwayDuration = _awayStopwatch.Elapsed;
        if (hasReturned)
        {
            _currentSwitch.ReturnedAt = DateTimeOffset.Now;
        }

        _unsavedSwitches.Add(_currentSwitch);
        _hasUnsavedChanges = true;
        _currentSwitch = null;
        _awayStopwatch.Reset();
    }

    private void OpenVisit(nint windowHandle)
    {
        _currentVisit = new FocusSwitchVisit
        {
            WindowTitle = _windowTrackingService.GetWindowTitle(windowHandle) ?? string.Empty,
            ProcessName = _windowTrackingService.GetProcessName(windowHandle),
            StartedAt = DateTimeOffset.Now
        };
        _currentVisitHandle = windowHandle;
        _currentSwitch!.Visits.Add(_currentVisit);
        _visitStopwatch.Restart();
        _unsavedSwitches.Add(_currentSwitch);
        _hasUnsavedChanges = true;
    }

    private void CloseVisit()
    {
        if (_currentVisit is null)
        {
            return;
        }

        _visitStopwatch.Stop();
        _currentVisit.Duration = _visitStopwatch.Elapsed;
        _unsavedSwitches.Add(_currentSwitch!);
        _hasUnsavedChanges = true;
        _currentVisit = null;
        _currentVisitHandle = nint.Zero;
        _visitStopwatch.Reset();
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
        if (_currentSwitch is not null)
        {
            _currentSwitch.AwayDuration = _awayStopwatch.Elapsed;
            _unsavedSwitches.Add(_currentSwitch);
        }

        if (_currentVisit is not null)
        {
            _currentVisit.Duration = _visitStopwatch.Elapsed;
        }

        try
        {
            _repository.Save(_currentSession, _unsavedSwitches);
            _unsavedSwitches.Clear();
            _hasUnsavedChanges = false;
        }
        catch (Exception exception) when (exception is DbException or IOException or UnauthorizedAccessException)
        {
            // Saving must never interrupt monitoring. The next save retries
            // everything that has not been saved yet.
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
