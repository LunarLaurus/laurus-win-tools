# SoundTracker

SoundTracker is a Windows tray utility built on `.NET 8` and `WinForms`. The active app lives in [`SoundTracker.App/`](SoundTracker.App), targets Windows 10 (`10.0.19041.0`) and newer, and uses Core Audio session and endpoint callbacks to update the tray state without timer-based polling.

The previous Rust implementation has been archived under [`archive/rust-legacy/`](archive/rust-legacy) so the migration history remains in-repo without competing with the active app layout.

## Build

```powershell
.\build.ps1
```

## Run

```powershell
dotnet run --project .\SoundTracker.App\SoundTracker.App.csproj
```

## Smoke Tests

```powershell
.\build.ps1
```

If the app or smoke runner is still open from a previous run:

```powershell
.\build.ps1 -StopRunningProcesses
```
