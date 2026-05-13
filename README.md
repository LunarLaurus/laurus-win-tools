# windows-apps

Monorepo for a small fleet of Windows tray utilities.

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

## Install

Pre-built framework-dependent binaries are attached to each GitHub Release at
[Releases](https://github.com/LunarLaurus/laurus-win-tools/releases). The .NET 8
Windows Desktop Runtime is required to run them — install it from
[dot.net/download](https://dot.net/download) if it's not already present.

`install.ps1` at the repo root automates publish + install to
`%LOCALAPPDATA%\LaurusWinTools\<AppName>` from a source checkout:

```powershell
.\install.ps1 -AutoRun -Run    # install all four, register for logon, launch now
.\install.ps1 -AutoRun         # install + register for logon (apps start on next sign-in)
.\install.ps1                  # install only
.\install.ps1 -Uninstall       # remove all four
```

`-AutoRun` writes to HKCU `Run` so the apps launch at logon (no elevation required).
`-Run` launches each installed app immediately so you don't have to sign out
to see them in the tray. After install, `run-all.ps1` starts any of the four
that aren't already running — handy for relaunching without re-publishing.

## Build from source

Requires .NET 8 SDK and Windows.

```
dotnet build apps/<AppName>
dotnet test  shared/<Lib>.Tests
dotnet test  apps/<AppName>.Tests   (or apps/<AppName>/tests/<AppName>.Tests)
```

## Conventions

See `docs/conventions/` for authoritative decisions on app data paths, startup registration, tray shell ownership, and JSON / logging format.

## Auto-update

Each app polls the GitHub releases API at startup and every 24 hours; a tray
balloon notification surfaces when a newer release is available.

## License

[PolyForm Noncommercial License 1.0.0](LICENSE) — free for noncommercial use
(personal, hobby, academic, charitable, government). Commercial use requires
a separate commercial license; open a GitHub issue if you want to discuss one.
