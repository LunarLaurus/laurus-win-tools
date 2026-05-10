# Program Hider

`Program Hider` is a Windows tray utility for hiding open application windows away from the taskbar and restoring them from a single tray menu.

`v0.0.2` is the current `.NET` implementation under `app/ProgramHider`. The earlier Rust prototype is preserved under `archive/legacy-rust-v0.0.1`.

## Features

- Tray icon with menu-driven restore flow
- `Ctrl+Shift+H` global hotkey to hide the active window
- `Hide window` submenu listing visible top-level windows
- One-click restore entries for hidden windows
- `Restore all` on demand and automatically on app exit

## Build

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The packaged single-file executable is written to `release\v0.0.2\ProgramHider.exe`.

## Layout

- `app/ProgramHider`: current WinForms application
- `archive/legacy-rust-v0.0.1`: preserved Rust prototype
- `fix-tooling-path.ps1`: machine PATH baseline helper
- `verify-tooling-path.ps1`: command-resolution verifier
