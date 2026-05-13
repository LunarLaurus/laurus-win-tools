# Testing

## Unit And Integration Harness

Run the repo-local test host:

```powershell
D:\tooling\dotnet\dotnet.exe run --project tests\ProgramHider.TestHost\ProgramHider.TestHost.csproj -c Release
```

Run the full verification suite:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\verify-release.ps1
```

Current coverage includes:

- rule matching
- merged rule behavior
- settings normalization and legacy-rule migration
- rule-protected PIN preservation
- settings-path override handling
- startup option parsing
- elevated relaunch argument composition
- PIN hashing/verification
- hotkey normalization
- window lookup with process filtering
- manageable-window filtering
- explicit foreground-handle tracking
- active-window tracking fallback behavior
- hide/restore/prune behavior against a fake window platform
- hide guard rails for excluded/duplicate windows

## Live Smoke Test

Build the solution first:

```powershell
D:\tooling\dotnet\dotnet.exe build ProgramHider.sln -c Release
```

Then run the isolated smoke test:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\smoke-test-sample-window.ps1
```

The smoke test:

- launches `tests/ProgramHider.SmokeWindow`
- waits for the sample window to appear
- verifies lookup through `ProgramHider.TestHost`
- hides the sample window
- verifies it is no longer visible
- restores the sample window
- verifies it is visible again

Run the normal PowerShell smoke test:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\smoke-test-powershell-window.ps1
```

Run the real Program Hider hotkey smoke test:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\smoke-test-program-hider-hotkey.ps1
```

Run the release-startup smoke test:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\smoke-test-release-startup.ps1 -ExePath .\release\v0.1.4\ProgramHider.exe
```

Manual admin-relaunch verification:

- start Program Hider unelevated
- attempt to hide an elevated Administrator PowerShell window
- accept the UAC prompt
- confirm the elevated tray instance appears and the target window hides after relaunch

## Notes

- The PowerShell custom-title target was dropped because it was not a reliable probe on this machine.
- The sample smoke window keeps live verification self-contained and avoids touching system or user windows.
- The hotkey smoke uses `PROGRAM_HIDER_SETTINGS_PATH` to force a deterministic `Ctrl+Shift+H` binding without touching the real user config.
- The hotkey smoke now drives Program Hider's real `WM_HOTKEY` handler through a repo-local message-window probe instead of relying on brittle shell focus tricks.
- Normal `powershell.exe` windows were verified to hide and restore correctly.
- Administrator/elevated PowerShell windows still require Program Hider to run elevated too; `v0.1.4` now offers an in-app relaunch path when that boundary is hit.
- Some system-protected or shell-critical windows are still intentionally not manageable; the elevation path is for elevated user apps, not literally every window on the desktop.
- See [window-compatibility.md](D:/code/program-hider/docs/window-compatibility.md) for the explicit support matrix and architectural limits.
