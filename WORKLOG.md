# Worklog

Resumability artifact. Read this + `NOTES.md` + `design-vision.md` to get full context before doing anything.

---

## 2026-05-13

**Did:** Full planning and design phase completed across three AI review rounds.
- Audited all five apps (NetProfileSwitcher, program-hider, sound-tracker, BatteryTray, DriveDredge)
- Produced `design-vision.md` — the authoritative design document
- Three rounds of cross-review (cross-review) resolving library split, crash logging, serialiser policy, TrayShell design, repo strategy, BatteryTray API validation
- Established project rules in `NOTES.md`
- `.gitignore` and `WORKLOG.md` created
- Git repo not yet initialised

**Committed:** not yet — no git repo

**Next:** Phase 0 (see checklist below)

---

## 2026-05-13 06:50

**Did:** Phase 0 — workspace restructure complete.
- `git init` at `D:\code\windows-apps\`
- Created `apps\`, `shared\`, `archive\snapshots\`, `docs\conventions\`
- Moved all apps and archive to target layout
- Per-app git histories (NetProfileSwitcher, SoundTracker, ProgramHider) grafted into
  monorepo under `apps\` via `git subtree add` with full history preservation
- BatteryTray added as regular files (no prior standalone repo)
- `.gitignore` extended to exclude private config files by name in addition to prefix patterns

**Committed:** 43bfcf5 (initial structure), 189424f (NPS subtree), 16bb5bd (ST subtree), d2762a7 (PH subtree)

**Next:** Phase 1 — conventions docs

---

## 2026-05-13

**Did:** Phase 1 — conventions docs complete.
- `docs\conventions\app-data.md` — APPDATA vs LOCALAPPDATA rules, AppPaths resolution, test override, quarantine path
- `docs\conventions\startup.md` — backend selection table, IStartupRegistration interface, RunKey vs ScheduledTask detail, startup flags
- `docs\conventions\tray-shell.md` — TrayShell ownership model, host-agnostic design, per-app mapping, full lifecycle
- `docs\conventions\json-and-logging.md` — JsonSettingsStore file-safety, serialiser policy, JSONL envelope, AppLog internals, CrashSink separation, startup wiring order

**Committed:** 7364739

**Next:** Phase 2 — `WindowsAppCore` skeleton + logging

---

## 2026-05-13

**Did:** Phase 2 — `WindowsAppCore` skeleton + logging complete (library only; NPS migration in next commit).
- `shared\WindowsAppCore\WindowsAppCore.csproj` (net8.0-windows, no WinForms)
- `AppPaths` — per-app path resolution with `{APPNAME}_DATA` env override for tests
- `AppIdentity` — app name + version holder
- `JsonLineWriter` — buffered `Channel<string>` drain thread; 500 ms flush, 50-line batch, daily rotation, 50 MB size cap, 30-day pruning
- `AppLog` — structured JSONL logger on top of `JsonLineWriter`; `{ts,app,v,evt,level,data}` envelope
- `CrashSink` — synchronous direct-write to `%TEMP%\{AppName}-crash.log`; never throws
- `UnhandledExceptionWatcher` — wires `AppDomain.CurrentDomain.UnhandledException` to CrashSink
- `shared\WindowsAppCore.Tests\` — 23 unit tests, all pass; tests use explicit log-dir injection (internal ctor via InternalsVisibleTo)

**Committed:** 4c33ffe (library skeleton), see below for NPS migration

**Next:** Phase 3 — `JsonSettingsStore<T>` + settings migration

---

## 2026-05-13

**Did:** Phase 2 — migrate NetProfileSwitcher to use WindowsAppCore logging.
- Added `ProjectReference` to `shared\WindowsAppCore`
- Recovered missing `app.manifest` (requireAdministrator, PerMonitorV2 DPI)
- Wired `AppLog` + `UnhandledExceptionWatcher` in `Program.cs`
- Instrumented: `app.started`, `profile.applied`, `profile.apply.failed`, `profile.autoswitch`, `profile.autoswitch.failed`, `startup.registration.changed`, `startup.registration.failed`, `app.shutdown`

**Committed:** 300e0fb

**Next:** Phase 3 — `JsonSettingsStore<T>` + settings migration

---

## 2026-05-13

**Did:** Phase 3 — `JsonSettingsStore<T>` + settings migration complete.
- `ISettingsMigration` interface and `JsonSettingsStore<T>` in `shared\WindowsAppCore`
- 36 tests in `JsonSettingsStoreTests`: happy path, corrupt quarantine, migration chain, atomic write, normalize delegate
- NetProfileSwitcher: replaced hand-rolled ConfigStore; added `MigrateConfigFileName()` for config.json→settings.json rename
- ProgramHider: replaced bespoke SettingsStore; TFM bumped to versioned windows form; PROGRAMHIDER_DATA replaces old env var
- BatteryTray: extracted v1→v2→v3 migration classes; fixed wrong project reference depth; migration tests rewritten to test classes directly
- SoundTracker: new `SoundTrackerConfig` + `SoundTracker.Tests` project; wired into Program.cs

**Committed:** fbe460b (library), 2399349 (NPS), 8fb18c2 (ProgramHider), 3e4201f (BatteryTray), a5a9352 (SoundTracker)

**Next:** Phase 4 — Single instance + startup (`SingleInstanceActivation`, `IStartupRegistration`)

---

## 2026-05-13

**Did:** Phase 4 — Single instance + startup complete.
- `CrossIntegrityMutex`, `ActivationSignal`, `SingleInstanceActivation`, `ElevationHelper`, `IStartupRegistration`, `RunKeyStartupRegistration`, `ScheduledTaskStartupRegistration` added to WindowsAppCore
- `WindowsAppCore.Tests`: `WindowsFactAttribute`, `ElevationHelperTests`, `RunKeyStartupRegistrationTests`, `ScheduledTaskStartupRegistrationTests`, `SingleInstanceActivationTests` (48 tests total)
- Library committed at d838ce3
- ProgramHider: replaced `StartupRegistration` static class with `RunKeyStartupRegistration`; added `SingleInstanceActivation` (committed ee7e9a1)
- NetProfileSwitcher: replaced `Services/StartupManager` with `ScheduledTaskStartupRegistration`; added `SingleInstanceActivation` (committed 5489fcd)
- BatteryTray: deleted `CrossIntegrityMutex.cs`, `ActivationSignal.cs`, `StartupManager.cs`; replaced manual thread loop with event subscription; wired `ScheduledTaskStartupRegistration` (committed d3a4f49)
- SoundTracker: added `RunAtStartup` to `SoundTrackerConfig`; added `RunKeyStartupRegistration` + "Run at startup" tray menu item (first startup UI for SoundTracker); added `SingleInstanceActivation` (committed 5c1f810)

**Committed:** d838ce3 (library), ee7e9a1 (ProgramHider), 5489fcd (NPS), d3a4f49 (BatteryTray), 5c1f810 (SoundTracker)

**Next:** Phase 5 — Wire `UnhandledExceptionWatcher` into all four apps

---

## 2026-05-13

**Did:** Phase 5 — Wire `UnhandledExceptionWatcher` into all four apps complete.
- Added missing `UnhandledExceptionWatcherTests` to `shared\WindowsAppCore.Tests` (Phase 2 gap; 50 tests total)
- ProgramHider: created `WindowsAppCore.AppLog` instance + `UnhandledExceptionWatcher.Install` in Program.cs (previously had zero crash handling)
- SoundTracker: replaced manual `AppDomain.CurrentDomain.UnhandledException` handler with `UnhandledExceptionWatcher.Install`; type-aliased `CoreAppLog` to avoid collision with `SoundTracker.App.Diagnostics.AppLog`; `Application.ThreadException` kept for Phase 6
- BatteryTray: removed `AppDomain.UnhandledException` from `CrashLogger.Install()` (now owned by `UnhandledExceptionWatcher`); added `WindowsAppCore.AppLog` + `UnhandledExceptionWatcher.Install` in Program.cs; `CrashLogger.Write()` and `GetLogPath()` unchanged
- NetProfileSwitcher: already wired in Phase 2 — no change

**Committed:** 590e20d (library tests), e16141c (ProgramHider), 6cfbfe5 (SoundTracker), dcce8af (BatteryTray)

**Next:** Phase 6 — `WindowsTrayCore` + theme

---

## 2026-05-13

**Did:** Phase 6 — `WindowsTrayCore` shared library + per-app theme fixes complete.
- `shared\WindowsTrayCore`: `TrayTheme` (singleton + Changed event), `UiDispatcher`, `TrayTooltip`, `TrayIconManager`, `ITrayIconProvider`, `IUserNotifier`, `BalloonNotifier`, `ToastNotifier`, `TrayShell`
- `shared\WindowsTrayCore.Tests`: 32 tests across all new types — all pass
- NetProfileSwitcher: `Theme.cs` static readonly Color fields → properties delegating to `TrayTheme.Current`; `MainForm` subscribes `TrayTheme.Current.Changed` and calls `ApplyTheme()` to re-colour at runtime
- `apps\NetProfileSwitcher.Tests`: new project, 6 tests covering delegation and derived colours (Surface2/AccentDim)
- SoundTracker: `RecentActivityForm` hardcoded light BackColor replaced with `TrayTheme.Current.Background`; subscribes to `TrayTheme.Current.Changed` for runtime updates
- 3 new tests added to `SoundTracker.Tests`; 6 total pass
- `WindowsTrayCore/Properties/AssemblyInfo.cs` extended with `InternalsVisibleTo` for both new test projects

**Committed:** see git log (WindowsTrayCore library, NPS fix, SoundTracker fix in separate commits); latest ca575b6 (NPS), 179bb37 (ST)

**Next:** Phase 7 — Icon providers per app, replace manual GDI management, dirty-flag in SoundTracker

---

## Phase Checklist

### Phase 0 — Workspace restructure *(complete)*

No code changes. Directory moves only.

- [x] `git init` at `D:\code\windows-apps\`
- [x] Create `apps\`, `shared\`, `archive\snapshots\`, `docs\conventions\`
- [x] `DriveDredge_v1.13.1\` → `archive\DriveDredge_v1.13.1\`
- [x] BatteryTray canonical source (from inside `BatteryTray\`):
  - [x] `BatteryTray\BatteryTray\`          → `apps\BatteryTray\BatteryTray\`
  - [x] `BatteryTray\BatteryTray.Tests\`    → `apps\BatteryTray\BatteryTray.Tests\`
  - [x] `BatteryTray\BatteryTray.E2ETests\` → `apps\BatteryTray\BatteryTray.E2ETests\`
  - [x] `BatteryTray\BatteryTray.sln`       → `apps\BatteryTray\BatteryTray.sln`
  - [x] Remaining loose files at `BatteryTray\` root → `archive\snapshots\batterytray-import-root\`
  - [x] Remove now-empty `BatteryTray\` import root
- [x] `NetProfileSwitcher\` → `apps\NetProfileSwitcher\`
- [x] `program-hider\`      → `apps\ProgramHider\`
- [x] `sound-tracker\`      → `apps\SoundTracker\`
- [x] Verify `.gitignore` excludes `claude-*` and `codex-*` before committing
- [x] Initial commit with detailed message — no AI attribution
- [x] Update this worklog entry with commit hash

### Phase 1 — Conventions docs *(complete)*

Write under `docs\conventions\` before any code extraction:
- [x] `app-data.md` — `%APPDATA%` vs `%LOCALAPPDATA%` rules
- [x] `startup.md` — startup registration policy per app
- [x] `tray-shell.md` — NotifyIcon ownership, click policy, shutdown
- [x] `json-and-logging.md` — settings format, log format, rotation policy
- [x] Commit

### Phase 2 — `WindowsAppCore` skeleton + logging *(in progress)*

- [x] Create `shared\WindowsAppCore\WindowsAppCore.csproj` (net8.0-windows, no WinForms)
- [x] Implement `AppPaths`, `AppIdentity`
- [x] Implement `JsonLineWriter` (buffered channel drain)
- [x] Implement `AppLog` built on `JsonLineWriter`
- [x] Implement `CrashSink` (synchronous, direct write — separate from AppLog)
- [x] Implement `UnhandledExceptionWatcher`
- [x] Unit tests for all of the above
- [x] Migrate NetProfileSwitcher first (add logging — currently has zero)
- [x] Commit per logical unit

### Phase 3 — Settings *(complete)*

- [x] Implement `JsonSettingsStore<T>` (atomic write, quarantine, migration framework)
- [x] Implement `ISettingsMigration`
- [x] Tests: happy path, corrupt file, migration chain, power-loss atomic write
- [x] Migrate: NetProfileSwitcher → SoundTracker → ProgramHider → BatteryTray
- [x] Commit per app migration

### Phase 4 — Single instance + startup *(complete)*

- [x] Implement `SingleInstanceActivation` (wrapper over CrossIntegrityMutex + ActivationSignal)
- [x] Implement `IStartupRegistration`, `RunKeyStartupRegistration`, `ScheduledTaskStartupRegistration`
- [x] Note: `ScheduledTaskStartupRegistration` is the most complex — plan a sub-phase for BatteryTray's full StartupManager scope
- [x] Add to all four apps
- [x] Add startup registration UI to SoundTracker (currently has none)
- [x] Commit per app

### Phase 5 — Error handling, non-WinForms half *(complete)*

- [x] Wire `UnhandledExceptionWatcher` into all four apps
- [x] Commit

### Phase 6 — `WindowsTrayCore` + theme *(complete)*

- [x] Create `shared\WindowsTrayCore\WindowsTrayCore.csproj`
- [x] Implement `TrayTheme` (singleton, registry detection, Changed event)
- [x] Implement `UiDispatcher`
- [x] Implement `TrayIconManager` + `ITrayIconProvider`
- [x] Implement `TrayShell` (see concrete API in vision doc)
- [x] Implement `IUserNotifier`, `BalloonNotifier`, `ToastNotifier`
- [x] Fix NetProfileSwitcher hardcoded dark theme
- [x] Fix SoundTracker RecentActivityForm ignoring theme
- [x] Commit per logical unit

### Phase 7 — Icon providers *(blocked on Phase 6)*

- [ ] Implement `ITrayIconProvider` per app
- [ ] Replace manual GDI management with `TrayIconManager`
- [ ] Add dirty-flag to SoundTracker (stop re-rendering on every event)
- [ ] Commit per app

### Phase 8 — Threading cleanup *(blocked on Phase 7)*

- [ ] Replace ad-hoc dispatch with `UiDispatcher` across all apps
- [ ] Add `CancellationToken` propagation to all background workers
- [ ] Commit

### Phase 9 — `WindowsAppTesting` + test normalisation *(blocked on Phase 8)*

- [ ] Create `shared\WindowsAppTesting\WindowsAppTesting.csproj`
- [ ] Implement `TempAppData`, `FakeStartupRegistration`, `SettingsCorruptionFixture`, `FakeClock`
- [ ] Port `WindowsFact` / `WindowsTheory` from BatteryTray
- [ ] Align test discipline across all apps (BatteryTray is the reference)
- [ ] Commit

### Post-phases — GitHub remote prep *(blocked on Phase 9)*

- [ ] Verify each app builds and tests pass independently
- [ ] Clean root README
- [ ] Push to private GitHub remote
