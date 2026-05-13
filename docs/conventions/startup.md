# Startup Registration

Each app in the suite uses one of two startup backends. The choice is fixed by whether the app requires elevation — it is not a user preference.

## Backend Selection

| App | Backend | Reason |
|---|---|---|
| NetProfileSwitcher | Task Scheduler (`ScheduledTaskStartupRegistration`) | Requires elevation to write network profiles |
| BatteryTray | Task Scheduler (`ScheduledTaskStartupRegistration`) | Requires elevation for ETW power sampling |
| ProgramHider | Registry Run key (`RunKeyStartupRegistration`) | User-level; elevation requested on-demand when needed |
| SoundTracker | Registry Run key (`RunKeyStartupRegistration`) | User-level; no elevation required |

## Interface

All startup registration goes through `IStartupRegistration`:

```csharp
public interface IStartupRegistration
{
    bool GetRunAtStartup();
    StartupResult Register();
    StartupResult Unregister();
}
```

App code only sees `IStartupRegistration`. The backend is injected at startup — apps do not reference `RunKeyStartupRegistration` or `ScheduledTaskStartupRegistration` directly in their domain logic.

## Run Key Backend

`RunKeyStartupRegistration` writes to `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. No elevation required. Supports `--startup` and `--delay=N` flags via `StartupOptions`.

## Scheduled Task Backend

`ScheduledTaskStartupRegistration` drives `schtasks.exe`. It is more complex than a simple register/unregister because it must handle:

- **Elevation** — the task runs at highest available privilege; the registration itself may require a self-relaunch with elevation
- **Self-install contract** — the app must support `--install-task` and `--uninstall-task` CLI flags so the elevated child process can complete installation
- **User SID capture** — the SID must be captured before elevation, in the original user context
- **Tri-state result** — `Success`, `Failed`, `UserCancelled` (user dismissed the UAC prompt)
- **Legacy cleanup** — removes any stale Run key entries left by older versions

BatteryTray's `StartupManager` is the reference implementation. Its extraction into `ScheduledTaskStartupRegistration` is the most disruptive migration in Phase 4 and should be planned as its own sub-phase, not treated as a drop-in backend swap.

## Startup Flags

Suite-wide startup flags (parsed by `StartupOptions` in `WindowsAppCore`):

| Flag | Meaning |
|---|---|
| `--startup` | Process was launched at startup (affects UI behaviour, e.g. suppressing splash) |
| `--delay=N` | Wait N seconds before showing UI (reduces startup contention) |

App-specific flags (`--safe-mode` for ProgramHider, `--rehide` for ProgramHider) are parsed by those apps and never passed to `StartupOptions`.
