# SoundTracker

SoundTracker is a Windows tray utility built on `.NET 8` and `WinForms`. The active app lives in [`SoundTracker.App/`](SoundTracker.App) and polls Core Audio sessions to show recent active audio processes from the system tray.

The previous Rust implementation has been archived under [`archive/rust-legacy/`](archive/rust-legacy) so the migration history remains in-repo without competing with the active app layout.

## Build

```powershell
dotnet build SoundTracker.sln -c Release
```

## Run

```powershell
dotnet run --project .\SoundTracker.App\SoundTracker.App.csproj
```
