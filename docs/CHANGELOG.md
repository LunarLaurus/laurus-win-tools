# Changelog

## Unreleased

- No unreleased changes yet.

## v0.1.4

- Added a tray command to restart Program Hider as administrator.
- Added hide-failure prompts that can relaunch elevated and retry the same target window automatically.
- Added startup support for pending re-hide handles during an elevated relaunch.
- Expanded automated coverage for startup-option parsing and elevated relaunch argument composition.
- Added a compatibility document plus a verification-first build flow with release-startup smoke testing.

## v0.1.3

- Added a foreground-window event hook so hotkey and inspect actions retain the last real active window more reliably.
- Added repo-local foreground, `SendInput`, and message-window probe helpers to the verification harness.
- Hardened the hotkey smoke test by driving the real `WM_HOTKEY` path through Program Hider's hidden message window and retry-based hide assertions.
- Expanded the repo-local verification harness to 15 automated checks.

## v0.1.2

- Fixed tray-driven active-window resolution by tracking the last real foreground window before the tray steals focus.
- Added deterministic settings-path override support for smoke runs.
- Added direct normal-PowerShell and real Program Hider hotkey smoke scripts.
- Expanded the repo-local verification harness to 14 automated checks.
- Clarified hide failures for windows that likely require Program Hider to run elevated too.

## v0.1.1

- Added a repo-local test host for deterministic verification.
- Added an isolated smoke-window app and smoke-test script for live hide/restore checks.
- Repackaged the app so the shipped artifact matches the verified test-harness state.

## v0.1.0

- Added structured window rules with process, title, and class matching.
- Added per-rule auto-hide, restore PIN, and quiet-notification behaviors.
- Added active-window inspection plus rule import/export tooling.
- Added a searchable restore browser with recent-window history.
- Added optional restore-without-focus behavior.
- Added placement/monitor capture to improve restore correctness.
- Added unlock timeout caching and an optional separate bulk-restore PIN/password.
- Added startup delay handling plus a safe mode toggle for suspending automation.
- Added session-lock/suspend safety actions, dead-handle pruning, and structured JSONL logs.

## v0.0.4

- Added startup registration support.
- Added optional restore PIN/password flow.
- Added grouped hidden-window restore behavior.

## v0.0.3

- Added branded icon asset.
- Added persistent settings.
- Added configurable hotkey.
- Added process-based auto-hide rules.

## v0.0.2

- Rebased the active app onto `.NET`/WinForms.
- Archived the legacy Rust prototype.
