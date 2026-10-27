# MyClock

MyClock is a Windows WPF desktop application with three sections:

- **Clock** — displays the current local time.
- **Window Monitor** — measures how long a selected application window remains the active foreground window.
- **Settings** — reserved for future themes, alarms, and other preferences.

## Requirements

- Windows 10 or later
- .NET 10 SDK

This is a Windows-only WPF application. The .NET SDK is required to restore dependencies, compile XAML, and build the project.

## Build and run

Open a terminal in this directory and run:

```powershell
dotnet restore
dotnet build .\MyClock.csproj
dotnet run --project .\MyClock.csproj
```

You can also open `WpfApp1.sln` in Visual Studio or another .NET-compatible Windows IDE and run the `MyClock` project.

## How to use the application

1. Start the application.
2. Use the navigation panel on the left to choose **Clock**, **Window Monitor**, or **Settings**.
3. In **Window Monitor**, select an open window from **TARGET WINDOW**.
4. Select **Start / Continue** to begin monitoring.
5. Work in the selected window. The timer runs while that window is the Windows foreground window and waits while another window is active.
6. Select **Pause** to stop monitoring while preserving the accumulated time.
7. Select **Reset** to clear the current session, the selected target, and the recorded active time for all windows.

The monitor refreshes the list of visible, titled windows automatically. MyClock excludes its own window from that list.

## Window Monitor display

### Current target

Shows the window currently selected for monitoring. The status badge reports the current state:

| Status | Meaning |
| --- | --- |
| `NOT SELECTED` | No target window has been selected. |
| `ACTIVE` | Monitoring is enabled and the target window is in the foreground. The timer is running. |
| `WAITING` | Monitoring is enabled, but another window is in the foreground. |
| `PAUSED` | Monitoring has been paused by the user. |
| `WINDOW LOST` | The selected window is no longer available. |
| `UNAVAILABLE` | The foreground window could not be read. |

### Current session

Shows the elapsed time for the current monitoring session in `minutes:seconds.tenths` format. It measures the selected target's session and pauses when monitoring is paused, the target is in the background, or the monitor page is hidden.

The description below the timer explains the current state, such as `Target is in foreground · Timer running` or `Target is in background · Waiting`.

### Target window and controls

- **TARGET WINDOW** — the list of currently visible, titled Windows windows that can be monitored.
- **Start / Continue** — starts monitoring the selected target or resumes monitoring.
- **Pause** — pauses monitoring without clearing elapsed values.
- **Reset** — clears the current session and all recorded active-time values, then removes the target selection.

### Today Active

Shows how long the selected window has been the foreground window today, in `hours:minutes:seconds` format. Each window keeps its own value, so changing targets does not erase the previous window's value during the same day. The values reset when you use **Reset** or when a new day begins.

### Foreground Rate

Shows the percentage of the current monitoring session during which the selected window was in the foreground. It is calculated from actual elapsed time rather than UI refresh ticks.

### Last Transition

Shows the previous target window when the selected target changes. It displays `--` until a previous target exists.

## Clock and Settings

The **Clock** page displays the local system time in 24-hour `HH:mm:ss` format and updates once per second.

The **Settings** page is currently a placeholder. Theme, alarm, and additional preference features are planned for a future version.

## Technical overview

The project uses WPF and XAML for the desktop interface, MVVM for presentation state, and dependency injection for application services. Window monitoring uses Windows APIs to enumerate visible windows and identify the current foreground window. Timing uses `System.Diagnostics.Stopwatch`, so UI refresh delays do not become part of the measured duration.

The main folders are:

- `Models` — data models and monitor states.
- `Services` — clock, stopwatch, and Windows window-tracking logic.
- `ViewModels` — bindable state and commands.
- `Views` — WPF pages.
- `Commands` — command implementations.
- `Resources` — shared visual styles and theme resources.
