# BatteryTray

A configurable Windows system-tray battery indicator. Replaces the default
Windows power icon with a custom one that shows the battery percentage as text,
color-coded by charge level, with toast notifications, WMI-backed health
diagnostics, and silent elevated auto-start at login.

## Features

- **Numeric tray icon** — actual battery % rendered into the 32×32 icon
- **Three icon styles** — `Numeric`, `Bar` (battery shape with fill), or `Both`
- **Color-coded by level** — charging / normal / low / critical, hex-configurable
- **Action Center toasts** with balloon-tip fallback on older Windows
- **Battery health** via WMI: design vs. full-charge capacity, chemistry, cycle count
- **Silent elevated auto-start** via Task Scheduler (no UAC at every login)
- **Configurable thresholds** for low and critical battery
- **Tooltip** with time remaining when on battery
- **Single-instance** enforced via session-scoped mutex
- **Settings JSON** persisted at `%AppData%\BatteryTray\settings.json`

## Quick start

1. Drop `BatteryTray.exe` anywhere durable (e.g. `C:\Tools\BatteryTray\`).
   *Avoid `Downloads` or `Desktop` — the scheduled task will hard-code the path.*
2. Double-click to launch. The new tray icon appears.
3. Right-click → **Settings…**, tick **Run at Windows startup (elevated)**, click Save.
4. Click **Yes** on the single UAC prompt. From now on it launches elevated at
   every login with no further prompts.
5. Right-click → **Hide default Windows battery icon…** to remove the duplicate.

## Hiding the default Windows battery icon

Windows doesn't expose an API to hide the system battery icon (it's owned by the
shell), so the tray menu opens **Settings → Personalization → Taskbar** for you:

1. Expand **Other system tray icons**
2. Toggle **Power** off

## How elevated auto-start works

The "Run at Windows startup" toggle does **not** use `HKCU\…\Run`. That key
cannot launch elevated processes — Windows refuses to auto-elevate from autorun
for security reasons. Instead, BatteryTray creates a **scheduled task**:

- Trigger: `LogonTrigger` for the current user's SID
- Principal: current user, `LogonType=InteractiveToken`, `RunLevel=HighestAvailable`
- Settings: `DisallowStartIfOnBatteries=false`, `StopIfGoingOnBatteries=false`,
  `ExecutionTimeLimit=PT0S`, `MultipleInstancesPolicy=IgnoreNew`

Toggling the setting re-launches BatteryTray with the `runas` verb to obtain a
single UAC consent. The elevated child process then calls `schtasks.exe /Create
/XML` to install the task. Once the task exists, Windows handles elevation at
every subsequent logon silently — the task's authorization is established at
create time, not re-verified at run time.

If a non-admin user enables auto-start, the UAC prompt asks for an admin's
credentials. The original (non-admin) user's SID is passed through to the
elevated helper as a command-line argument, so the task is correctly created to
run at that user's logon — not the admin's. With `RunLevel=HighestAvailable`,
the task runs at the user's normal level (since they aren't an admin), which is
the most we can safely do without giving non-admins persistent elevation.

If you move `BatteryTray.exe` after enabling auto-start, the task will break.
Re-toggle the setting to re-create it with the new path.

### Diagnostic log

If the task install ever fails, stderr from `schtasks.exe` is written to
`%TEMP%\BatteryTray-startup.log`.

### Manually verify or remove the task

```cmd
schtasks /Query /TN "BatteryTray" /V /FO LIST
schtasks /Delete /TN "BatteryTray" /F
```

## Build

Targets `net8.0-windows10.0.19041.0` (for toast notifications). Cross-compile
from Linux works because of `<EnableWindowsTargeting>true</EnableWindowsTargeting>`.

```bash
# Framework-dependent single-file (~25 MB, requires .NET 8 Desktop Runtime on target)
dotnet publish -c Release -r win-x64 \
  --self-contained false \
  /p:PublishSingleFile=true \
  /p:IncludeNativeLibrariesForSelfExtract=true

# Self-contained (~150 MB, no runtime dependency)
dotnet publish -c Release -r win-x64 \
  --self-contained true \
  /p:PublishSingleFile=true \
  /p:IncludeNativeLibrariesForSelfExtract=true
```

Output: `bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/BatteryTray.exe`.

## Settings file

```json
{
  "UpdateIntervalSeconds": 5,
  "LowBatteryThreshold": 20,
  "CriticalBatteryThreshold": 10,
  "NotifyOnLow": true,
  "NotifyOnCritical": true,
  "NotifyOnFullyCharged": true,
  "Style": 0,
  "ShowTimeRemainingInTooltip": true,
  "RunAtStartup": false,
  "ColorCharging": "#1E88E5",
  "ColorNormal":   "#43A047",
  "ColorLow":      "#FB8C00",
  "ColorCritical": "#E53935",
  "ColorText":     "#FFFFFF"
}
```

`Style`: `0` = Numeric, `1` = Bar, `2` = Both.

`RunAtStartup` is informational; the actual state lives in Task Scheduler. The
Settings dialog reads from Task Scheduler directly so the UI doesn't drift if
you delete the task manually.

## Architecture

| File | Role |
|---|---|
| `Program.cs` | Entry point. Handles `--install-task` / `--uninstall-task` flags as elevated-helper invocations |
| `BatteryTrayContext.cs` | `ApplicationContext` — owns `NotifyIcon`, polling timer, notification state machine |
| `BatteryMonitor.cs` | Wraps `SystemInformation.PowerStatus` into a `BatteryState` record |
| `IconRenderer.cs` | Renders state into an `Icon` via GDI+. Owns the HICON lifecycle (`DestroyIcon`) |
| `Notifier.cs` | Action Center toast → balloon-tip fallback |
| `BatteryHealthReader.cs` | WMI queries: `Win32_Battery`, `BatteryStaticData`, `BatteryFullChargedCapacity`, `BatteryCycleCount` |
| `BatteryInfoForm.cs` | Battery health diagnostic dialog |
| `AppSettings.cs` | JSON-backed settings in `%AppData%\BatteryTray\` |
| `SettingsForm.cs` | WinForms settings UI, built programmatically |
| `StartupManager.cs` | Task Scheduler integration with self-elevation |
| `app.ico` | Multi-size icon (16/24/32/48/64/128/256) embedded via `<ApplicationIcon>` |

## Design notes

- **Manifest stays `asInvoker`** (the SDK's implicit default). If we set
  `requireAdministrator`, every manual launch — including double-clicking the
  EXE — would prompt UAC. Auto-elevation is delegated entirely to the
  scheduled task, which is the only path that runs without prompting.
- **Icon handle lifecycle.** `Bitmap.GetHicon()` allocates a native HICON that
  GDI does not free automatically. `IconRenderer.Free` calls `DestroyIcon` on
  the old icon after the new one is assigned to `NotifyIcon.Icon`, keeping the
  per-process user-object handle count flat over long runs.
- **Notification debouncing.** Threshold notifications fire only on transitions
  *down through* the threshold while on battery. The "fully charged" toast
  latches and resets when the battery dips below 95%.
- **Toast registration.** `Microsoft.Toolkit.Uwp.Notifications`'s
  `ToastNotificationManagerCompat` auto-creates an AUMID, COM activator, and
  Start-menu shortcut on first call. We probe by touching `.History` at
  startup; if anything throws (older Windows, broken AppX cache), we fall back
  to legacy balloon tips silently.
- **Defensive polling.** `Refresh` swallows exceptions from
  `BatteryMonitor.Read` rather than letting transient driver glitches tear
  down the tray.
- **Mutex scope.** `Local\` prefix keeps the single-instance check
  per-session, so two users on the same machine each get their own instance.
  *Caveat:* if the elevated task-launched instance and a manual user-launched
  instance run in the same session, the integrity-level mismatch may let both
  start (named-mutex DACL doesn't cross IL boundaries by default). In
  practice this only happens if you double-launch on top of the auto-started
  instance.

## Possible next steps

- Manual mutex security descriptor allowing low-IL access, to plug the
  cross-IL single-instance gap
- WinUI 3 / Windows App SDK migration for native modern toast features
  (progress bars in toasts, etc.)
- Charge history graph in the Battery Info dialog
- Power-plan switching (e.g. "switch to Battery Saver below X%") — would
  actually use the elevation we now have
