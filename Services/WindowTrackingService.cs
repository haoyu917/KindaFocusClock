using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using MyClock.Models;

namespace MyClock.Services;









public sealed class WindowTrackingService : IWindowTrackingService
{
    private const int RefreshMilliseconds = 250;
    private readonly DispatcherTimer _timer;
    private IReadOnlyList<OpenWindowInfo> _openWindows = Array.Empty<OpenWindowInfo>();
    private nint _foregroundWindowHandle;
    private bool _disposed;

    public WindowTrackingService()
    {
        
        
        
        
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(RefreshMilliseconds) };
        _timer.Tick += OnTimerTick;
        RefreshWindowState();
        _timer.Start();
    }

    
    public IReadOnlyList<OpenWindowInfo> OpenWindows => _openWindows;

    
    public nint ForegroundWindowHandle => _foregroundWindowHandle;

    
    public event EventHandler? WindowsChanged;

    
    public event EventHandler? ForegroundWindowChanged;

    
    public bool IsForeground(nint windowHandle) =>
        windowHandle != nint.Zero && windowHandle == _foregroundWindowHandle;

    private void OnTimerTick(object? sender, EventArgs e)
    {
        
        
        RefreshWindowState();
    }

    private void RefreshWindowState()
    {
        
        
        
        
        
        
        var windows = EnumerateOpenWindows();
        if (!HaveSameWindows(_openWindows, windows))
        {
            _openWindows = windows;
            
            
            WindowsChanged?.Invoke(this, EventArgs.Empty);
        }

        var foregroundWindowHandle = GetForegroundWindow();
        if (_foregroundWindowHandle != foregroundWindowHandle)
        {
            _foregroundWindowHandle = foregroundWindowHandle;
            
            
            ForegroundWindowChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static IReadOnlyList<OpenWindowInfo> EnumerateOpenWindows()
    {
        
        
        
        
        var windows = new List<OpenWindowInfo>();
        var currentProcessId = Environment.ProcessId;

        EnumWindows((windowHandle, _) =>
        {
            
            
            if (!IsWindowVisible(windowHandle) || GetWindowThreadProcessId(windowHandle, out var processId) == 0 || processId == currentProcessId)
            {
                return true;
            }

            var titleLength = GetWindowTextLength(windowHandle);
            if (titleLength == 0)
            {
                return true;
            }

            var title = new StringBuilder(titleLength + 1);
            
            
            GetWindowText(windowHandle, title, title.Capacity);
            if (title.Length > 0)
            {
                windows.Add(new OpenWindowInfo(windowHandle, title.ToString()));
            }

            return true;
        }, nint.Zero);

        return windows;
    }

    private static bool HaveSameWindows(IReadOnlyList<OpenWindowInfo> first, IReadOnlyList<OpenWindowInfo> second)
    {
        
        
        
        
        if (first.Count != second.Count)
        {
            return false;
        }

        for (var index = 0; index < first.Count; index++)
        {
            if (first[index].Handle != second[index].Handle || first[index].Title != second[index].Title)
            {
                return false;
            }
        }

        return true;
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

    private delegate bool EnumWindowsCallback(nint windowHandle, nint lParam);

    
    
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, nint lParam);

    
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint windowHandle);

    
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint windowHandle, StringBuilder text, int maxCount);

    
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(nint windowHandle);

    
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint windowHandle, out uint processId);

    
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
}
