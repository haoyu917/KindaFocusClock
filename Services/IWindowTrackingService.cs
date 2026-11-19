using MyClock.Models;

namespace MyClock.Services;

public interface IWindowTrackingService : IDisposable
{

    IReadOnlyList<OpenWindowInfo> OpenWindows { get; }

    nint ForegroundWindowHandle { get; }

    event EventHandler? WindowsChanged;

    event EventHandler? ForegroundWindowChanged;

    bool IsForeground(nint windowHandle);

    // True when the window belongs to MyClock itself.
    bool IsCurrentProcessWindow(nint windowHandle);

    // Current title of the window, or null when it has none.
    string? GetWindowTitle(nint windowHandle);

    // Executable name of the window's process, or null when it cannot be read.
    string? GetProcessName(nint windowHandle);
}
