using MyClock.Models;

namespace MyClock.Services;







public interface IWindowTrackingService : IDisposable
{
    
    IReadOnlyList<OpenWindowInfo> OpenWindows { get; }

    
    nint ForegroundWindowHandle { get; }

    
    event EventHandler? WindowsChanged;

    
    event EventHandler? ForegroundWindowChanged;

    
    bool IsForeground(nint windowHandle);
}
