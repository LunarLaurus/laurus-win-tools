# Worklog

Resumability artifact. Read this to get current state before continuing.

---

## 2026-05-13

**Did:** Full planning and design phase completed across three review rounds.
- Audited all five apps (NetProfileSwitcher, program-hider, sound-tracker, BatteryTray, DriveDredge)
- Authoritative design vision established
- Three rounds of design review resolved library split, crash logging, serialiser policy, TrayShell design, repo strategy, BatteryTray API validation
- Established project rules
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
- `.gitignore` extended to exclude private working files by name in addition to prefix patterns

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

## 2026-05-13

**Did:** Phase 7 — dirty-flag mechanism added to `ITrayIconProvider` / `TrayIconManager`.
- `ITrayIconProvider.HasChanged` default interface property (default: `true`) — providers override to implement dirty-flag; backward compatible with all existing stubs
- `TrayIconManager.RequestRefresh()` short-circuits when `HasChanged == false`; `ForceRefresh()` and theme-change path bypass the flag unconditionally
- 6 new tests in `TrayIconManagerTests`; 38 total pass
- Per-app provider implementations (NPS, SoundTracker, ProgramHider, BatteryTray) deferred — icon rendering code untouched

**Committed:** 679335a

**Next:** Phase 8 — Threading cleanup (`UiDispatcher` adoption, `CancellationToken` propagation)

---

## 2026-05-13

**Did:** Phase 8 — Threading cleanup complete.
- SoundTracker: replaced `private readonly Control _uiDispatcher` + forced-HWND pattern with `UiDispatcher _ui`; added `_shuttingDown` bool flag to guard against post-dispose dispatch; updated all `BeginInvoke`/`InvokeRequired`/`IsDisposed` call sites; removed debug log line for HWND
- BatteryTray: added `UiDispatcher _ui`; replaced `ContextMenuStrip.BeginInvoke` in the `BatterySaverController` callback and `ActivationRequested` handler with `_ui.Post`; added `_ui.Dispose()` in `Dispose(bool)`; added `WindowsTrayCore` project reference
- ProgramHider: replaced `private readonly SynchronizationContext _uiContext` with `UiDispatcher _ui`; replaced all four `_uiContext.Post` calls (activation, pending-startup-hide, minimize-event, foreground-event) with `_ui.Post` closures; removed `MinimizeEventPayload` and `ForegroundEventPayload` payload structs that existed solely to avoid `this` capture; added `WindowsTrayCore` project reference
- NetProfileSwitcher: added `UiDispatcher _ui` and `CancellationTokenSource _applyCts`; replaced `BeginInvoke`/`InvokeRequired` pattern with `_ui.Post`; added `_applyCts.Token` to all three `Task.Run(NetCommands.Apply)` calls with `OperationCanceledException` handling; cleanup in `OnFormClosed`

**Committed:** 22ffcc5 (SoundTracker), 9ffe4bf (BatteryTray), f3e0a6f (ProgramHider), 602cbab (NPS)

**Next:** Phase 9 — `WindowsAppTesting` + test normalisation

---

## 2026-05-13 11:40

**Did:** Phase 9 — `WindowsAppTesting` + test normalisation complete.
- `shared/WindowsAppTesting` library created: `WindowsFactAttribute`, `WindowsTheoryAttribute`, `TempAppData`, `FakeStartupRegistration`, `SettingsCorruptionFixture`, `FakeClock`
- `IClock` interface + `SystemClock` singleton added to `WindowsAppCore`; injected into `JsonLineWriter` and `AppLog` (optional param, backward compatible)
- All five existing test projects: added `WindowsAppTesting` project reference and `GlobalUsings.cs`; removed local `WindowsFactAttribute.cs` copies (4 files deleted)
- `JsonSettingsStoreTests`: migrated to `TempAppData`; removed manual env-var boilerplate
- `SoundTrackerConfigTests`: migrated to `TempAppData`; three try/finally blocks collapsed to IDisposable
- `JsonLineWriterTests`: new `DateRollover_WritesToNewFileAtMidnight` test using `FakeClock`
- `EtwEnergyPowerSampler._isHealthy`: fixed race — changed default from `false` to `true` so the `ConstructedWithoutAdmin_QuicklyReportsUnhealthy` wait loop actually waits for RunSession to flip it
- `ProgramHider.Tests`: new xUnit project replacing custom `TestHost/Program.cs` runner; all 17 unit tests migrated to `[Fact]`/`[WindowsFact]`; `FakeWindowPlatform` extracted to its own file; added to `ProgramHider.sln`

**Committed:** ee7befa (WindowsAppTesting library), 1aaae3a (IClock injection), d0d607f (test project migration), 514a10a (EtwEnergy fix), b3f5b0c (ProgramHider.Tests)

**Next:** Post-phases — GitHub remote prep

---

## 2026-05-13 11:55

**Did:** Post-phases — GitHub remote prep complete.
- Full release build verified: all 7 projects (4 apps + 3 shared libs) clean, 0 warnings
- Full test run: 178/178 passing across 6 test projects
- Root `README.md` written
- Private GitHub remote configured (`LunarLaurus/laurus-win-tools`) and pushed

**Committed:** caf6b19 (README)
**Remote:** https://github.com/LunarLaurus/laurus-win-tools.git (master, up-to-date)

**Next:** All planned phases complete — open development / per-issue work

---

## 2026-05-13 12:30

**Did:** GitHub Actions workflow + install script + next-phase planning.
- `.github/workflows/build.yml`: CI pipeline — runs 6 test projects, publishes all 4 apps framework-dependent, uploads artifacts; triggers on push/PR to master
- `install.ps1`: PowerShell 5.1 install/uninstall; publishes to `%LOCALAPPDATA%\LaurusWinTools\<AppName>`; optional `-AutoRun` writes HKCU Run key; ProgramHider gets `--startup --delay=5` startup args; `-Uninstall` removes dir and registry entry
- Brainstormed Phases 10–13: CLI args standardisation, install script hardening, release pipeline, version stamping, cleanup (TestHost retirement, single-instance audit), settings schema versioning

**Committed:** f3ea969 (workflow + install script)

**Next:** Phases 10–13 complete — see entries below

---

## 2026-05-13 14:00

**Did:** Phases 10–13 complete in four discrete commits.

Phase 10 — CLI args standardisation + install script hardening:
- `WindowsAppCore.StartupOptions`: parses `--startup` and `--delay=N` (0–300 s); 13 tests
- `ProgramHider.StartupOptions`: refactored to delegate to core, keeps `--safe-mode` / `--rehide=` locally
- All four apps accept `string[] args`, parse `StartupOptions`, apply delay after single-instance claim
- `install.ps1`: all four apps get `--startup --delay=5` under `-AutoRun`; `Stop-AppIfRunning` kills running instance before overwrite; `Assert-DesktopRuntime` warns if .NET 8 Desktop Runtime absent

Phase 11 — Version stamping + release pipeline:
- `Directory.Build.props`: `RELEASE_VERSION` env var overrides all four version properties; local builds use csproj values
- `SoundTracker/AppMetadata.cs`: `DisplayVersion` / `TooltipPrefix` now read `Application.ProductVersion` dynamically
- NPS and BatteryTray: initial tray icon `Text` includes `ProductVersion`
- `.github/workflows/release.yml`: tag-triggered (`v*`), runs all 6 test suites, stamps binaries with tag version, zips and creates GitHub Release

Phase 12 — Cleanup:
- `ProgramHider.TestHost`: deleted `Program.cs` and `.csproj`; removed from `ProgramHider.sln`
- Single-instance audit: all four apps confirmed using `SingleInstanceActivation.TryClaim`

Phase 13 — Settings schema versioning + configurable startup delay:
- `SchemaVersion` added to `ProgramHider.AppSettings` (= 1) and `NetProfileSwitcher.AppConfig` (= 1)
- `StartupDelaySeconds` added to `BatteryTray.AppSettings`, `NetProfileSwitcher.AppConfig`, `SoundTrackerConfig`
- All four `Program.cs`: CLI `--delay=N` takes priority; `settings.StartupDelaySeconds` is the fallback

**Committed:** 2dbb339 (Ph10), 0ebda38 (Ph11), 03a4415 (Ph12), 9809957 (Ph13)

**Next:** Open development — no pending phases

---

## 2026-05-13 14:30

**Did:** Planned and logged Phases 14–17 (auto-versioning, update checker, repo audit, publish).
- Phase 14: GitVersion Mainline mode; CI derives semver from commit history
- Phase 15: `UpdateChecker` in `WindowsAppCore`; GitHub releases API polling; wired into all four tray contexts
- Phase 16: Full repo audit for prohibited references, private file leakage, secrets
- Phase 17: Make repo public; first `v1.0.0` tag; verify auto-update flow end-to-end

**Committed:** (worklog only — no code yet)

**Next:** Phase 14 — GitVersion auto-versioning

---

## 2026-05-13 15:00

**Did:** Phase 14 — GitVersion auto-versioning complete.
- `GitVersion.yml`: Mainline mode on master, patch increment, semver bump via commit message keywords (`+semver: breaking/minor/fix`)
- `build.yml`: `fetch-depth: 0`; GitVersion 5.x setup + execute steps; all four publish steps stamp `RELEASE_VERSION` from `gitversion.outputs.semVer`
- `release.yml`: replaced manual tag-strip step with GitVersion setup + execute; all downstream refs updated to `gitversion.outputs.semVer`

**Committed:** 02d9ffd

**Next:** Phase 15 — Update checker

---

## 2026-05-13 15:30

**Did:** Phase 15 — Update checker complete.
- `WindowsAppCore.UpdateCheckResult` record, `UpdateChecker` class — polls GitHub releases API, parses `tag_name`, compares with `Version.Parse`
- `StartPeriodicChecks`: fires on launch + every 24 h; unparseable current version skips checks
- `WindowsAppTesting.FakeHttpMessageHandler`: configurable response lambda, `Requests` list for assertions, `WithJson`/`ThatThrows` factory helpers
- 11 `UpdateCheckerTests` covering all paths; 201/201 tests passing
- All four tray contexts wired: balloon tip on update available, cancellation + disposal on exit

**Committed:** 40777e6

**Next:** Phase 16 — Full repo audit

---

## 2026-05-13 16:00

**Did:** Phase 16 — Full repo audit complete.
- Audited 291 tracked files across 99 commits for prohibited references and secrets
- Deleted `archive/DriveDredge_v1.13.1/` (27 retired-project files) and `apps/NetProfileSwitcher/docs/superpowers/` (4 obsolete planning docs)
- Scrubbed remaining references in `WORKLOG.md` and `apps/SoundTracker/docs/dotnet8-migration.md`
- Rewrote full git history with `git-filter-repo` (4 passes): purged deleted paths + content/message text replacements across all 99 commits
- Force-pushed rewritten history to origin
- 201/201 tests pass post-rewrite; all four apps build clean

**Committed:** 582e5ce (HEAD scrub, later rewritten to d249d80)

**Next:** Phase 17 — Publish + tag first release

---

## 2026-05-13 16:30

**Did:** Phase 17 — Public beta launched.
- Repository visibility flipped to public on GitHub
- Public REST API (`api.github.com/repos/LunarLaurus/laurus-win-tools`) reachable without auth — UpdateChecker endpoint confirmed
- Moved private-tooling exclusion patterns from `.gitignore` (public) to `.git/info/exclude` (local-only, never committed) so public `.gitignore` shows only generic build/IDE/OS patterns
- README rewritten for public framing: Install section pointing at GitHub Releases, .NET 8 Desktop Runtime prerequisite, install.ps1 helper, auto-update behaviour
- Added `LICENSE` under PolyForm Noncommercial 1.0.0 — free for personal/hobby/academic/charitable/government use; commercial use requires a separate licence
- Tagged `v1.0.0`; GitHub Actions release workflow completed in 2.7 min with all four app zips published: BatteryTray (10.49 MB), NetProfileSwitcher (6.27 MB), ProgramHider (6.48 MB), SoundTracker (6.28 MB)

**Committed:** 358b82b (tidy), 8836a45 (README + LICENSE)
**Release:** https://github.com/LunarLaurus/laurus-win-tools/releases/tag/v1.0.0

**Next:** Smoke-test release zips

---

## 2026-05-13 17:15

**Did:** Smoke-tested release zips; discovered + fixed UpdateChecker SemVer bug.
- Downloaded all four `v1.0.0` zips; verified each app launches and exits cleanly
- Confirmed BatteryTray stamped `ProductVersion = 1.0.0+358b82bb...` (GitVersion semver + commit SHA)
- Caught: `System.Version.TryParse("1.0.0+sha")` returns false → `_currentVersion` was null in production → CheckAsync returned `NoUpdate` unconditionally → auto-update was silently disabled in v1.0.0
- Fix: added `UpdateChecker.ParseSemver` helper that strips prerelease (`-`) and build-metadata (`+`) suffixes before `Version.TryParse`; applied to both the constructor's `currentVersion` and the remote `tag_name`
- 4 new functional tests + 8-row `[Theory]` covering edge cases; 85/85 pass
- Tagged `v1.0.1`; release workflow completed in ~2 min with stamped binaries (`1.0.1+2ec42b8...`)
- Smoke-tested v1.0.1 BatteryTray: launches cleanly, version reads correctly

**Committed:** 2ec42b8 (fix)
**Release:** https://github.com/LunarLaurus/laurus-win-tools/releases/tag/v1.0.1

**Caveat:** v1.0.0 users will need to upgrade manually once; v1.0.1+ installs auto-update going forward.

**Next:** Phases 18–23 — Polish + UX

---

## 2026-05-13 17:45

**Did:** Scoped Polish + UX work into six phases (18–23) and added them to the phase checklist.
- Phase 18: shared About / Check-for-updates / Open-logs menu block — highest visibility, smallest surface
- Phase 19: SoundTracker settings dialog (currently no UI; JSON-only)
- Phase 20: ProgramHider settings dialog (largest surface — hotkey, rules editor, PIN config)
- Phase 21: First-run welcomes for the three apps that lack them
- Phase 22: Per-app icon providers (Phase 7's deferred wiring)
- Phase 23: Accessibility pass — AccessibleName, tab order, keyboard shortcuts, High-DPI

**Committed:** (worklog only — code starts with Phase 18)

**Next:** Phase 18 — standard tray-menu block

---

## 2026-05-13 18:15

**Did:** Phase 18 — Standard tray-menu block complete.
- `WindowsTrayCore.RepoInfo`: shared owner/name/url/license constants
- `WindowsTrayCore.AboutDialog`: theme-aware Form with title, version (suffix-stripped), repo link, license summary, optional extra-info text area, "Check for updates" button
- `WindowsTrayCore.StandardMenuItems`: three factories — `CreateAbout` (lazy `Func<string?>` extraInfoProvider so live diagnostics stay fresh), `CreateCheckForUpdates` (one-shot check, balloon result), `CreateOpenLogs` (Explorer at `%LOCALAPPDATA%\<App>\logs`)
- All four tray menus updated: BatteryTray retained its rich diagnostic content via `BuildAboutDiagnostics`; the other three got the new block fresh
- Each app now stores `_updateChecker` as a field so the menu and background poller share one instance
- 12 new tests (4 StandardMenuItems + 8 AboutDialog Theory rows); 224/224 across all six test projects

**Committed:** 42b1138

**Next:** Phase 18.5 — Shell_NotifyIcon wrapper for stable tray identity

---

## 2026-05-13 18:45

**Did:** Phase 18.5 — Shell_NotifyIcon wrapper.
- Discovered that WinForms `NotifyIcon` exposes no Guid setter, so Windows treats every rebuild as a new tray icon and demotes it to overflow. Each user has to drag the icon out of overflow after every update.
- Built `WindowsTrayCore.TrayIcon`: drop-in replacement using `Shell_NotifyIcon` P/Invoke directly with `NIF_GUID`. Stable per-app identity derived deterministically from app name (SHA-256, RFC-4122 v5 bits) via `AppIdGuid.For("AppName")`.
- Bonus: TrayIcon handles the broadcast `TaskbarCreated` message and re-adds itself after explorer restarts — a long-standing gap in WinForms NotifyIcon.
- Migrated all four apps off NotifyIcon. `StandardMenuItems.CreateCheckForUpdates` and `BatteryTray.Notifier` updated to take TrayIcon.
- `install.ps1` hardened: `Stop-AppIfRunning` now waits for the process to actually exit (up to 10s) and then sleeps 250ms for the kernel handle close, so the install dir overwrite no longer races.
- 14 new tests; 238/238 passing.

**Committed:** 608fbcf

**Next:** Phase 19 — SoundTracker settings dialog

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
- [x] Verify `.gitignore` excludes private working files before committing
- [x] Initial commit with detailed message
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

### Phase 7 — Icon providers *(complete)*

- [x] Add `HasChanged` dirty-flag property to `ITrayIconProvider` (default true — backward compatible)
- [x] `TrayIconManager.RequestRefresh()` skips render when `HasChanged == false`; `ForceRefresh()` and theme changes bypass the flag
- [x] 6 new tests in `TrayIconManagerTests` covering all dirty-flag branches (38 total pass)
- [ ] Per-app `ITrayIconProvider` implementations and GDI management wiring — deferred to later phase
- [ ] Commit per app

### Phase 8 — Threading cleanup *(complete)*

- [x] Replace ad-hoc dispatch with `UiDispatcher` across all apps
- [x] Add `CancellationToken` propagation to background apply workers (NPS)
- [x] Commit

### Phase 9 — `WindowsAppTesting` + test normalisation *(complete)*

- [x] Create `shared\WindowsAppTesting\WindowsAppTesting.csproj`
- [x] Implement `TempAppData`, `FakeStartupRegistration`, `SettingsCorruptionFixture`, `FakeClock`
- [x] Port `WindowsFact` / `WindowsTheory` from BatteryTray
- [x] Align test discipline across all apps (BatteryTray is the reference)
- [x] Commit

### Post-phases — GitHub remote prep *(complete)*

- [x] Verify each app builds and tests pass independently
- [x] Clean root README
- [x] Push to private GitHub remote

### Phase 10 — CLI args standardisation + install script hardening *(complete)*

- [x] Add `StartupOptions` to `WindowsAppCore`: parse `--startup` and `--delay=N`; tests
- [x] Wire `StartupOptions` into all four apps' `Program.cs` (replace ad-hoc arg handling)
- [x] Update `install.ps1` to pass `--startup --delay=5` for all four apps under `-AutoRun`
- [x] Kill running app processes before overwriting install dir in `install.ps1`
- [x] Fix `install.ps1` runtime check: validate .NET 8 Windows Desktop Runtime, not the SDK
- [x] Commit per logical unit

### Phase 11 — Release pipeline + version stamping *(complete)*

- [x] Add `AssemblyInformationalVersion` MSBuild property wired from git tag at publish time
- [x] Surface version in tray tooltip for each app (e.g. "AppName v1.2.3")
- [x] Tag-triggered GitHub Actions release workflow: builds, publishes, creates GitHub Release with zipped artifacts attached
- [x] Commit per logical unit

### Phase 12 — Cleanup *(complete)*

- [x] Retire `ProgramHider.TestHost` project: delete directory, remove from `ProgramHider.sln`
- [x] Single-instance audit: confirm all four apps use `SingleInstanceActivation`; add any missing
- [x] Commit

### Phase 13 — Settings schema versioning + configurable startup delay *(complete)*

- [x] Add `SchemaVersion` field to `ProgramHider.AppSettings` and `NetProfileSwitcher.AppConfig` (infrastructure already present in `JsonSettingsStore`)
- [x] Add `StartupDelaySeconds` to each app's settings type (fallback when `--delay=N` not passed)
- [x] Wire CLI-arg-over-settings priority in all four `Program.cs`
- [x] Commit per logical unit

### Phase 14 — GitVersion auto-versioning *(complete)*

- [x] Add `GitVersion.yml` at repo root (Mainline mode, master branch, patch increment)
- [x] Update `build.yml` to run `gittools/actions@v2` GitVersion step; expose `gitVersion.semVer` as env var for publish steps
- [x] Update `release.yml` to derive version from GitVersion output rather than manual tag strip
- [x] Verify `RELEASE_VERSION` continues to stamp all four app binaries correctly in CI
- [x] Commit

### Phase 15 — Update checker *(complete)*

- [x] `UpdateCheckResult` record and `UpdateChecker` class added to `WindowsAppCore`
- [x] `FakeHttpMessageHandler` added to `WindowsAppTesting` for deterministic HTTP unit tests
- [x] 11 tests covering all paths: newer version, same, older, malformed, network failure, endpoint, User-Agent
- [x] All four tray contexts wired: `StartPeriodicChecks` at startup, balloon tip on update, CTS/HttpClient disposed on exit
- [x] Commit per logical unit (library, then per-app wiring)

### Phase 16 — Full repo audit *(complete)*

- [x] Scan all tracked files for prohibited attribution references
- [x] Verify commit messages are clean of attribution leakage
- [x] Verify no private working files are tracked
- [x] Confirm `.gitignore` coverage is complete
- [x] Verify no hardcoded secrets, tokens, or credentials anywhere
- [x] Review README and docs for any policy violations
- [x] Delete archived sub-project and obsolete planning docs that contained prohibited references
- [x] Rewrite git history to purge prohibited references from all prior commits

### Phase 17 — Publish + auto-update wire-up *(complete)*

- [x] Make GitHub repository public
- [x] Confirm `UpdateChecker` can reach `api.github.com/repos/LunarLaurus/laurus-win-tools` without auth (public API)
- [x] Move private-tooling exclusion patterns to `.git/info/exclude` so public `.gitignore` is clean
- [x] Update README for public framing; add `LICENSE` (PolyForm Noncommercial 1.0.0)
- [x] Tag and push `v1.0.0` to trigger first public release
- [x] Verify GitHub Actions release workflow fires cleanly on first public tag (2.7 min, success)
- [x] Verify all four app zips appear in GitHub Release
- [x] Smoke-test release zips; ship `v1.0.1` to fix SemVer-suffix parsing in UpdateChecker
- [x] Add `-Run` flag to `install.ps1` and standalone `run-all.ps1` for post-install launching

### Phase 18 — Standard tray-menu block: About / Check for updates / Open logs *(complete)*

- [x] `WindowsTrayCore.StandardMenuItems`: static helpers that build the three `ToolStripMenuItem`s
- [x] `WindowsTrayCore.AboutDialog`: theme-aware form — app name, version, repo URL link, license summary, optional diagnostic block, "Check for updates" button
- [x] "Check for updates" wiring: triggers `UpdateChecker.CheckAsync` once and shows result via balloon
- [x] "Open logs" wiring: launches Explorer at `%LOCALAPPDATA%\<AppName>\logs`
- [x] Add the block to BatteryTray / NetProfileSwitcher / ProgramHider / SoundTracker context menus
- [x] Tests: `AboutDialog` constructs cleanly, `StandardMenuItems` shape, version suffix-trim Theory
- [x] Commit per logical unit

### Phase 18.5 — Shell_NotifyIcon wrapper for stable tray identity *(complete)*

- [x] `WindowsTrayCore.AppIdGuid`: deterministic Guid from app id (SHA-256, RFC-4122 v5)
- [x] `WindowsTrayCore.Native.TrayNativeMethods`: Shell_NotifyIcon P/Invoke surface
- [x] `WindowsTrayCore.TrayIcon`: NotifyIcon replacement with NIF_GUID + TaskbarCreated recovery + NOTIFYICON_VERSION_4
- [x] Migrate BatteryTray / NetProfileSwitcher / ProgramHider / SoundTracker off NotifyIcon
- [x] Update `Notifier` and `StandardMenuItems.CreateCheckForUpdates` to take TrayIcon
- [x] Tests: AppIdGuid determinism + variant bits, TrayIcon construction + property round-trip
- [x] `install.ps1` race fix: wait for process exit + handle release before deleting install dir

### Phase 19 — SoundTracker settings dialog

SoundTracker currently has no settings UI — `RunAtStartup` is a menu toggle, everything else is hand-edited JSON.

- [ ] `SoundTracker.App.SettingsForm`: theme-aware Form with controls for `RunAtStartup`, `StartupDelaySeconds`, and any session/history retention knobs that exist
- [ ] Wire Settings menu item in `TrayApplicationContext`; close menu Run-at-startup toggle in favour of the dialog (or keep both for ergonomics — decide during build)
- [ ] Atomic save via `JsonSettingsStore`
- [ ] Tests: form construction, save round-trip, validation
- [ ] Commit

### Phase 20 — ProgramHider settings dialog

ProgramHider has the most settings of any app and the worst current UX (hand-edited JSON with hotkey enum values, window rules, PIN config).

- [ ] `ProgramHider.SettingsForm`: theme-aware Form, sections for hotkey, safe mode, startup delay, restore-PIN config, window rules editor (list + add/edit/delete rule dialog)
- [ ] Reuse pattern emerging from SoundTracker; extract `SettingsFormBase` in `WindowsTrayCore` only if 2+ concrete forms share enough surface
- [ ] Wire Settings menu item; deprecate any current menu-item config toggles where the dialog replaces them
- [ ] Tests: form construction, save round-trip, rule editor flow
- [ ] Commit

### Phase 21 — First-run welcomes for NPS, SoundTracker, ProgramHider

BatteryTray already shows a first-run balloon. Add equivalents to the other three.

- [ ] `WindowsTrayCore.FirstRunBalloon`: helper that shows a one-shot balloon ("Right-click the tray icon to configure / open settings") gated by a `ShownFirstRunWelcome` flag in each app's settings
- [ ] Add `ShownFirstRunWelcome` to `NetProfileSwitcher.AppConfig`, `SoundTrackerConfig`, `ProgramHider.AppSettings` (with schema bump)
- [ ] Wire into each app's tray context after the initial render
- [ ] Tests: flag persistence, idempotency on subsequent launches
- [ ] Commit

### Phase 22 — Per-app icon providers

Phase 7 added the `ITrayIconProvider` interface with a `HasChanged` dirty-flag default but deferred per-app wiring. Each app currently renders its tray icon ad-hoc.

- [ ] BatteryTray: wrap `IconRenderer` in `BatteryIconProvider : ITrayIconProvider`; route through `TrayIconManager`
- [ ] SoundTracker: wrap `TrayIconRenderer`
- [ ] NetProfileSwitcher: wrap the current state-driven icon logic
- [ ] ProgramHider: wrap the `_appIcon` source (mostly static, dirty-flag returns false except on theme change)
- [ ] Tests: provider behaviour, `HasChanged` semantics, theme-change forced refresh
- [ ] Commit per app

### Phase 23 — Accessibility pass

- [ ] Set `AccessibleName` / `AccessibleDescription` on each app's `NotifyIcon` and key form controls
- [ ] Audit tab order on all settings forms and `NetProfileSwitcher.MainForm`
- [ ] Add keyboard-shortcut underlines (`&Save`, `&Cancel`, etc.) where missing
- [ ] Verify High-DPI scaling on each form at 100 / 125 / 150 / 200 % at design time
- [ ] Tests: AccessibleName surface where assertable
- [ ] Commit
