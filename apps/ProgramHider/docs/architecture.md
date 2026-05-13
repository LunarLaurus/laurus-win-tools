# Architecture Overview

## Purpose

Program Hider is a Windows tray-first WinForms application that hides normal top-level application windows, tracks them internally, and restores them on demand. The app is intentionally local, lightweight, and built around native Win32 window control rather than process injection or shell extension hooks.

## Main Components

### Application Shell

- `Program.cs` starts the app and parses startup/elevation handoff arguments.
- `ProgramHiderContext.cs` owns the tray icon, menus, global hotkey, WinEvent hooks, restore flows, notifications, and shutdown behavior.
- `HotkeyMessageWindow.cs` provides a hidden window for `WM_HOTKEY` delivery.

### Window Management

- `NativeMethods.cs` contains Win32 interop for enumeration, visibility changes, placement, monitors, hotkeys, and event hooks.
- `IWindowPlatform.cs` abstracts the minimum window-control surface needed by the runtime and tests.
- `WindowCatalog.cs` decides which windows are manageable.
- `WindowHideService.cs` performs actual hide/restore/prune behavior.
- `ActiveWindowTracker.cs` preserves the last valid foreground window so tray focus changes do not lose the user’s intended target.

### Rules, Settings, and Security

- `AppSettings.cs`, `SettingsStore.cs`, and `SettingsForm.cs` control persisted configuration.
- `WindowRule.cs` and `WindowRuleMatchResult` model auto-hide/security behavior.
- `PinSecurity.cs` and `PinPromptForm.cs` implement restore authorization.
- `StartupRegistration.cs` manages the current-user Run key.
- `ElevationService.cs` handles admin relaunch and retry for elevated targets.

### Testing and Verification

- `tests/ProgramHider.TestHost` contains deterministic unit/integration checks.
- `tests/ProgramHider.SmokeWindow` is the repo-local smoke target.
- `tools/verify-release.ps1` and `build.ps1` enforce the verification-first release flow.

## State Model

Runtime state is centered on:

- `_hiddenWindows`: current hidden window inventory
- `%APPDATA%\ProgramHider\settings.json`: persisted settings
- `%APPDATA%\ProgramHider\logs`: structured JSONL diagnostics

## Design Constraints

- Only normal top-level desktop windows are considered manageable.
- Elevated windows may require Program Hider itself to relaunch elevated.
- Secure desktop, shell-critical surfaces, and other protected UI remain out of scope by design.
