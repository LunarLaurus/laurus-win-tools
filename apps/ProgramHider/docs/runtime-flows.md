# Runtime Flows

## Hide Active Window

1. The user presses the global hotkey or chooses `Hide active window`.
2. `ProgramHiderContext` asks `ActiveWindowTracker` for the best current target.
3. `WindowHideService` validates the handle and hides it through `IWindowPlatform`.
4. A `HiddenWindow` record is added to the in-memory hidden set.
5. A structured `window.hidden` log event is written.

Why the tracker exists:

- opening the tray menu steals focus
- some hotkey/tray timing races make `GetForegroundWindow()` alone unreliable

## Auto-Hide On Minimize

1. Win32 sends `EVENT_SYSTEM_MINIMIZESTART`.
2. `ProgramHiderContext` receives it through a WinEvent hook.
3. The matching window snapshot is evaluated against the configured rules.
4. If a rule enables auto-hide, the window is hidden and tracked immediately.

## Restore Flow

1. The user restores from the tray menu or `Restore browser...`.
2. If needed, PIN/password authorization is requested.
3. `WindowHideService` restores placement, show state, and optional foreground focus.
4. The hidden entry is removed from the tracked set.

## Elevation Retry

1. A hide attempt fails for a likely elevated target window.
2. `ElevationService` offers a `runas` relaunch.
3. The new instance starts with a `--rehide=...` handle argument.
4. After a short handoff delay, the elevated instance retries the hide automatically.

This is intended for elevated user apps such as Administrator PowerShell, not for protected system UI.

## Release Verification Flow

1. `build.ps1` reads the project version and builds in `Release`.
2. Unless skipped, `tools/verify-release.ps1` runs:
   - unit/integration harness
   - repo-local smoke window
   - PowerShell smoke
   - hotkey smoke
3. The app is published as a single-file framework-dependent executable.
4. A release-startup smoke test validates the packaged exe directly.
5. Release docs are copied into `release\vX.Y.Z\`.
