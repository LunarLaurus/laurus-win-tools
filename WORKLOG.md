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
