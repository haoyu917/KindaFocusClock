# MyClock

MyClock is a lightweight Windows productivity timer built with WPF and .NET 10. Its main feature is foreground-window tracking, allowing users to measure how much active time they spend in a selected application.

## Features

- **Clock** — displays the current local time in a clean desktop interface.
- **Window Monitor** — measures how long a selected application window remains the active foreground window.
- **Session Tracking** — pauses automatically when the target window is no longer in the foreground or when monitoring is paused.
- **Today Active** — tracks how much time each monitored window has spent in the foreground during the current day.
- **Foreground Rate** — shows the percentage of the current monitoring session spent actively using the selected window.
- **Switches** — counts how many times focus left the selected window during the current session.
- **Session History** — saves every monitoring session, with its switch count and foreground rate, to a local SQLite database.
- **Focus Summary** — rates each session from LOCKED IN to ESCAPE ARTIST, with a tip and the applications that pulled you away most.
- **Settings** — adjust rating thresholds, ignore apps such as music players, choose how many days of history to keep, switch comments to Chinese, and pick a 12- or 24-hour clock.

## Screenshots

Add screenshots of the Clock and Window Monitor pages here.

## Requirements

- Windows 10 or later
- .NET 10 SDK

MyClock is a Windows-only WPF application. The .NET SDK is required to restore dependencies, compile XAML, and build the project.

## Build and Run

Open a terminal in the project directory and run:

```powershell
dotnet restore
dotnet build .\MyClock.csproj
dotnet run --project .\MyClock.csproj
```

You can also open `WpfApp1.sln` in Visual Studio or another .NET-compatible Windows IDE and run the `MyClock` project.

## How to Use

1. Start the application.
2. Use the navigation panel on the left to choose **Clock**, **Window Monitor**, **Summary**, or **Settings**.
3. In **Window Monitor**, select an open window from **TARGET WINDOW**.
4. Select **Start / Continue** to begin monitoring.
5. Work in the selected window. The timer runs while that window is the Windows foreground window and waits while another window is active.
6. Select **Pause** to stop monitoring while preserving the accumulated time.
7. Select **Reset** to clear the current session, the selected target, and the recorded active time for all windows.

The monitor automatically refreshes the list of visible, titled application windows. MyClock excludes its own window from that list.

## Window Monitor

### Current Target

Shows the window currently selected for monitoring. The status badge reports the current state:

| Status | Meaning |
| --- | --- |
| `NOT SELECTED` | No target window has been selected. |
| `ACTIVE` | Monitoring is enabled and the target window is in the foreground. The timer is running. |
| `WAITING` | Monitoring is enabled, but another window is in the foreground. |
| `PAUSED` | Monitoring has been paused by the user. |
| `WINDOW LOST` | The selected window is no longer available. |
| `UNAVAILABLE` | The foreground window could not be read. |

### Current Session

Shows the elapsed time for the current monitoring session in `minutes:seconds.tenths` format, switching to `hours:minutes:seconds.tenths` once the session reaches one hour.

The timer runs only while the selected target is active. It pauses when monitoring is paused, the target is in the background, or the monitor page is hidden.

The description below the timer explains the current state, such as `Target is in foreground · Timer running` or `Target is in background · Waiting`.

### Target Window and Controls

- **TARGET WINDOW** — lists currently visible, titled application windows that can be monitored.
- **Start / Continue** — starts monitoring the selected target or resumes monitoring.
- **Pause** — pauses monitoring without clearing elapsed values.
- **Reset** — clears the current session and all recorded active-time values, then removes the target selection.

### Today Active

Shows how long the selected window has been the foreground window today in `hours:minutes:seconds` format.

Each window keeps its own value, so changing targets does not erase the previous window's value during the same day. Values reset when you use **Reset** or when a new day begins.

### Foreground Rate

Shows the percentage of the current monitoring session during which the selected window was in the foreground.

The value is calculated from actual elapsed time rather than UI refresh ticks.

### Switches

Counts how many times focus left the target for another application window during the current session. Moving between several other windows before returning to the target counts as one switch. Switching to MyClock itself (for example to pause or change targets) or to an app ignored in **Settings**, and the brief moment with no foreground window during Alt+Tab are not counted, and nothing is counted while monitoring is paused or the monitor page is hidden.

### Last Transition

Shows the previous target window when the selected target changes. It displays `--` until a previous target exists.

## Focus Summary

The **Summary** page reviews one monitoring session at a time. Choose a session from the list in the top-right corner; sessions from the kept days of history are listed (7 by default), newest first, and the session in progress includes its latest data.

The verdict is based on the session's foreground rate. These are the default thresholds, which can be changed in **Settings**:

| Verdict | Foreground rate |
| --- | --- |
| `LOCKED IN` | 85% or more |
| `IN THE ZONE` | 65% to 85% |
| `HALF HERE` | 40% to 65% |
| `ESCAPE ARTIST` | Below 40% |
| `WARMING UP` | Sessions shorter than one minute, which are too short to rate |

Each verdict comes with a light-hearted comment and a tip based on the session's data, in English or Chinese, such as the application that pulled you away most often or an unusually long average detour.

The page also shows:

- **FOCUS TIME** — time the target was in the foreground, out of the monitored time.
- **SWITCHES** — number of switches and switches per hour.
- **AVG DETOUR** — average and longest time away from the target per switch.
- **TOP DISTRACTION** — the application you switched to most often, with its visit count and time.
- **WHERE YOUR FOCUS WENT** — the top three applications visited while away, with each one's share of the time away.
- **TODAY SO FAR** — totals for all sessions that started today: a verdict and comment for the whole day, the number of sessions, focus time, the overall foreground rate, switches, and the day's top distraction. A session that runs past midnight counts toward the day it started.

## Focus session history

Every monitoring session is saved to a local SQLite database at `%LOCALAPPDATA%\MyClock\focus.db`. A session starts when monitoring begins for a target and ends when you select another target, use **Reset**, or close MyClock. Each session records:

| Column | Meaning |
| --- | --- |
| `window_title` | Title of the target window when the session started. |
| `process_name` | Executable name of the target, such as `devenv`; empty if it could not be read. |
| `started_at`, `ended_at` | Session start and end as UTC timestamps. `ended_at` is empty if MyClock did not exit normally. |
| `monitored_ms` | Time spent monitoring, including time other windows were in the foreground. |
| `foreground_ms` | Time the target was in the foreground. `foreground_ms / monitored_ms` is the foreground rate. |
| `switch_count` | Number of switches away from the target, as shown in **Switches**. |

Each switch is also saved in detail, so later summaries can show where time away from the target went:

- `focus_switches` — one row per switch, linked to its session: `left_at` (when focus reached another application), `returned_at` (when it came back to the target; empty if the session ended first), and `away_ms` (monitored time away, excluding time while paused).
- `focus_switch_visits` — one row per window visited during a switch, in order: `window_title`, `process_name`, `started_at`, and `duration_ms`. Time spent in MyClock itself or with no foreground window counts toward `away_ms` but not toward any visit.

Window titles of visited windows are stored only in this local file. Sessions recorded before switch details were added keep their `switch_count` but have no `focus_switches` rows.

Progress is saved every 30 seconds while a session is open. **Reset** clears the values on screen but keeps the saved history.

MyClock keeps 7 days of history by default: today and the six days before it. The number of days can be changed in **Settings**. Older sessions, with their switches and visits, are deleted when MyClock starts, each time a new day begins, and when the setting is saved. The session being recorded is never deleted.

## Clock

The **Clock** page displays the local system time and updates once per second. By default it uses the 24-hour `HH:mm:ss` format; the 12-hour format and hiding seconds can be chosen in **Settings**.

## Settings

Changes on the **Settings** page are checked as you type and apply when you select **Save**. **Restore defaults** fills in the default values, which also apply only after **Save**. Settings are stored in `%LOCALAPPDATA%\MyClock\settings.json`; if the file is missing or damaged, the defaults are used.

| Section | Setting | Default |
| --- | --- | --- |
| Focus rating | Lowest foreground rate for `LOCKED IN`, `IN THE ZONE`, and `HALF HERE`. Each must be lower than the one above it. | 85%, 65%, 40% |
| Focus rating | Shortest session that gets a verdict; shorter sessions are `WARMING UP`. | 60 seconds |
| Display | Language of the summary's comments and tips: English or Chinese. Verdict names and other labels stay in English. | English |
| Display | 24-hour clock and showing seconds on the **Clock** page. | Both on |
| Ignored apps | Applications, by process name, that do not count as switches, such as a music player. Pick a running application or type a name such as `Spotify`. Time spent in them still counts as time away from the target. | None |
| History | Days of focus history to keep, including today (1 to 365). Lowering it asks for confirmation, because older sessions are deleted when you save. | 7 days |

The **History** section also has two actions that take effect immediately:

- **Open data folder** opens File Explorer with `focus.db` selected.
- **Clear history…** asks for confirmation, then deletes all saved sessions, switches, and visits. A session being recorded at that moment is kept.

## Technical Overview

MyClock uses WPF and XAML for the desktop interface, MVVM for presentation state, and dependency injection for application services.

Window monitoring uses Windows APIs to enumerate visible windows and identify the current foreground window. Timing uses `System.Diagnostics.Stopwatch`, which helps prevent UI refresh delays from affecting measured duration.

## Project Structure

- `Models` — data models and monitor states
- `Services` — clock, stopwatch, and Windows window-tracking logic
- `ViewModels` — bindable state and commands
- `Views` — WPF pages
- `Commands` — command implementations
- `Resources` — shared visual styles and theme resources
