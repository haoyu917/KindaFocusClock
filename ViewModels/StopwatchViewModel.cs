using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using MyClock.Commands;
using MyClock.Models;
using MyClock.Services;

namespace MyClock.ViewModels;







public sealed class StopwatchViewModel : ViewModelBase, IDisposable
{
    private readonly IStopwatchService _stopwatchService;
    private readonly IWindowTrackingService _windowTrackingService;
    private readonly DispatcherTimer _refreshTimer;
    private readonly RelayCommand _startCommand;
    private readonly RelayCommand _pauseCommand;
    private readonly RelayCommand _resetCommand;
    
    
    private readonly System.Diagnostics.Stopwatch _monitoringSessionStopwatch = new();
    
    
    private readonly System.Diagnostics.Stopwatch _foregroundSessionStopwatch = new();
    
    
    private readonly Dictionary<nint, System.Diagnostics.Stopwatch> _todayActiveStopwatches = new();
    private bool _isPageActive;
    private bool _wasRunningBeforeUnload;
    private bool _isTrackingEnabled;
    private OpenWindowInfo? _selectedWindow;
    private string? _lastSelectedWindowTitle;
    private bool _isWindowLost;
    private bool _isRefreshingWindows;
    private bool _isPercentageCleared;
    private DateTime _activeDate = DateTime.Today;
    
    
    private string? _lastNonTargetWindowTitle;
    private bool _disposed;

    public StopwatchViewModel(IStopwatchService stopwatchService, IWindowTrackingService windowTrackingService)
    {
        
        
        _stopwatchService = stopwatchService;
        _windowTrackingService = windowTrackingService;
        OpenWindows = new ObservableCollection<OpenWindowInfo>(_windowTrackingService.OpenWindows);
        _windowTrackingService.WindowsChanged += OnWindowsChanged;
        _windowTrackingService.ForegroundWindowChanged += OnForegroundWindowChanged;
        
        
        _startCommand = new RelayCommand(Start, () => CanStart);
        _pauseCommand = new RelayCommand(Pause, () => CanPause);
        _resetCommand = new RelayCommand(Reset, () => CanReset);
        
        
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _refreshTimer.Tick += OnRefreshTimerTick;
    }

    
    public string ElapsedTime => _stopwatchService.Elapsed.ToString(@"mm\:ss\.f");

    
    public bool IsRunning => _stopwatchService.IsRunning;

    
    public string SelectedWindowTitle => SelectedWindow?.Title ?? (_isWindowLost ? _lastSelectedWindowTitle ?? "Unknown window" : "No window selected");

    
    public MonitorState CurrentMonitorState
    {
        get
        {
            if (_isWindowLost)
            {
                return MonitorState.WindowLost;
            }

            if (SelectedWindow is null)
            {
                return MonitorState.NotSelected;
            }

            if (!IsTrackingEnabled)
            {
                return MonitorState.Paused;
            }

            return _windowTrackingService.IsForeground(SelectedWindow.Handle)
                ? MonitorState.Active
                : MonitorState.Background;
        }
    }

    
    public string MonitorStateText => CurrentMonitorState switch
    {
        MonitorState.Active => "ACTIVE",
        MonitorState.Background => "WAITING",
        MonitorState.Paused => "PAUSED",
        MonitorState.WindowLost => "WINDOW LOST",
        MonitorState.Unavailable => "UNAVAILABLE",
        _ => "NOT SELECTED"
    };

    
    public string MonitorDescription => CurrentMonitorState switch
    {
        MonitorState.Active => "Target is in foreground · Timer running",
        MonitorState.Background => "Target is in background · Waiting",
        MonitorState.Paused => "Monitoring paused by user",
        MonitorState.WindowLost => "Target window is no longer available",
        MonitorState.Unavailable => "The foreground window cannot be read",
        _ => "Select a target window to begin"
    };

    
    public bool CanStart => SelectedWindow is not null && !IsTrackingEnabled;

    
    public bool CanPause => IsTrackingEnabled;

    
    public bool CanReset => IsRunning || _stopwatchService.Elapsed > TimeSpan.Zero;

    
    
    
    
    
    
    
    
    public string ForegroundTimePercentage
    {
        get
        {
            if (_isPercentageCleared)
            {
                return "0.0%";
            }

            if (_monitoringSessionStopwatch.Elapsed <= TimeSpan.Zero)
            {
                return "--";
            }

            
            
            var percentage = _foregroundSessionStopwatch.Elapsed.TotalMilliseconds /
                             _monitoringSessionStopwatch.Elapsed.TotalMilliseconds * 100;
            return $"{Math.Clamp(percentage, 0, 100):0.0}%";
        }
    }

    
    public string TodayActiveTime
    {
        get
        {
            EnsureCurrentDay();
            if (SelectedWindow is null)
            {
                return "00:00:00";
            }

            
            
            var elapsed = GetTodayActiveStopwatch(SelectedWindow.Handle).Elapsed;
            var totalHours = (int)elapsed.TotalHours;
            return $"{totalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        }
    }

    
    public string LastTransitionWindow => _lastNonTargetWindowTitle ?? "--";

    
    public ICommand StartCommand => _startCommand;

    
    public ICommand PauseCommand => _pauseCommand;

    
    public ICommand ResetCommand => _resetCommand;

    
    public ObservableCollection<OpenWindowInfo> OpenWindows { get; }

    public OpenWindowInfo? SelectedWindow
    {
        get => _selectedWindow;
        set
        {
            var previousHandle = _selectedWindow?.Handle;
            if (previousHandle is not null && value is not null && previousHandle != value.Handle)
            {
                _lastNonTargetWindowTitle = _selectedWindow?.Title ?? _lastSelectedWindowTitle;
            }

            if (previousHandle is not null && (value is null || previousHandle != value.Handle))
            {
                StopTodayActiveMeasurement(previousHandle.Value);
            }

            if (!SetProperty(ref _selectedWindow, value))
            {
                return;
            }

            if (!_isRefreshingWindows)
            {
                _isWindowLost = false;
            }

            if (value is not null)
            {
                _lastSelectedWindowTitle = value.Title;
                // Start a new percentage session only when the user changes targets
                // refreshing the window list must preserve the current session
                if (previousHandle != value.Handle && !_isRefreshingWindows)
                {
                    // Allow the result to show normally
                    _isPercentageCleared = false;
                    _monitoringSessionStopwatch.Reset();
                    _foregroundSessionStopwatch.Reset();
                    _activeDate = DateTime.Today;
                }
            }
            
            EvaluateTrackingState();
            NotifyDisplayStateChanged();
        }
    }

    public bool IsTrackingEnabled
    {
        get => _isTrackingEnabled;
        private set => SetProperty(ref _isTrackingEnabled, value);
    }

    
    public string TrackingStatus => MonitorDescription;

    
    
    
    
    
    
    public void OnPageLoaded()
    {
        
        
        
        
        _isPageActive = true;
        _refreshTimer.Start();

        if (_wasRunningBeforeUnload)
        {
            
            
            IsTrackingEnabled = true;
            _wasRunningBeforeUnload = false;
            EvaluateTrackingState();
        }
    }

    
    
    
    
    
    
    public void OnPageUnloaded()
    {
        
        
        
        
        _isPageActive = false;
        _wasRunningBeforeUnload = IsTrackingEnabled;
        IsTrackingEnabled = false;
        StopSessionMeasurement();
        _stopwatchService.Pause();

        _refreshTimer.Stop();
        NotifyStateChanged();
    }

    private void Start()
    {
        
        
        
        
        IsTrackingEnabled = true;
        EvaluateTrackingState();
    }

    private void Pause()
    {
        
        
        IsTrackingEnabled = false;
        StopSessionMeasurement();
        _stopwatchService.Pause();
        NotifyStateChanged();
    }

    private void Reset()
    {
        
        
        IsTrackingEnabled = false;
        StopSessionMeasurement();
        _monitoringSessionStopwatch.Reset();
        _foregroundSessionStopwatch.Reset();
        _todayActiveStopwatches.Clear();
        _isPercentageCleared = true;
        _lastNonTargetWindowTitle = null;
        _stopwatchService.Reset();
        
        
        SelectedWindow = null;
        NotifyStateChanged();
    }

    private void OnWindowsChanged(object? sender, EventArgs e)
    {
        var selectedHandle = SelectedWindow?.Handle;
        if (selectedHandle is not null)
        {
            StopTodayActiveMeasurement(selectedHandle.Value);
        }
        _isRefreshingWindows = true;
        try
        {
            OpenWindows.Clear();
            foreach (var window in _windowTrackingService.OpenWindows)
            {
                OpenWindows.Add(window);
            }
            
            var refreshedSelection = selectedHandle is not null
                ? OpenWindows.FirstOrDefault(window => window.Handle == selectedHandle)
                : null;

            if (selectedHandle is not null && refreshedSelection is null)
            {
                // Keep the selected handle and accumulated values if enumeration
                // temporarily drops it during an Alt+Tab transition.
                _isWindowLost = true;
                StopSessionMeasurement();
                _stopwatchService.Pause();
            }
            else
            {
                _isWindowLost = false;
                SelectedWindow = refreshedSelection;
            }
        }
        finally
        {
            _isRefreshingWindows = false;
        }

        NotifyDisplayStateChanged();
    }

    private void OnForegroundWindowChanged(object? sender, EventArgs e)
    {

        EvaluateTrackingState();
        NotifyDisplayStateChanged();
    }

    private void EvaluateTrackingState()
    {
        if (!_isPageActive || !IsTrackingEnabled || SelectedWindow is null || _isWindowLost)
        {
            StopSessionMeasurement();
            _stopwatchService.Pause();
            NotifyStateChanged();
            return;
        }
        
        var isForeground = _windowTrackingService.IsForeground(SelectedWindow.Handle);
        if (isForeground)
        {
            _stopwatchService.Resume();
        }
        else
        {
            _stopwatchService.Pause();
        }

        UpdateSessionMeasurement(isForeground);
        NotifyStateChanged();
    }

    private void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        if (_isPageActive)
        {
            EnsureCurrentDay();
            OnPropertyChanged(nameof(ElapsedTime));
            OnPropertyChanged(nameof(ForegroundTimePercentage));
            OnPropertyChanged(nameof(TodayActiveTime));
        }
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(ElapsedTime));
        OnPropertyChanged(nameof(IsRunning));
        _startCommand.RaiseCanExecuteChanged();
        _pauseCommand.RaiseCanExecuteChanged();
        _resetCommand.RaiseCanExecuteChanged();
        NotifyDisplayStateChanged();
    }

    private void NotifyDisplayStateChanged()
    {
        OnPropertyChanged(nameof(SelectedWindowTitle));
        OnPropertyChanged(nameof(CurrentMonitorState));
        OnPropertyChanged(nameof(MonitorStateText));
        OnPropertyChanged(nameof(MonitorDescription));
        OnPropertyChanged(nameof(TrackingStatus));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanReset));
        OnPropertyChanged(nameof(ForegroundTimePercentage));
        OnPropertyChanged(nameof(TodayActiveTime));
        OnPropertyChanged(nameof(LastTransitionWindow));
    }

    private void UpdateSessionMeasurement(bool isForeground)
    {
        EnsureCurrentDay();
        // The session denominator includes time spent in other real windows.
        // Only the target's foreground time pauses when focus moves away.
        _monitoringSessionStopwatch.Start();
        if (isForeground)
        {
            _foregroundSessionStopwatch.Start();
            if (SelectedWindow is not null)
            {
                GetTodayActiveStopwatch(SelectedWindow.Handle).Start();
            }
        }
        else
        {
            _foregroundSessionStopwatch.Stop();
            if (SelectedWindow is not null)
            {
                StopTodayActiveMeasurement(SelectedWindow.Handle);
            }
        }
    }

    private void StopSessionMeasurement()
    {
        _monitoringSessionStopwatch.Stop();
        _foregroundSessionStopwatch.Stop();
        if (SelectedWindow is not null)
        {
            StopTodayActiveMeasurement(SelectedWindow.Handle);
        }
    }

    private void EnsureCurrentDay()
    {
        if (_activeDate == DateTime.Today)
        {
            return;
        }
        
        foreach (var stopwatch in _todayActiveStopwatches.Values)
        {
            stopwatch.Reset();
        }

        _activeDate = DateTime.Today;
        if (_isPageActive && IsTrackingEnabled && SelectedWindow is not null && _windowTrackingService.IsForeground(SelectedWindow.Handle))
        {
            GetTodayActiveStopwatch(SelectedWindow.Handle).Start();
        }
    }

    private System.Diagnostics.Stopwatch GetTodayActiveStopwatch(nint windowHandle)
    {
        if (!_todayActiveStopwatches.TryGetValue(windowHandle, out var stopwatch))
        {
            stopwatch = new System.Diagnostics.Stopwatch();
            _todayActiveStopwatches[windowHandle] = stopwatch;
        }

        return stopwatch;
    }

    private void StopTodayActiveMeasurement(nint windowHandle)
    {
        if (_todayActiveStopwatches.TryGetValue(windowHandle, out var stopwatch))
        {
            stopwatch.Stop();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _windowTrackingService.WindowsChanged -= OnWindowsChanged;
        _windowTrackingService.ForegroundWindowChanged -= OnForegroundWindowChanged;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= OnRefreshTimerTick;
    }
}
