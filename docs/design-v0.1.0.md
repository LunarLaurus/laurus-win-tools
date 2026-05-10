# Program Hider Design Doc

## Goal

Evolve `Program Hider` from a working tray utility into a more complete Windows power-user tool with richer rule matching, safer restore flows, better restore ergonomics, stronger reliability, and more polished packaging.

Target milestone: `v0.1.0`

Status: implemented and shipped in the `v0.1.0` release on 2026-05-10.

## Current State

Current baseline already supports:

- tray-first app model
- hide active window via global hotkey
- auto-hide on minimize by process rule
- grouped tray restore menu
- persistent settings
- optional PIN/password restore protection
- launch-on-startup support

## Scope

This pass covers every feature previously listed, grouped into practical bundles that can be committed independently.

### Bundle 1: Design, rule model, and project tracking

- design doc
- work log
- changelog/versioning baseline
- prepare data model for richer rule matching

### Bundle 2: Rich window rules and capture tools

- rules matched by:
  - process name
  - window title substring
  - class name
- per-rule behavior:
  - auto-hide on minimize
  - require PIN on restore
  - suppress notifications
- inspect-active-window UI:
  - title
  - class
  - process
  - handle
- one-click rule creation from the active window
- export/import rules JSON

### Bundle 3: Restore UX and placement correctness

- searchable restore browser
- recently hidden section
- restore without focus steal
- stronger restore placement:
  - preserve window placement
  - preserve restore rectangle
  - preserve monitor affinity where possible
- detect monitor mismatch and log it when relevant

### Bundle 4: Security, startup, and reliability

- unlock timeout after successful PIN entry
- separate PIN/password for restore-all
- optional secure clear/restore behavior on:
  - session lock
  - sleep/suspend
- optional startup delay
- startup change notifications
- watchdog prune for dead handles
- structured app log
- safe mode toggle to temporarily disable auto-hide automation

### Bundle 5: Packaging and release polish

- changelog file
- portable zip artifact
- build script updates for release packaging
- code-signing hook/documentation

## Non-Goals

- remote sync
- cloud backup
- full installer authoring with custom wizard UX in this pass
- guaranteed virtual desktop reattachment across all third-party desktop managers

## Rule Model

Rules move from a flat process-name list to a richer model:

- `RuleName`
- `MatchProcessName`
- `MatchTitleContains`
- `MatchClassName`
- `AutoHideOnMinimize`
- `RequirePinOnRestore`
- `SuppressNotifications`

Matching policy:

- empty match fields are ignored
- a rule matches when every populated field matches the candidate window
- multiple matching rules are merged conservatively:
  - any `RequirePinOnRestore` means restore is protected
  - any `SuppressNotifications` suppresses balloons
  - any `AutoHideOnMinimize` enables auto-hide

## Restore Model

Each hidden window should retain:

- handle
- title
- process name
- class name
- capture time
- last-hidden timestamp
- whether it was maximized
- `WINDOWPLACEMENT`
- restore rectangle
- monitor/device name at hide time
- whether restore requires PIN due to rule or global setting

## Security Model

Security should remain optional and default off.

States:

- global restore protection off
- global restore protection on
- per-rule restore protection on
- restore-all protection optionally stronger than single-window restore

Unlock behavior:

- user can enter PIN/password once
- unlock remains valid for a configurable timeout window
- timeout expiry clears the unlock

## Startup Model

Startup registration stays in HKCU Run.

Behavior:

- Run entry points to the current executable with a startup argument
- startup argument allows:
  - optional delay
  - quieter notifications
  - safe-mode awareness

## Reliability Model

Add a lightweight maintenance timer to:

- remove invalid window handles
- prune stale recent-history entries
- emit structured log entries

Structured logs go under `%APPDATA%\ProgramHider\logs`.

## Packaging Plan

Packaging outputs:

- single-file framework-dependent exe
- portable zip containing the exe and companion files

Code signing:

- support a signing step hook/script
- actual signing requires an available certificate and is treated as external-input dependent

## Risks

- Win32 event traffic can be noisy; logging must be bounded
- title/class matching can over-match if rules are too broad
- restore placement logic can fight app-specific behavior
- session-lock secure clear needs a safe default to avoid stranding hidden windows

## Acceptance Criteria

- settings UI can create, edit, export, and import rich rules
- hidden-window restore browser supports search and recent entries
- restore placement is more stable across monitor changes
- safe mode visibly disables automation without breaking manual restore
- build script produces both exe and portable zip
- work log records each bundle landed
