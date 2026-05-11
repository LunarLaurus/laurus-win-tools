# .NET 8 / C# Migration Plan

## Why migrate this repo

`sound-tracker` is a small Windows-only tray utility. Its original Rust code has now been preserved under `archive/rust-legacy/src/main.rs` and depended directly on Win32/Core Audio COM APIs through the `windows` crate. That kept the binary small, but it also concentrated a lot of low-level interop in one file. In the final Rust checkout, `cargo check` was already failing against the `windows = 0.58.0` API surface, so the move to C# was a simplification rather than an unnecessary rewrite.

## Recommended target stack

Use:

- `.NET 8`
- `C# 12`
- `WinForms` for the tray application shell
- direct Core Audio COM interop isolated in `SoundTracker.App/Audio`

This app does not need a full desktop windowing framework. `NotifyIcon`, `ApplicationContext`, and `ContextMenuStrip` are a better fit than WPF for a tray-first utility with no primary window.

## Proposed project shape

The repo now uses this layout:

```powershell
dotnet new sln -n SoundTracker
dotnet new winforms -f net8.0 -n SoundTracker.App
dotnet sln add .\SoundTracker.App\SoundTracker.App.csproj
```

Suggested structure:

```text
docs/
  dotnet8-migration.md
archive/
  rust-legacy/
SoundTracker.App/
  Program.cs
  TrayApplicationContext.cs
  TooltipFormatter.cs
  Audio/
    AudioSessionPoller.cs
    CoreAudioInterop.cs
  Processes/
    ProcessNameResolver.cs
```

## Rust-to-C# mapping

Map the current responsibilities as follows:

- `main()` -> `Program.cs` plus `TrayApplicationContext`
- `create_tray_icon()` -> `NotifyIcon` initialization
- `SetTimer(..., 1000, ...)` -> replaced with Core Audio session and endpoint event callbacks
- `window_proc()` right-click exit behavior -> `ContextMenuStrip` with `Exit`
- `get_recent_audio_sessions()` -> `AudioSessionPoller.GetActiveSessionNames()`
- `get_process_name()` -> `Process.GetProcessById(id).ProcessName`
- `build.rs` icon generation -> replaced by a standard system tray icon

The C# version should stop modeling a hidden message-only window explicitly unless a WinForms limitation forces it. Let the framework own the message loop.

## Migration phases

### Phase 1: functional parity

Build the smallest working WinForms tray app:

- tray icon appears on startup
- tooltip updates when session or endpoint events fire
- active audio sessions are enumerated
- right-click offers an `Exit` action

Do not optimize beyond parity in this phase.

### Phase 2: structure and cleanup

After parity:

- move audio polling out of UI code
- deduplicate process names before rendering tooltip text
- add cancellation/disposal around timers and enumerators
- replace ad hoc string building with a small tooltip formatter

### Phase 3: packaging

Publish as a Windows desktop binary:

```powershell
dotnet publish .\SoundTracker.App\SoundTracker.App.csproj -c Release -r win-x64 --self-contained false
```

If a single-file deployment is preferred later:

```powershell
dotnet publish .\SoundTracker.App\SoundTracker.App.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true
```

## Implementation notes

- Prefer `Application.Run(new TrayApplicationContext())` instead of a visible form.
- Keep the callback-driven audio monitor isolated from the WinForms tray shell.
- Cap tooltip text aggressively; tray tooltips are short and inconsistent across Windows shells.
- Store the icon as a normal asset. Recreating `build.rs`-style dynamic icon generation in C# adds complexity without value.
- Keep the manual COM definitions inside `CoreAudioInterop.cs` so they do not bleed into UI code.

## Suggested validation

Before removing Rust, verify:

1. app starts with no visible main window
2. tray icon persists after explorer refresh/restart
3. tooltip updates when audio starts and stops
4. duplicate sessions do not flood the tooltip
5. exit disposes the icon cleanly and leaves no orphan process

## Cutover strategy

The Rust crate was kept in place until the C# app reached a usable baseline. After validation, the repo was cut over by moving the old source into `archive/rust-legacy/`. Any future cleanup should:

- add a short `README` note pointing contributors to the .NET project
- leave `archive/rust-legacy/` as a read-only historical snapshot
- replace Rust-focused CI/build steps with `dotnet build` and `dotnet publish`

This repo is small enough that an incremental dual-track migration should be brief; the main risk is not scale, but preserving tray behavior and audio-session polling accuracy during the switch.
