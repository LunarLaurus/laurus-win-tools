# App Data Paths

All apps in the suite follow this storage layout. `{AppName}` is the PascalCase app identifier (e.g. `BatteryTray`, `SoundTracker`).

## Path Table

| Data type | Path |
|---|---|
| Settings | `%APPDATA%\{AppName}\settings.json` |
| Logs | `%LOCALAPPDATA%\{AppName}\logs\{appname}-YYYYMMDD.jsonl` |
| History / audit | `%LOCALAPPDATA%\{AppName}\history\` |
| Crash fallback | `%TEMP%\{AppName}-crash.log` |
| Broken settings quarantine | `%APPDATA%\{AppName}\settings.json.broken-{timestamp}` |

## Rationale

**`%APPDATA%` for settings** — roaming profile data; small, user-specific configuration that should follow the user across machines.

**`%LOCALAPPDATA%` for logs and history** — machine-local data; logs are large and machine-specific (device names, hardware events). They must not roam.

**`%TEMP%` for crash logs** — written synchronously during a crash before normal paths may be available. `%TEMP%` is always writable. This is a last-resort path, not the primary log destination.

## Resolution via `AppPaths`

All path construction goes through `AppPaths`. Never construct `%APPDATA%` or `%LOCALAPPDATA%` paths by string concatenation in app code.

```csharp
// Correct
var settingsPath = AppPaths.Settings(appName);
var logDir      = AppPaths.LogDirectory(appName);

// Wrong — bypasses test override and naming convention
var settingsPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    appName, "settings.json");
```

## Test Override

`AppPaths` reads `{APPNAME}_DATA` environment variable if set (e.g. `BATTERYTRAY_DATA`). `TempAppData` (from `WindowsAppTesting`) sets this automatically and cleans up on disposal — test projects never touch the real `%APPDATA%` directory.

## Broken Settings Quarantine

When `JsonSettingsStore<T>` encounters a deserialisation failure it renames the broken file to `settings.json.broken-{timestamp}` in the same directory before returning `new T()`. This preserves the broken file for diagnostics without blocking startup. App code never handles this — the store does.
