# JSON and Logging

## Settings — `JsonSettingsStore<T>`

### File Safety (Non-Negotiable)

`JsonSettingsStore<T>` owns three file-safety behaviours that apply to every app without exception:

| Behaviour | Implementation |
|---|---|
| Atomic saves | Write to temp file, then `File.Move(overwrite: true)` — never direct overwrite |
| Corruption quarantine | On deserialisation failure: rename broken file to `settings.json.broken-{timestamp}`, return `new T()` |
| Schema migration | `ISettingsMigration` chain applied in order on load |

### Serialiser Policy

`JsonSettingsStore<T>` does **not** impose a global serialiser policy. File-safety is shared; casing and compatibility choices are per-store, app-owned.

**Shared minimum (all stores):**
- `WriteIndented = true` — settings files are human-readable on disk

**Per-store, app-owned:**
- `PropertyNamingPolicy` — apps choose; camelCase recommended for new stores, but not forced
- `JsonStringEnumConverter` — opt-in; appropriate for most settings
- `PropertyNameCaseInsensitive` — apps decide; changing this is a compatibility event requiring a migration

**Why not a global policy:** Each app has settings files in production with different casing conventions. A forced global policy turns a shared-infra refactor into a silent settings migration for all apps simultaneously. Each app manages its own compatibility via `ISettingsMigration`.

### Migration Interface

```csharp
public interface ISettingsMigration
{
    int FromVersion { get; }
    JsonDocument Migrate(JsonDocument raw);
}
```

Migrations are applied in `FromVersion` order on load. The store handles sequencing — app code passes the migration list at construction and does not call migrations directly.

---

## Logging — `AppLog` / `JsonLineWriter`

### JSONL Envelope

All apps write structured logs in this format:

```json
{"ts":"2026-05-13T12:34:56.789Z","app":"sound-tracker","v":"0.4.1","evt":"session.started","data":{}}
```

| Field | Type | Description |
|---|---|---|
| `ts` | ISO 8601 UTC | Timestamp |
| `app` | string | App identifier (kebab-case) |
| `v` | string | App version |
| `evt` | string | Event name (dot-separated, e.g. `session.started`, `profile.applied`) |
| `data` | object | Event-specific payload; empty object `{}` if none |

### `AppLog` API

```csharp
public sealed class AppLog : IDisposable
{
    public AppLog(string appName, string appVersion);
    public void Info(string evt, object? data = null);
    public void Warn(string evt, object? data = null);
    public void Error(string evt, Exception? ex = null, object? data = null);
    public string LogPath { get; }
}
```

### Internals

- `Channel<string>` (unbounded) — log calls never block the caller
- Background drain thread flushes every 500 ms or 50 queued lines
- Daily rollover on UTC date change
- 50 MB size cap per file → rolls to `{name}-YYYYMMDD-1.jsonl`
- Files older than 30 days pruned on startup
- Drain thread fault → logging silently stops, app continues
- `Dispose()` drains the remaining queue before closing

### Crash Logging — `CrashSink` (Separate Write Path)

`CrashSink` is **not** `AppLog`. It exists because the buffered channel will not flush if the process is killed before `Dispose()` runs.

Fatal crash paths must write **synchronously** to `CrashSink` — never routed through the channel.

```csharp
public static class CrashSink
{
    // Synchronous, direct write to %TEMP%\{AppName}-crash.log
    // Safe to call from AppDomain.UnhandledException and Application.ThreadException handlers
    public static void Write(string appName, Exception ex);
}
```

`UnhandledExceptionWatcher` (`WindowsAppCore`) and `Application.ThreadException` handlers (`WindowsTrayCore`) both call `CrashSink.Write()` — not `AppLog.Error()`.

**BatteryTray migration note:** BatteryTray has a working `CrashLogger`. The migration goal is to align its interface to `CrashSink` — the synchronous direct-write behaviour is correct and must be preserved. Do not route BatteryTray's fatal crash path through `AppLog`.

### Startup Wiring Order

```csharp
var log = new AppLog(appName, version);
UnhandledExceptionWatcher.Install(log, appName);   // WindowsAppCore — before anything else
// ... build tray icon ...
IUserNotifier notifier = new ToastNotifier(trayIcon, log);   // or BalloonNotifier
notifier.InstallThreadExceptionHandler();                     // WindowsTrayCore — after tray ready
Application.Run(context);
```

`UnhandledExceptionWatcher` is installed first so any startup crash is captured. The `Application.ThreadException` handler is installed after the tray is ready because it needs `IUserNotifier` to be constructed.
