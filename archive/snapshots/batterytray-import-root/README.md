# BatteryTray test suite

Two test projects, one solution.

## Projects

**`BatteryTray.Tests`** — fast, deterministic unit tests for pure logic.
- `TimeFormatTests` — duration formatting boundaries
- `ColorBlenderTests` — RGB lerp, t clamping
- `RateHistoryTests` — bounded queue semantics
- `AppSettingsMigrationTests` — v1→v2→v3 schema transitions
- `BatteryChemistryTests` — IOCTL ASCII tag decoding + friendly mapping
- `PerformanceCounterPowerSamplerTests` — CPU% math, clamping, noise floor
- `ProcessPowerCoordinatorTests` — tier selection, fallback, the watchdog bug regression
- `EtwEnergyPowerSamplerTests` — admin probe, watchdog demotion behaviour

**`BatteryTray.E2ETests`** — runs against real Windows APIs.
- `BatteryMonitorE2ETests` — exercises real WinRT Battery, PowerStatus
- `BatteryHealthReaderE2ETests` — IOCTL + WMI, observes whatever the host has
- `ProcessPowerCoordinatorE2ETests` — verifies all three tiers register and Tier 1 produces samples on real hardware

E2E tests use a `[WindowsFact]` attribute that skips on non-Windows runners.

## Running

Both test projects target `net8.0-windows` (because `BatteryTray.csproj` does, and
NuGet enforces TFM compatibility on `ProjectReference`). They **build** on Linux
but only **run** on Windows because the test host needs `Microsoft.WindowsDesktop.App`.

```cmd
:: From a Windows dev machine:
dotnet test BatteryTray.sln -c Release

:: Just the fast tests:
dotnet test BatteryTray.Tests -c Release

:: Just the E2E tests (slowest — ETW watchdog tests have multi-second deadlines):
dotnet test BatteryTray.E2ETests -c Release
```

## Notable regressions covered

`ProcessPowerCoordinatorTests.GetCurrent_PrefersLowerTierWithDataOverHigherTierWithout`
locks in the v1.9 fix for the ETW-stuck-warming-up bug. Pre-fix, an ETW sampler
that reported `IsHealthy=true` but never produced a sample would be picked as the
active source forever, returning empty samples. The coordinator's selection
predicate `IsHealthy && HasFirstSample` is now contractual — if a higher tier
has no data, a lower tier with data wins.

`EtwEnergyPowerSamplerTests.StuckWarmup_TrippedByWatchdog_WhenAdminButProviderInactive`
covers the underlying ETW watchdog. When admin and the kernel session starts but
no events arrive within the deadline, the sampler must demote `IsHealthy=false`.

## What's not covered

UI rendering (NotifyIcon, the Battery Info form, sparkline drawing) — would
require a UI automation harness like FlaUI. The forms are thin enough that
manual verification is reasonable.

Real ETW behaviour on a system where the EnergyEstimation provider is actually
active. Hard to set up in CI; verified manually on hardware that supports it.

The scheduled-task install/uninstall flow (`StartupManager`) — touches a real
system service. Manually exercised; would need a sandboxed VM in CI.
