# Windows Apps — Project Index

*Last updated: 2026-05-13*

All projects target Windows, .NET 8, WinForms unless noted otherwise.

---

## DriveDredge
**Version:** v1.13.1
**Purpose:** Block-level disk imaging tool for raw archival of removable USB drives. Built specifically to recover and preserve data from old or failing USB sticks, capturing as much data as possible rather than achieving forensic perfection.

**Stack:** C# / .NET 8 / WinForms / System.Management (WMI) — single-file self-contained win-x64 executable; cross-compiles from Linux via `build.sh`.

**Key Features:**
- WMI-based USB enumeration with serial number tracking
- MBR/GPT detection and filesystem classification (PlainData / BootableOrComplex / UnknownOrEncrypted)
- Adaptive output modes: raw image, ZIP, or both — auto-selected by drive type
- SHA-256 hashing of all output files
- Bad-sector detection with configurable zero-fill or abort behaviour
- File-to-offset mapping (FSCTL_GET_RETRIEVAL_POINTERS) to identify files on bad sectors
- Per-region timing stats (64 MiB chunks) to detect failing hardware
- Structured metrics output (`.metrics.json`) per drive
- Theme system: dark, light, lesbian pride, trans pride
- Queue-based multi-drive processing

**Code Style:**
- Architecture: functional decomposition by subsystem — `Drives/`, `Filesystem/`, `Imaging/`, `Settings/`, `Ui/` — flat namespace, mostly sealed and static utility classes
- Naming: PascalCase classes, camelCase private fields, UPPER_CASE constants
- Error handling: graceful degradation (WMI failures → empty results, not crashes); `using` blocks for all P/Invoke handles
- Comments: high density, intent-focused XML docs explaining *why* — design rationale, thresholds, edge cases
- Tests: no automated test suite; manual verification with synthetic data on Linux, hardware validation on Windows
- Notable: immutable `record` types for data payloads; exponential moving average for throughput; progressive file-map built before device lock

---

## BatteryTray
**Version:** current source tree present in `files(18)\BatteryTray`
**Purpose:** Configurable Windows battery tray replacement. It renders the battery state directly into the tray icon, shows toast/balloon notifications, exposes health diagnostics, and can auto-start elevated through Task Scheduler.

**Stack:** C# / .NET 8 / WinForms / WMI / WinRT battery APIs / `Microsoft.Toolkit.Uwp.Notifications` / xunit

**Key Features:**
- Numeric, bar, or hybrid tray icon rendering
- Color-coded charge states with configurable thresholds
- Action Center toasts with balloon fallback
- Battery health diagnostics: design capacity, full-charge capacity, chemistry, cycle count
- Silent elevated auto-start via scheduled task
- Single-instance behavior with activation signal wakeup for second launches
- Settings schema migration (`v1 → v2 → v3`) with broken-file quarantine
- Optional power-plan auto-switching plus process power sampling infrastructure

**Code Style:**
- Architecture: `ApplicationContext` tray shell (`BatteryTrayContext`), explicit renderer (`IconRenderer`), settings model/store (`AppSettings`), startup backend (`StartupManager`), notification wrapper (`Notifier`), and power sampler/coordinator subsystem
- Naming: PascalCase types with focused file-per-type layout; test projects mirror production concerns cleanly
- Error handling: defensive logging via `CrashLogger`, broken settings backup, transient polling failures swallowed rather than killing the tray app
- Comments: strong rationale comments around startup/elevation, mutex behavior, toast fallback, and icon handle lifecycle
- Tests: **strong coverage** — unit tests plus E2E tests for battery, WMI/IOCTL, and power-sampler selection behavior
- Notable: explicit native `HICON` lifecycle management, self-elevating scheduled-task install path, and cross-integrity single-instance primitives

---

## NetProfileSwitcher
**Version:** See project source
**Purpose:** Tray utility for managing Windows network profiles with auto-switching rules based on detected Wi-Fi SSID.

**Stack:** C# / .NET 8 / WinForms

**Key Features:**
- SSID polling with configurable auto-switch rules
- netsh CLI integration for profile application
- Persistent JSON config
- Administrator elevation support
- Dark theme

**Code Style:** Event-driven WinForms; JSON config persistence; graceful netsh failure handling.

---

## program-hider
**Version:** v0.1.5 (Rust prototype archived at v0.0.1)
**Purpose:** Tray utility for hiding application windows on demand and restoring them via menu or hotkey, with rule-based automation and PIN protection.

**Stack:** C# / .NET 8 / WinForms (current); Rust prototype (archived)

**Key Features:**
- Global hotkey: Ctrl+Shift+H
- Rule engine matching on process name, window title, or class
- Restore PIN / password protection
- JSONL audit log
- JSON config persistence
- Elevation handling
- Smoke test suite

**Code Style:** Rule-engine pattern; JSONL audit logging; test coverage via smoke tests.

---

## sound-tracker
**Version:** See project source
**Purpose:** Tray utility that records a historical log of Windows audio activity — session starts/stops, device changes, master volume — to a JSONL audit file.

**Stack:** C# / .NET 8 / WinForms / Core Audio API (Windows)

**Key Features:**
- Real-time audio device and session monitoring via Core Audio
- Persistent JSONL audit log
- Dynamic tray icon (theme-aware)
- Recent Activity window
- Smoke tests with real audio playback

**Code Style:** Event-driven Core Audio callbacks; JSONL audit logging (consistent pattern with program-hider); smoke-test coverage.

---

## Common Patterns Across the Suite

| Aspect | Detail |
|---|---|
| **Language / Runtime** | C# / .NET 8 across all five projects |
| **UI** | WinForms + NotifyIcon (tray) as the dominant pattern |
| **Config persistence** | JSON in app-data locations, but not yet normalized: `%APPDATA%` for BatteryTray/ProgramHider/NetProfileSwitcher, `%LOCALAPPDATA%` for SoundTracker history/logs |
| **Audit logging** | JSONL append-only logs (program-hider, sound-tracker) |
| **Themes** | Dark mode support in all; DriveDredge adds pride palettes |
| **Error handling** | Graceful degradation preferred over hard crashes throughout |
| **Test maturity** | Ranges from none (DriveDredge) to strong unit+E2E (BatteryTray) |
| **Elevation** | NetProfileSwitcher and BatteryTray handle admin requirements explicitly |
