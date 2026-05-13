# windows-apps

Private monorepo for Windows tray utilities.

## Apps

| App | Description |
|-----|-------------|
| `apps/BatteryTray` | System-tray battery monitor with live power-draw sampling (ETW / Performance Counters / WMI) |
| `apps/NetProfileSwitcher` | Switches Windows network profiles and applies NPS policy from the system tray |
| `apps/ProgramHider` | Hides and restores windows on hotkey; supports auto-hide on minimize and PIN protection |
| `apps/SoundTracker` | Tracks per-app audio activity and logs it to JSONL for later review |

## Shared libraries

| Library | Description |
|---------|-------------|
| `shared/WindowsAppCore` | Settings store, structured logging, crash sink, startup registration, app paths |
| `shared/WindowsTrayCore` | Tray icon management, theme detection, `UiDispatcher` |
| `shared/WindowsAppTesting` | xUnit test helpers: `TempAppData`, `FakeClock`, `WindowsFactAttribute`, fakes |

## Build

Requires .NET 8 SDK and Windows.

```
dotnet build apps/<AppName>
dotnet test  shared/<Lib>.Tests
dotnet test  apps/<AppName>.Tests   (or apps/<AppName>/tests/<AppName>.Tests)
```

## Conventions

See `docs/conventions/` for authoritative decisions on app data paths, startup registration, tray shell ownership, and JSON / logging format.
