# SoundTracker

SoundTracker is a Windows 10 tray app built on `.NET 8` and `WinForms`. It records historical audio activity instead of only showing a live session snapshot: when a session starts, stops, or when the default render device changes, the app keeps that event in memory and appends it to `%LOCALAPPDATA%\SoundTracker\history\audio-activity.jsonl`.

The tray icon also tracks the default endpoint master volume and mute state, uses a theme-aware dynamically drawn icon, and can act as a usable replacement for the standard Windows speaker icon when the shell mixer is unreliable.

The previous Rust implementation has been archived under [`archive/rust-legacy/`](archive/rust-legacy) so the migration history remains in-repo without competing with the active app layout.

## Build

```powershell
.\build.ps1
```

## Run

```powershell
dotnet run --project .\SoundTracker.App\SoundTracker.App.csproj
```

Use the tray menu or double-click the tray icon to open the Recent Activity window.

## Smoke Tests

```powershell
.\build.ps1
```

If the app or smoke runner is still open from a previous run:

```powershell
.\build.ps1 -StopRunningProcesses
```

The smoke suite uses real interactions: it generates actual audio playback, waits for Core Audio callbacks, verifies JSONL history writes, and renders a real Recent Activity window screenshot during the run.
