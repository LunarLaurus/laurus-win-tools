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

## Phase Checklist

### Phase 0 — Workspace restructure *(not started)*

No code changes. Directory moves only.

- [ ] `git init` at `D:\code\windows-apps\`
- [ ] Create `apps\`, `shared\`, `archive\snapshots\`, `docs\conventions\`
- [ ] `DriveDredge_v1.13.1\` → `archive\DriveDredge_v1.13.1\`
- [ ] BatteryTray canonical source (from inside `BatteryTray\`):
  - [ ] `BatteryTray\BatteryTray\`          → `apps\BatteryTray\BatteryTray\`
  - [ ] `BatteryTray\BatteryTray.Tests\`    → `apps\BatteryTray\BatteryTray.Tests\`
  - [ ] `BatteryTray\BatteryTray.E2ETests\` → `apps\BatteryTray\BatteryTray.E2ETests\`
  - [ ] `BatteryTray\BatteryTray.sln`       → `apps\BatteryTray\BatteryTray.sln`
  - [ ] Remaining loose files at `BatteryTray\` root → `archive\snapshots\batterytray-import-root\`
  - [ ] Remove now-empty `BatteryTray\` import root
- [ ] `NetProfileSwitcher\` → `apps\NetProfileSwitcher\`
- [ ] `program-hider\`      → `apps\ProgramHider\`
- [ ] `sound-tracker\`      → `apps\SoundTracker\`
- [ ] Verify `.gitignore` excludes `claude-*` and `codex-*` before committing
- [ ] Initial commit with detailed message — no AI attribution
- [ ] Update this worklog entry with commit hash

### Phase 1 — Conventions docs *(blocked on Phase 0)*

Write under `docs\conventions\` before any code extraction:
- [ ] `app-data.md` — `%APPDATA%` vs `%LOCALAPPDATA%` rules
- [ ] `startup.md` — startup registration policy per app
- [ ] `tray-shell.md` — NotifyIcon ownership, click policy, shutdown
- [ ] `json-and-logging.md` — settings format, log format, rotation policy
- [ ] Commit

### Phase 2 — `WindowsAppCore` skeleton + logging *(blocked on Phase 1)*

- [ ] Create `shared\WindowsAppCore\WindowsAppCore.csproj` (net8.0-windows, no WinForms)
- [ ] Implement `AppPaths`, `AppIdentity`
- [ ] Implement `JsonLineWriter` (buffered channel drain)
- [ ] Implement `AppLog` built on `JsonLineWriter`
- [ ] Implement `CrashSink` (synchronous, direct write — separate from AppLog)
- [ ] Implement `UnhandledExceptionWatcher`
- [ ] Unit tests for all of the above
- [ ] Migrate NetProfileSwitcher first (add logging — currently has zero)
- [ ] Commit per logical unit

### Phase 3 — Settings *(blocked on Phase 2)*

- [ ] Implement `JsonSettingsStore<T>` (atomic write, quarantine, migration framework)
- [ ] Implement `ISettingsMigration`
- [ ] Tests: happy path, corrupt file, migration chain, power-loss atomic write
- [ ] Migrate: NetProfileSwitcher → SoundTracker → ProgramHider → BatteryTray
- [ ] Commit per app migration

### Phase 4 — Single instance + startup *(blocked on Phase 3)*

- [ ] Implement `SingleInstanceActivation` (wrapper over CrossIntegrityMutex + ActivationSignal)
- [ ] Implement `IStartupRegistration`, `RunKeyStartupRegistration`, `ScheduledTaskStartupRegistration`
- [ ] Note: `ScheduledTaskStartupRegistration` is the most complex — plan a sub-phase for BatteryTray's full StartupManager scope
- [ ] Add to all four apps
- [ ] Add startup registration UI to SoundTracker (currently has none)
- [ ] Commit per app

### Phase 5 — Error handling, non-WinForms half *(blocked on Phase 4)*

- [ ] Wire `UnhandledExceptionWatcher` into all four apps
- [ ] Commit

### Phase 6 — `WindowsTrayCore` + theme *(blocked on Phase 5)*

- [ ] Create `shared\WindowsTrayCore\WindowsTrayCore.csproj`
- [ ] Implement `TrayTheme` (singleton, registry detection, Changed event)
- [ ] Implement `UiDispatcher`
- [ ] Implement `TrayIconManager` + `ITrayIconProvider`
- [ ] Implement `TrayShell` (see concrete API in vision doc)
- [ ] Implement `IUserNotifier`, `BalloonNotifier`, `ToastNotifier`
- [ ] Fix NetProfileSwitcher hardcoded dark theme
- [ ] Fix SoundTracker RecentActivityForm ignoring theme
- [ ] Commit per logical unit

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
