# Program Hider Work Log

## 2026-05-10

### Planned Bundles

1. Design doc, work log, and roadmap baseline
2. Rich window rules and rule-capture tooling
3. Restore UX and placement correctness
4. Security/startup/reliability polish
5. Packaging/changelog polish

### Progress Entries

- Started `v0.1.0` planning pass and created the design/work-log artifacts.
- Landed the rich rule engine:
  - migrated settings from process-only rules to structured window rules
  - added rule match fields for process, title substring, and class name
  - added per-rule behaviors for auto-hide, PIN-gated restore, and quiet mode
  - added active-window inspection and one-click rule creation
  - added rule import/export in the settings UI
- Landed the restore UX bundle:
  - added a searchable restore browser with a recently hidden section
  - added optional restore-without-focus behavior in settings
  - started preserving window placement and monitor identity across hide/restore
- Landed the security/startup/reliability bundle:
  - added unlock timeout caching and separate bulk-restore PIN support
  - added startup delay handling and safe mode automation suspension
  - added automatic restore options for session lock and suspend
  - added watchdog pruning for dead handles and structured JSONL app logs
- Landed the packaging/release bundle:
  - bumped the app to `v0.1.0`
  - updated the build script to emit both the packaged exe and a portable zip
  - added a repo-local code-signing hook script and release documentation
- Added a verification harness:
  - factored hide/restore logic into a shared window service
  - added a repo-local console test host for deterministic unit/integration checks
  - added a repo-local WinForms smoke target so live tests stay self-contained
  - recorded a passing live smoke run: find -> hide-verify-ok -> restore-verify-ok
- Tightened the active-window path and live verification:
  - tracked the last manageable foreground window so tray actions do not lose the true active target
  - added deterministic settings override support for app-level hotkey smoke runs
  - verified direct normal-PowerShell hide/restore
  - verified the real Program Hider hotkey path against the sample window
- Hardened the hotkey/foreground path:
  - added a foreground-window event hook so the app keeps following the last real active target
  - expanded the test host with repo-local foreground inspection, `SendInput`, and Program Hider message-window probes
  - tightened the hotkey smoke flow so it drives the real `WM_HOTKEY` handler without depending on fragile shell focus tricks
