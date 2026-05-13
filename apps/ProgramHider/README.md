# Program Hider

`Program Hider` is a Windows tray utility for hiding open application windows away from the taskbar and restoring them from a single tray menu.

`v0.1.5` is the current `.NET` implementation under `app/ProgramHider`. The earlier Rust prototype is preserved under `archive/legacy-rust-v0.0.1`.

## Features

- Tray icon with menu-driven restore flow
- Branded executable and tray icon
- `Ctrl+Shift+H` global hotkey to hide the active window
- Persistent settings stored under `%APPDATA%\ProgramHider\settings.json`
- Configurable hotkey with a fuller built-in settings dialog
- Structured window rules:
  - match by process name
  - match by title substring
  - match by class name
  - per-rule auto-hide, restore PIN, and quiet-mode behavior
- Active-window inspector with one-click rule creation
- Rule import/export as JSON
- Optional restore PIN/password protection, off by default
- Optional separate PIN/password for `Restore all` and other bulk restores
- Unlock timeout cache for repeated restores
- Launch-on-Windows-start option with configurable startup delay
- Safe mode toggle to suspend automation without closing the app
- Searchable restore browser with a recently hidden section
- Hidden windows grouped by process in the tray restore menu
- `Hide window` submenu listing visible top-level windows
- Optional restore-without-focus behavior
- Placement and monitor capture to improve restore correctness
- Structured JSONL logs under `%APPDATA%\ProgramHider\logs`
- Watchdog pruning for dead handles
- Optional restore-on-session-lock / restore-on-suspend safety behavior
- Administrator relaunch and retry when a hide is blocked by elevation boundaries
- `Restore all` on demand and automatically on app exit

## Quick Start

1. Launch `ProgramHider.exe`.
2. Focus the window you want to hide.
3. Press `Ctrl+Shift+H`, or use the tray menu.
4. Restore the window from the tray menu or `Restore browser...`.

If a target app is running as administrator, Program Hider can relaunch itself elevated and retry.

For step-by-step usage, see [docs/user-guide.md](D:/code/program-hider/docs/user-guide.md).

## Build

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

By default, `build.ps1` now:

- builds the app
- runs the repo-local verification suite
- runs live smoke tests
- publishes the release artifact
- runs a release-startup smoke check
- writes release docs alongside the exe

If you need to bypass part of that flow, the main switches are:

- `-SkipVerification`
- `-SkipLiveSmoke`
- `-SkipStartupSmoke`
- `-SkipSigning`

The packaged single-file executable is written to `release\v0.1.5\ProgramHider.exe`.

The portable zip is written to `release\ProgramHider-v0.1.5-portable.zip`.

If you want to sign release builds, set:

- `PROGRAM_HIDER_SIGNTOOL`
- `PROGRAM_HIDER_PFX_PATH`
- `PROGRAM_HIDER_PFX_PASSWORD`
- optional: `PROGRAM_HIDER_TIMESTAMP_URL`

Then `build.ps1` will invoke `tools\sign-release.ps1` automatically.

## Testing

Run the repo-local verification harness:

```powershell
D:\tooling\dotnet\dotnet.exe run --project tests\ProgramHider.TestHost\ProgramHider.TestHost.csproj -c Release
```

Run the isolated live smoke test:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\smoke-test-sample-window.ps1
```

The smoke test uses a repo-local sample window rather than a system app, so it does not need to manipulate your real working windows.

Run the normal PowerShell smoke test:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\smoke-test-powershell-window.ps1
```

Run the real Program Hider hotkey smoke test:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\smoke-test-program-hider-hotkey.ps1
```

The hotkey smoke uses an isolated temporary settings file, so it does not depend on the machine's real Program Hider hotkey.

Run the full verification suite:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\verify-release.ps1
```

## Layout

- `app/ProgramHider`: current WinForms application
- `archive/legacy-rust-v0.0.1`: preserved Rust prototype
- `docs/design-v0.1.0.md`: roadmap/design record for the `v0.1.0` pass
- `docs/work-log.md`: feature implementation log for this pass
- `docs/CHANGELOG.md`: project changelog
- `docs/architecture.md`: component-level architecture overview
- `docs/runtime-flows.md`: key hide/restore/elevation/release flows
- `docs/user-guide.md`: detailed end-user usage guide
- `docs/testing.md`: test and smoke-test commands
- `docs/window-compatibility.md`: supported, elevation-required, and unsupported window classes
- `fix-tooling-path.ps1`: machine PATH baseline helper
- `verify-tooling-path.ps1`: command-resolution verifier
