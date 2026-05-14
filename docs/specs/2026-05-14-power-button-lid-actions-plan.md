# Configurable Power-Button and Lid-Close Actions — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Hardware Actions tab to BatteryTray's Settings dialog that lets the user choose what Windows does when the hardware power button is pressed or the laptop lid is closed. Values are written to every installed power plan on Save under a single UAC prompt.

**Architecture:** New `HardwareActionsController` static class wraps `powercfg.exe` (matching the existing `PowerPlanController` idiom). Read path uses unelevated `powercfg /q SCHEME_CURRENT SUB_BUTTONS`. Write path batches every required `/setacvalueindex` and `/setdcvalueindex` call into one `cmd.exe /c "..."` chain invoked via the `runas` verb (UAC prompt). New `HardwareActionPolicy` class persists into `AppSettings` via a v3 -> v4 schema bump.

**Tech Stack:** .NET 8, WinForms, xUnit + FluentAssertions, `powercfg.exe` shell-out, `System.Diagnostics.Process` with `Verb = "runas"`.

**Spec:** `docs/specs/2026-05-14-power-button-lid-actions.md`

---

## File Structure

### New files

| Path | Responsibility |
|---|---|
| `apps\BatteryTray\BatteryTray\HardwareAction.cs` | Enum of the five Windows actions; integer values match powercfg indices |
| `apps\BatteryTray\BatteryTray\HardwareActionsSnapshot.cs` | Read-only return type for `ReadCurrent()` |
| `apps\BatteryTray\BatteryTray\HardwareActionPolicy.cs` | Mutable persisted preference class |
| `apps\BatteryTray\BatteryTray\HardwareActionsController.cs` | powercfg wrapper: read, args builder, parser, elevated apply |
| `apps\BatteryTray\BatteryTray\Settings\AppSettingsMigrationV3ToV4.cs` | No-op schema bump |
| `apps\BatteryTray\BatteryTray.Tests\HardwareActionPolicyTests.cs` | |
| `apps\BatteryTray\BatteryTray.Tests\HardwareActionsControllerArgsBuilderTests.cs` | |
| `apps\BatteryTray\BatteryTray.Tests\HardwareActionsControllerParserTests.cs` | |
| `apps\BatteryTray\BatteryTray.Tests\AppSettingsMigrationV3ToV4Tests.cs` | |
| `apps\BatteryTray\BatteryTray.Tests\fixtures\powercfg-sub-buttons-balanced.txt` | Real powercfg output fixture |
| `apps\BatteryTray\BatteryTray.E2ETests\HardwareActionsControllerE2ETests.cs` | Real-powercfg integration |

### Modified files

| Path | Change |
|---|---|
| `apps\BatteryTray\BatteryTray\AppSettings.cs` | `CurrentSchemaVersion = 4`; new `HardwareActions` property; register V3->V4 migration |
| `apps\BatteryTray\BatteryTray\SettingsForm.cs` | Rename Power tab; insert Hardware Actions tab; wire load/save |
| `apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj` | `<None Include="fixtures\*.txt" CopyToOutputDirectory="PreserveNewest" />` |
| `WORKLOG.md` | One new entry on the final commit |

### Working directory

All paths relative to `D:\code\windows-apps\`. All shell commands assume PowerShell (the project shell). Use `dotnet build` and `dotnet test` from the repo root unless otherwise noted.

---

## Task 1: Add `HardwareAction` enum and `HardwareActionsSnapshot` record struct

These two types are trivially correct and have no behaviour to test. Group them in one commit so subsequent tasks can reference them.

**Files:**
- Create: `apps\BatteryTray\BatteryTray\HardwareAction.cs`
- Create: `apps\BatteryTray\BatteryTray\HardwareActionsSnapshot.cs`

- [ ] **Step 1: Create `HardwareAction.cs`**

```csharp
namespace BatteryTray;

/// <summary>
/// The five actions Windows supports for power-button and lid-close events.
/// Integer values match the indices powercfg uses for /setacvalueindex and
/// /setdcvalueindex, so the enum value can be cast to int and emitted directly.
/// </summary>
public enum HardwareAction
{
    DoNothing      = 0,
    Sleep          = 1,
    Hibernate      = 2,
    ShutDown       = 3,
    TurnOffDisplay = 4,
}
```

- [ ] **Step 2: Create `HardwareActionsSnapshot.cs`**

```csharp
namespace BatteryTray;

/// <summary>
/// Live read-back from powercfg of the current power-button and lid-close
/// actions on the active scheme. Used to populate the Settings dialog and
/// to detect drift between BatteryTray's persisted policy and Windows state.
/// </summary>
public readonly record struct HardwareActionsSnapshot(
    HardwareAction PowerButtonAc,
    HardwareAction PowerButtonDc,
    HardwareAction LidCloseAc,
    HardwareAction LidCloseDc);
```

- [ ] **Step 3: Build to verify the BatteryTray project still compiles**

Run: `dotnet build apps\BatteryTray\BatteryTray\BatteryTray.csproj`
Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add apps/BatteryTray/BatteryTray/HardwareAction.cs apps/BatteryTray/BatteryTray/HardwareActionsSnapshot.cs
git commit -m "BatteryTray: add HardwareAction enum and HardwareActionsSnapshot record struct

Foundation types for the upcoming Hardware Actions feature. The enum's
integer values are the powercfg setting indices (0=DoNothing, 1=Sleep,
2=Hibernate, 3=ShutDown, 4=TurnOffDisplay), so callers can cast directly
without a lookup table.

HardwareActionsSnapshot is the return type for the upcoming
HardwareActionsController.ReadCurrent() call."
```

---

## Task 2: Add `HardwareActionPolicy` class with TDD-driven tests

**Files:**
- Create: `apps\BatteryTray\BatteryTray\HardwareActionPolicy.cs`
- Create: `apps\BatteryTray\BatteryTray.Tests\HardwareActionPolicyTests.cs`

- [ ] **Step 1: Write the failing test**

Create `apps\BatteryTray\BatteryTray.Tests\HardwareActionPolicyTests.cs`:

```csharp
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace BatteryTray.Tests;

public class HardwareActionPolicyTests
{
    [Fact]
    public void Defaults_AreSleepAndSleepAndDifferOnBatteryFalse()
    {
        var policy = new HardwareActionPolicy();

        policy.PowerButton.Should().Be(HardwareAction.Sleep);
        policy.LidClose.Should().Be(HardwareAction.Sleep);
        policy.DifferOnBattery.Should().BeFalse();
        policy.PowerButtonOnBattery.Should().Be(HardwareAction.Sleep);
        policy.LidCloseOnBattery.Should().Be(HardwareAction.Hibernate);
    }

    [Fact]
    public void JsonRoundtrip_PreservesAllFields()
    {
        var original = new HardwareActionPolicy
        {
            PowerButton          = HardwareAction.Hibernate,
            LidClose             = HardwareAction.DoNothing,
            DifferOnBattery      = true,
            PowerButtonOnBattery = HardwareAction.ShutDown,
            LidCloseOnBattery    = HardwareAction.TurnOffDisplay,
        };

        var json = JsonSerializer.Serialize(original);
        var roundtripped = JsonSerializer.Deserialize<HardwareActionPolicy>(json);

        roundtripped.Should().NotBeNull();
        roundtripped!.PowerButton.Should().Be(HardwareAction.Hibernate);
        roundtripped.LidClose.Should().Be(HardwareAction.DoNothing);
        roundtripped.DifferOnBattery.Should().BeTrue();
        roundtripped.PowerButtonOnBattery.Should().Be(HardwareAction.ShutDown);
        roundtripped.LidCloseOnBattery.Should().Be(HardwareAction.TurnOffDisplay);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj --filter FullyQualifiedName~HardwareActionPolicyTests`
Expected: Build error `CS0246: The type or namespace name 'HardwareActionPolicy' could not be found`.

- [ ] **Step 3: Create `HardwareActionPolicy.cs`**

```csharp
namespace BatteryTray;

/// <summary>
/// User-configured policy for hardware power events. Persisted into AppSettings.
///
/// When <see cref="DifferOnBattery"/> is false, the two "OnBattery" fields are
/// ignored at apply time and the AC values are written to both AC and DC
/// powercfg indices. When true, the OnBattery fields populate the DC indices.
/// </summary>
public sealed class HardwareActionPolicy
{
    public HardwareAction PowerButton { get; set; } = HardwareAction.Sleep;
    public HardwareAction LidClose    { get; set; } = HardwareAction.Sleep;

    public bool DifferOnBattery { get; set; }
    public HardwareAction PowerButtonOnBattery { get; set; } = HardwareAction.Sleep;
    public HardwareAction LidCloseOnBattery    { get; set; } = HardwareAction.Hibernate;
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj --filter FullyQualifiedName~HardwareActionPolicyTests`
Expected: 2 tests pass.

- [ ] **Step 5: Commit**

```bash
git add apps/BatteryTray/BatteryTray/HardwareActionPolicy.cs apps/BatteryTray/BatteryTray.Tests/HardwareActionPolicyTests.cs
git commit -m "BatteryTray: add HardwareActionPolicy persistence class

Holds the user's preferred action for power-button and lid-close events.
DifferOnBattery toggles whether the OnBattery fields are consulted when
applying values; when false the AC values are written to both AC and DC
powercfg indices.

Tests cover defaults and System.Text.Json round-trip (the same serialiser
JsonSettingsStore<T> uses)."
```

---

## Task 3: Add `AppSettingsMigrationV3ToV4` with TDD-driven tests

The migration is a functional no-op: it bumps `SchemaVersion` to 4 and changes nothing else. It exists so the migration chain remains complete and loading a v3 file yields a populated v4 instance with `HardwareActions == null`.

**Files:**
- Create: `apps\BatteryTray\BatteryTray\Settings\AppSettingsMigrationV3ToV4.cs`
- Create: `apps\BatteryTray\BatteryTray.Tests\AppSettingsMigrationV3ToV4Tests.cs`

- [ ] **Step 1: Write the failing test**

Create `apps\BatteryTray\BatteryTray.Tests\AppSettingsMigrationV3ToV4Tests.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;

namespace BatteryTray.Tests;

public class AppSettingsMigrationV3ToV4Tests
{
    [Fact]
    public void FromVersion_IsThree()
    {
        new AppSettingsMigrationV3ToV4().FromVersion.Should().Be(3);
    }

    [Fact]
    public void Migrate_BumpsSchemaVersionToFour()
    {
        var v3Json = """
            {
              "SchemaVersion": 3,
              "UpdateIntervalSeconds": 30,
              "LowBatteryThreshold": 20
            }
            """;

        var output = new AppSettingsMigrationV3ToV4().Migrate(JsonDocument.Parse(v3Json));
        var node = JsonNode.Parse(output.RootElement.GetRawText())!.AsObject();

        ((int)node["SchemaVersion"]!).Should().Be(4);
    }

    [Fact]
    public void Migrate_PreservesAllExistingFields()
    {
        var v3Json = """
            {
              "SchemaVersion": 3,
              "UpdateIntervalSeconds": 45,
              "LowBatteryThreshold": 15,
              "NotifyOnLow": false,
              "ColorCharging": "#ABCDEF"
            }
            """;

        var output = new AppSettingsMigrationV3ToV4().Migrate(JsonDocument.Parse(v3Json));
        var node = JsonNode.Parse(output.RootElement.GetRawText())!.AsObject();

        ((int)node["UpdateIntervalSeconds"]!).Should().Be(45);
        ((int)node["LowBatteryThreshold"]!).Should().Be(15);
        ((bool)node["NotifyOnLow"]!).Should().BeFalse();
        ((string)node["ColorCharging"]!).Should().Be("#ABCDEF");
    }

    [Fact]
    public void Migrate_DoesNotAddHardwareActionsField()
    {
        var v3Json = """{"SchemaVersion": 3}""";

        var output = new AppSettingsMigrationV3ToV4().Migrate(JsonDocument.Parse(v3Json));
        var node = JsonNode.Parse(output.RootElement.GetRawText())!.AsObject();

        node.ContainsKey("HardwareActions").Should().BeFalse(
            because: "the migration is a no-op for content; HardwareActions stays null which the serialiser will fill in on Load");
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj --filter FullyQualifiedName~AppSettingsMigrationV3ToV4Tests`
Expected: Build error `CS0246: The type or namespace name 'AppSettingsMigrationV3ToV4' could not be found`.

- [ ] **Step 3: Create `AppSettingsMigrationV3ToV4.cs`**

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using WindowsAppCore;

namespace BatteryTray;

internal sealed class AppSettingsMigrationV3ToV4 : ISettingsMigration
{
    public int FromVersion => 3;

    public JsonDocument Migrate(JsonDocument raw)
    {
        var node = JsonNode.Parse(raw.RootElement.GetRawText()) as JsonObject;
        if (node is null) return raw;

        // v3 -> v4: no field rename, no value remap. The bump exists to
        // keep the migration-chain pattern intact and to ensure loading a
        // v3 file produces a fully-populated v4 instance with
        // HardwareActions == null (sentinel for "user has never configured
        // this", which drives the Settings dialog's initial-state logic).
        node["SchemaVersion"] = 4;
        return JsonDocument.Parse(node.ToJsonString());
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj --filter FullyQualifiedName~AppSettingsMigrationV3ToV4Tests`
Expected: 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add apps/BatteryTray/BatteryTray/Settings/AppSettingsMigrationV3ToV4.cs apps/BatteryTray/BatteryTray.Tests/AppSettingsMigrationV3ToV4Tests.cs
git commit -m "BatteryTray: add AppSettingsMigrationV3ToV4 (no-op schema bump)

The v3 -> v4 migration is functional no-op: it bumps SchemaVersion to 4
and changes nothing else. The bump exists so loading a v3 file through
JsonSettingsStore<AppSettings> after the v4 wiring is in place yields a
fully-populated v4 instance with HardwareActions == null, which is the
sentinel for 'user has never configured this' that drives the Settings
dialog's initial-state logic.

Migration is not yet registered in AppSettings.Store — that wiring comes
in the next commit."
```

---

## Task 4: Bump `AppSettings` to v4 and register migration

**Files:**
- Modify: `apps\BatteryTray\BatteryTray\AppSettings.cs`
- Create: `apps\BatteryTray\BatteryTray.Tests\AppSettingsV4IntegrationTests.cs`

- [ ] **Step 1: Write the failing test**

Create `apps\BatteryTray\BatteryTray.Tests\AppSettingsV4IntegrationTests.cs`:

```csharp
using System.IO;
using System.Text.Json;
using FluentAssertions;
using WindowsAppTesting;
using Xunit;

namespace BatteryTray.Tests;

public class AppSettingsV4IntegrationTests
{
    [Fact]
    public void Load_FreshFile_HasSchemaVersionFourAndNullHardwareActions()
    {
        using var temp = new TempAppData("BatteryTray");

        var settings = AppSettings.Load();
        try
        {
            settings.SchemaVersion.Should().Be(4);
            settings.HardwareActions.Should().BeNull(
                because: "a brand-new file has no user-configured policy");
        }
        finally
        {
            settings.Save();
        }
    }

    [Fact]
    public void Load_FromV3OnDiskFile_MigratesToV4WithNullHardwareActions()
    {
        using var temp = new TempAppData("BatteryTray");

        var settingsPath = Path.Combine(temp.AppDataDir, "settings.json");
        File.WriteAllText(settingsPath, """
            {
              "SchemaVersion": 3,
              "UpdateIntervalSeconds": 45
            }
            """);

        var settings = AppSettings.Load();

        settings.SchemaVersion.Should().Be(4);
        settings.UpdateIntervalSeconds.Should().Be(45,
            because: "the v3 -> v4 migration must preserve existing values");
        settings.HardwareActions.Should().BeNull();
    }

    [Fact]
    public void SaveAndLoad_PreservesHardwareActionsPolicy()
    {
        using var temp = new TempAppData("BatteryTray");

        var written = AppSettings.Load();
        written.HardwareActions = new HardwareActionPolicy
        {
            PowerButton          = HardwareAction.Hibernate,
            LidClose             = HardwareAction.DoNothing,
            DifferOnBattery      = true,
            PowerButtonOnBattery = HardwareAction.ShutDown,
            LidCloseOnBattery    = HardwareAction.TurnOffDisplay,
        };
        written.Save();

        var read = AppSettings.Load();

        read.HardwareActions.Should().NotBeNull();
        read.HardwareActions!.PowerButton.Should().Be(HardwareAction.Hibernate);
        read.HardwareActions.LidClose.Should().Be(HardwareAction.DoNothing);
        read.HardwareActions.DifferOnBattery.Should().BeTrue();
        read.HardwareActions.PowerButtonOnBattery.Should().Be(HardwareAction.ShutDown);
        read.HardwareActions.LidCloseOnBattery.Should().Be(HardwareAction.TurnOffDisplay);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj --filter FullyQualifiedName~AppSettingsV4IntegrationTests`
Expected: 3 tests fail — `SchemaVersion` is 3 (not 4) and `HardwareActions` property does not exist (build error `CS1061` on `settings.HardwareActions`).

- [ ] **Step 3: Modify `AppSettings.cs`**

Locate the `CurrentSchemaVersion` constant near the top of the class and change it from 3 to 4. Locate the existing fields and add the `HardwareActions` property in a new section. Locate the `Store` field's migration array and append `new AppSettingsMigrationV3ToV4()`. Final relevant excerpts:

```csharp
public int SchemaVersion { get; set; } = CurrentSchemaVersion;
public const int CurrentSchemaVersion = 4;
```

```csharp
// ---- Hardware actions ----
// Null = user has never configured this; the Settings dialog shows live
// Windows values. Non-null = user's last saved preference.
public HardwareActionPolicy? HardwareActions { get; set; }
```

Place the new section between the existing `Power plan auto-switch` section and the `Colors` section.

```csharp
private static readonly JsonSettingsStore<AppSettings> Store = new(
    "BatteryTray",
    migrations: new ISettingsMigration[]
    {
        new AppSettingsMigrationV1ToV2(),
        new AppSettingsMigrationV2ToV3(),
        new AppSettingsMigrationV3ToV4(),
    });
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj --filter FullyQualifiedName~AppSettingsV4IntegrationTests`
Expected: 3 tests pass.

- [ ] **Step 5: Run the full BatteryTray.Tests suite to confirm no regression**

Run: `dotnet test apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj`
Expected: All existing BatteryTray.Tests pass + the new tests pass. Total count should be (previous count) + 9 (2 policy + 4 migration + 3 integration).

- [ ] **Step 6: Commit**

```bash
git add apps/BatteryTray/BatteryTray/AppSettings.cs apps/BatteryTray/BatteryTray.Tests/AppSettingsV4IntegrationTests.cs
git commit -m "BatteryTray: bump AppSettings schema to v4 and register HardwareActions

Adds AppSettings.HardwareActions (nullable HardwareActionPolicy) and
registers AppSettingsMigrationV3ToV4 on the JsonSettingsStore<AppSettings>
migration chain. CurrentSchemaVersion goes 3 -> 4.

Null is the sentinel for 'user has never configured the new feature';
the upcoming Settings dialog branch on this to decide whether to show
live Windows values or the persisted policy. The v3 -> v4 migration
preserves all existing fields untouched.

Integration tests use WindowsAppTesting.TempAppData to redirect the
JsonSettingsStore<T> file location to a temp dir via the BATTERYTRAY_DATA
env var."
```

---

## Task 5: `HardwareActionsController.BuildCmdArgs` (pure function, TDD)

The args builder is the most testable seam in the controller. Implement it first, in isolation, so the elevated apply path becomes a thin wrapper.

**Files:**
- Create: `apps\BatteryTray\BatteryTray\HardwareActionsController.cs` (skeleton with only `BuildCmdArgs` so far)
- Create: `apps\BatteryTray\BatteryTray.Tests\HardwareActionsControllerArgsBuilderTests.cs`

- [ ] **Step 1: Write the failing test**

Create `apps\BatteryTray\BatteryTray.Tests\HardwareActionsControllerArgsBuilderTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace BatteryTray.Tests;

public class HardwareActionsControllerArgsBuilderTests
{
    private static readonly Guid PlanA = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    private static readonly Guid PlanB = new("a1841308-3541-4fab-bc81-f71556f20b4a");
    private static readonly Guid PlanC = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");

    [Fact]
    public void BuildCmdArgs_NoPlans_StillEndsWithSetactive()
    {
        var policy = new HardwareActionPolicy();

        var args = HardwareActionsController.BuildCmdArgs(Array.Empty<Guid>(), policy);

        args.Should().Be("/c \"powercfg /setactive SCHEME_CURRENT\"",
            because: "with zero plans we still refresh the active scheme so the call is a clean no-op rather than a malformed cmd line");
    }

    [Fact]
    public void BuildCmdArgs_OnePlan_DifferOnBatteryFalse_WritesAcEqualsDc()
    {
        var policy = new HardwareActionPolicy
        {
            PowerButton     = HardwareAction.Sleep,       // index 1
            LidClose        = HardwareAction.Hibernate,   // index 2
            DifferOnBattery = false,
            // OnBattery fields should be ignored when DifferOnBattery=false
            PowerButtonOnBattery = HardwareAction.ShutDown,
            LidCloseOnBattery    = HardwareAction.DoNothing,
        };

        var args = HardwareActionsController.BuildCmdArgs(new[] { PlanA }, policy);

        args.Should().Be(
            "/c \"powercfg /setacvalueindex 381b4222-f694-41f0-9685-ff5bb260df2e 4f971e89-eebd-4455-a8de-9e59040e7347 7648efa3-dd9c-4e3e-b566-50f929386280 1 && " +
            "powercfg /setdcvalueindex 381b4222-f694-41f0-9685-ff5bb260df2e 4f971e89-eebd-4455-a8de-9e59040e7347 7648efa3-dd9c-4e3e-b566-50f929386280 1 && " +
            "powercfg /setacvalueindex 381b4222-f694-41f0-9685-ff5bb260df2e 4f971e89-eebd-4455-a8de-9e59040e7347 5ca83367-6e45-459f-a27b-476b1d01c936 2 && " +
            "powercfg /setdcvalueindex 381b4222-f694-41f0-9685-ff5bb260df2e 4f971e89-eebd-4455-a8de-9e59040e7347 5ca83367-6e45-459f-a27b-476b1d01c936 2 && " +
            "powercfg /setactive SCHEME_CURRENT\"");
    }

    [Fact]
    public void BuildCmdArgs_OnePlan_DifferOnBatteryTrue_WritesDistinctAcDc()
    {
        var policy = new HardwareActionPolicy
        {
            PowerButton          = HardwareAction.Sleep,       // AC index 1
            LidClose             = HardwareAction.Sleep,       // AC index 1
            DifferOnBattery      = true,
            PowerButtonOnBattery = HardwareAction.Hibernate,   // DC index 2
            LidCloseOnBattery    = HardwareAction.ShutDown,    // DC index 3
        };

        var args = HardwareActionsController.BuildCmdArgs(new[] { PlanA }, policy);

        args.Should().Contain("/setacvalueindex 381b4222-f694-41f0-9685-ff5bb260df2e 4f971e89-eebd-4455-a8de-9e59040e7347 7648efa3-dd9c-4e3e-b566-50f929386280 1");
        args.Should().Contain("/setdcvalueindex 381b4222-f694-41f0-9685-ff5bb260df2e 4f971e89-eebd-4455-a8de-9e59040e7347 7648efa3-dd9c-4e3e-b566-50f929386280 2");
        args.Should().Contain("/setacvalueindex 381b4222-f694-41f0-9685-ff5bb260df2e 4f971e89-eebd-4455-a8de-9e59040e7347 5ca83367-6e45-459f-a27b-476b1d01c936 1");
        args.Should().Contain("/setdcvalueindex 381b4222-f694-41f0-9685-ff5bb260df2e 4f971e89-eebd-4455-a8de-9e59040e7347 5ca83367-6e45-459f-a27b-476b1d01c936 3");
        args.Should().EndWith("powercfg /setactive SCHEME_CURRENT\"");
    }

    [Fact]
    public void BuildCmdArgs_ThreePlans_EmitsTwelveValueWritesPlusSetactive()
    {
        var policy = new HardwareActionPolicy();

        var args = HardwareActionsController.BuildCmdArgs(new[] { PlanA, PlanB, PlanC }, policy);

        // 3 plans * (2 actions * 2 power-states) = 12 value writes + 1 setactive
        var setAcCount = System.Text.RegularExpressions.Regex.Matches(args, "/setacvalueindex").Count;
        var setDcCount = System.Text.RegularExpressions.Regex.Matches(args, "/setdcvalueindex").Count;
        var setactiveCount = System.Text.RegularExpressions.Regex.Matches(args, "/setactive ").Count;

        setAcCount.Should().Be(6,    because: "3 plans * 2 actions (power button + lid)");
        setDcCount.Should().Be(6,    because: "3 plans * 2 actions (power button + lid)");
        setactiveCount.Should().Be(1, because: "one trailing /setactive refreshes the live state");
    }

    [Theory]
    [InlineData(HardwareAction.DoNothing,      0)]
    [InlineData(HardwareAction.Sleep,          1)]
    [InlineData(HardwareAction.Hibernate,      2)]
    [InlineData(HardwareAction.ShutDown,       3)]
    [InlineData(HardwareAction.TurnOffDisplay, 4)]
    public void BuildCmdArgs_EmitsExpectedIntegerIndexForEachAction(HardwareAction action, int expectedIndex)
    {
        var policy = new HardwareActionPolicy
        {
            PowerButton = action,
            LidClose    = action,
        };

        var args = HardwareActionsController.BuildCmdArgs(new[] { PlanA }, policy);

        args.Should().Contain($"7648efa3-dd9c-4e3e-b566-50f929386280 {expectedIndex}",
            because: $"PowerButton={action} must serialise to AC index {expectedIndex}");
        args.Should().Contain($"5ca83367-6e45-459f-a27b-476b1d01c936 {expectedIndex}",
            because: $"LidClose={action} must serialise to AC index {expectedIndex}");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj --filter FullyQualifiedName~HardwareActionsControllerArgsBuilderTests`
Expected: Build error `CS0246: The type or namespace name 'HardwareActionsController' could not be found`.

- [ ] **Step 3: Create the skeleton `HardwareActionsController.cs` with only `BuildCmdArgs`**

```csharp
using System;
using System.Collections.Generic;
using System.Text;

namespace BatteryTray;

/// <summary>
/// Wraps powercfg.exe to read and write the power-button and lid-close
/// actions used by Windows. Reads are unelevated (powercfg /q works for
/// standard users on the active scheme). Writes require admin, so
/// <see cref="ApplyToAllPlans"/> spawns an elevated cmd.exe via the runas
/// verb that chains every required setacvalueindex/setdcvalueindex into
/// one UAC prompt per Save.
/// </summary>
public static class HardwareActionsController
{
    public static readonly Guid SubButtons    = new("4f971e89-eebd-4455-a8de-9e59040e7347");
    public static readonly Guid PButtonAction = new("7648efa3-dd9c-4e3e-b566-50f929386280");
    public static readonly Guid LidAction     = new("5ca83367-6e45-459f-a27b-476b1d01c936");

    /// <summary>
    /// Builds the argument string passed to cmd.exe /c. Chains every
    /// powercfg invocation with &amp;&amp; so the elevated process's exit
    /// code reflects the first powercfg failure rather than only the last.
    /// </summary>
    internal static string BuildCmdArgs(IReadOnlyList<Guid> planGuids, HardwareActionPolicy policy)
    {
        int acPower = (int)policy.PowerButton;
        int dcPower = (int)(policy.DifferOnBattery ? policy.PowerButtonOnBattery : policy.PowerButton);
        int acLid   = (int)policy.LidClose;
        int dcLid   = (int)(policy.DifferOnBattery ? policy.LidCloseOnBattery : policy.LidClose);

        var sb = new StringBuilder();
        sb.Append("/c \"");

        for (int i = 0; i < planGuids.Count; i++)
        {
            var p = planGuids[i];
            sb.Append("powercfg /setacvalueindex ").Append(p).Append(' ').Append(SubButtons).Append(' ').Append(PButtonAction).Append(' ').Append(acPower).Append(" && ");
            sb.Append("powercfg /setdcvalueindex ").Append(p).Append(' ').Append(SubButtons).Append(' ').Append(PButtonAction).Append(' ').Append(dcPower).Append(" && ");
            sb.Append("powercfg /setacvalueindex ").Append(p).Append(' ').Append(SubButtons).Append(' ').Append(LidAction).Append(' ').Append(acLid).Append(" && ");
            sb.Append("powercfg /setdcvalueindex ").Append(p).Append(' ').Append(SubButtons).Append(' ').Append(LidAction).Append(' ').Append(dcLid).Append(" && ");
        }

        sb.Append("powercfg /setactive SCHEME_CURRENT\"");
        return sb.ToString();
    }
}
```

Note: `BuildCmdArgs` is `internal`. For tests to see it, we need `InternalsVisibleTo("BatteryTray.Tests")`. The BatteryTray.csproj already has this assembly attribute — verify by reading line 25-29 of `BatteryTray.csproj`. If missing, add it before building.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj --filter FullyQualifiedName~HardwareActionsControllerArgsBuilderTests`
Expected: 4 facts + 5 theory rows = 9 tests pass.

- [ ] **Step 5: Commit**

```bash
git add apps/BatteryTray/BatteryTray/HardwareActionsController.cs apps/BatteryTray/BatteryTray.Tests/HardwareActionsControllerArgsBuilderTests.cs
git commit -m "BatteryTray: HardwareActionsController.BuildCmdArgs

Pure function that produces the argument string for an elevated cmd.exe
invocation chaining every powercfg /setacvalueindex and /setdcvalueindex
needed to apply a HardwareActionPolicy to a list of power plans.

Commands are chained with && so the elevated process's exit code reflects
the first powercfg failure rather than only the last (cmd.exe's & operator
would mask intermediate failures).

DifferOnBattery=false collapses AC values into both AC and DC indices.
A trailing /setactive SCHEME_CURRENT refreshes the live state once every
write has landed.

Other controller methods (ReadCurrent, ParseSubButtonsQuery,
ApplyToAllPlans) come in follow-up commits."
```

---

## Task 6: `HardwareActionsController.ParseSubButtonsQuery` + fixture file (TDD)

**Files:**
- Create: `apps\BatteryTray\BatteryTray.Tests\fixtures\powercfg-sub-buttons-balanced.txt`
- Create: `apps\BatteryTray\BatteryTray.Tests\HardwareActionsControllerParserTests.cs`
- Modify: `apps\BatteryTray\BatteryTray\HardwareActionsController.cs` (add parser method)
- Modify: `apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj` (copy fixtures to output)

- [ ] **Step 1: Create the fixture file**

Create `apps\BatteryTray\BatteryTray.Tests\fixtures\powercfg-sub-buttons-balanced.txt`. The content below is captured real powercfg output (English locale, Windows 11). Lid close index AC=1 Sleep, DC=1 Sleep; Power button index AC=1 Sleep, DC=3 ShutDown:

```
Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)
  Subgroup GUID: 4f971e89-eebd-4455-a8de-9e59040e7347  (Power buttons and lid)
    Power Setting GUID: 5ca83367-6e45-459f-a27b-476b1d01c936  (Lid close action)
      Possible Setting Index: 000
      Possible Setting Friendly Name: Do nothing
      Possible Setting Index: 001
      Possible Setting Friendly Name: Sleep
      Possible Setting Index: 002
      Possible Setting Friendly Name: Hibernate
      Possible Setting Index: 003
      Possible Setting Friendly Name: Shut down
      Current AC Power Setting Index: 0x00000001
      Current DC Power Setting Index: 0x00000001
    Power Setting GUID: 7648efa3-dd9c-4e3e-b566-50f929386280  (Power button action)
      Possible Setting Index: 000
      Possible Setting Friendly Name: Do nothing
      Possible Setting Index: 001
      Possible Setting Friendly Name: Sleep
      Possible Setting Index: 002
      Possible Setting Friendly Name: Hibernate
      Possible Setting Index: 003
      Possible Setting Friendly Name: Shut down
      Possible Setting Index: 004
      Possible Setting Friendly Name: Turn off the display
      Current AC Power Setting Index: 0x00000001
      Current DC Power Setting Index: 0x00000003
    Power Setting GUID: 96996bc0-ad50-47ec-923b-6f41874dd9eb  (Sleep button action)
      Possible Setting Index: 000
      Possible Setting Friendly Name: Do nothing
      Possible Setting Index: 001
      Possible Setting Friendly Name: Sleep
      Current AC Power Setting Index: 0x00000001
      Current DC Power Setting Index: 0x00000001
```

- [ ] **Step 2: Wire the fixture into the test project's csproj**

Open `apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj`. Add a new `<ItemGroup>` after the existing one (before `</Project>`):

```xml
  <ItemGroup>
    <None Include="fixtures\*.txt">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
```

- [ ] **Step 3: Write the failing test**

Create `apps\BatteryTray\BatteryTray.Tests\HardwareActionsControllerParserTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using Xunit;

namespace BatteryTray.Tests;

public class HardwareActionsControllerParserTests
{
    private static string ReadFixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", name);
        return File.ReadAllText(path);
    }

    [Fact]
    public void Parse_BalancedPlanFixture_ReturnsExpectedSnapshot()
    {
        var fixture = ReadFixture("powercfg-sub-buttons-balanced.txt");

        var snapshot = HardwareActionsController.ParseSubButtonsQuery(fixture);

        snapshot.Should().NotBeNull();
        snapshot!.Value.LidCloseAc.Should().Be(HardwareAction.Sleep);
        snapshot.Value.LidCloseDc.Should().Be(HardwareAction.Sleep);
        snapshot.Value.PowerButtonAc.Should().Be(HardwareAction.Sleep);
        snapshot.Value.PowerButtonDc.Should().Be(HardwareAction.ShutDown);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsNull()
    {
        HardwareActionsController.ParseSubButtonsQuery("").Should().BeNull();
    }

    [Fact]
    public void Parse_MissingLidSection_ReturnsNull()
    {
        const string partial = """
            Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)
              Subgroup GUID: 4f971e89-eebd-4455-a8de-9e59040e7347  (Power buttons and lid)
                Power Setting GUID: 7648efa3-dd9c-4e3e-b566-50f929386280  (Power button action)
                  Current AC Power Setting Index: 0x00000001
                  Current DC Power Setting Index: 0x00000001
            """;

        HardwareActionsController.ParseSubButtonsQuery(partial).Should().BeNull(
            because: "missing lid close indices is unparseable; we return null rather than guess");
    }

    [Fact]
    public void Parse_MissingPowerButtonSection_ReturnsNull()
    {
        const string partial = """
            Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)
              Subgroup GUID: 4f971e89-eebd-4455-a8de-9e59040e7347  (Power buttons and lid)
                Power Setting GUID: 5ca83367-6e45-459f-a27b-476b1d01c936  (Lid close action)
                  Current AC Power Setting Index: 0x00000001
                  Current DC Power Setting Index: 0x00000001
            """;

        HardwareActionsController.ParseSubButtonsQuery(partial).Should().BeNull();
    }

    [Fact]
    public void Parse_ExtraWhitespace_StillSucceeds()
    {
        var fixture = ReadFixture("powercfg-sub-buttons-balanced.txt");
        var withExtraWhitespace = fixture.Replace("Current AC", "  Current  AC");

        var snapshot = HardwareActionsController.ParseSubButtonsQuery(withExtraWhitespace);

        snapshot.Should().NotBeNull(
            because: "the parser must tolerate the variable indentation powercfg may emit");
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dotnet test apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj --filter FullyQualifiedName~HardwareActionsControllerParserTests`
Expected: Build error — `ParseSubButtonsQuery` does not exist.

- [ ] **Step 5: Implement `ParseSubButtonsQuery` in `HardwareActionsController.cs`**

Add to the existing `HardwareActionsController.cs` (after `BuildCmdArgs`):

```csharp
/// <summary>
/// Parses output of `powercfg /q SCHEME_CURRENT SUB_BUTTONS`. Walks
/// line-by-line, holding the current setting GUID as state, and assigns
/// AC/DC ints to the matching field of a snapshot. Returns the snapshot
/// iff all four fields were populated; returns null if any are missing
/// (taken as "unparseable" rather than risking partial values).
///
/// Anchors on invariant GUIDs and the literal substrings
/// "Current AC Power Setting Index:" and "Current DC Power Setting Index:".
/// </summary>
internal static HardwareActionsSnapshot? ParseSubButtonsQuery(string powerCfgOutput)
{
    if (string.IsNullOrWhiteSpace(powerCfgOutput)) return null;

    int? pbAc = null, pbDc = null, lidAc = null, lidDc = null;
    Guid? currentSettingGuid = null;

    foreach (var rawLine in powerCfgOutput.Split('\n'))
    {
        var line = rawLine.Trim();
        if (line.Length == 0) continue;

        // Track which setting block we're in by detecting the GUID lines.
        if (line.StartsWith("Power Setting GUID:", StringComparison.OrdinalIgnoreCase))
        {
            // Format: "Power Setting GUID: <guid>  (Friendly Name)"
            var afterColon = line.Substring("Power Setting GUID:".Length).TrimStart();
            var spaceIdx = afterColon.IndexOf(' ');
            var guidStr = spaceIdx > 0 ? afterColon[..spaceIdx] : afterColon;
            currentSettingGuid = Guid.TryParse(guidStr, out var g) ? g : null;
            continue;
        }

        if (currentSettingGuid is null) continue;

        // Value lines look like: "Current AC Power Setting Index: 0x00000001"
        var isAc = line.StartsWith("Current AC Power Setting Index:", StringComparison.OrdinalIgnoreCase);
        var isDc = !isAc && line.StartsWith("Current DC Power Setting Index:", StringComparison.OrdinalIgnoreCase);
        if (!isAc && !isDc) continue;

        var colonIdx = line.IndexOf(':');
        if (colonIdx < 0) continue;

        var valuePart = line[(colonIdx + 1)..].Trim();
        // Accept both 0xNN and decimal forms.
        int? value = null;
        if (valuePart.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(valuePart[2..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var hex))
                value = hex;
        }
        else if (int.TryParse(valuePart, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var dec))
        {
            value = dec;
        }

        if (value is null) continue;

        if (currentSettingGuid == PButtonAction)
        {
            if (isAc) pbAc = value;
            else      pbDc = value;
        }
        else if (currentSettingGuid == LidAction)
        {
            if (isAc) lidAc = value;
            else      lidDc = value;
        }
        // Other setting GUIDs (e.g. sleep button) are intentionally ignored.
    }

    if (pbAc is null || pbDc is null || lidAc is null || lidDc is null)
        return null;

    return new HardwareActionsSnapshot(
        PowerButtonAc: (HardwareAction)pbAc.Value,
        PowerButtonDc: (HardwareAction)pbDc.Value,
        LidCloseAc:    (HardwareAction)lidAc.Value,
        LidCloseDc:    (HardwareAction)lidDc.Value);
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj --filter FullyQualifiedName~HardwareActionsControllerParserTests`
Expected: 5 tests pass.

- [ ] **Step 7: Commit**

```bash
git add apps/BatteryTray/BatteryTray/HardwareActionsController.cs apps/BatteryTray/BatteryTray.Tests/HardwareActionsControllerParserTests.cs apps/BatteryTray/BatteryTray.Tests/fixtures/powercfg-sub-buttons-balanced.txt apps/BatteryTray/BatteryTray.Tests/BatteryTray.Tests.csproj
git commit -m "BatteryTray: HardwareActionsController.ParseSubButtonsQuery + fixture

Parses output of \`powercfg /q SCHEME_CURRENT SUB_BUTTONS\`. Anchors on
invariant GUIDs and the value-line substrings (which are produced by
powercfg's own format strings and are stable across Windows 10/11
builds). Returns null on partial output rather than risk reporting
half-correct values.

Fixture is captured real output and lives under
apps/BatteryTray/BatteryTray.Tests/fixtures/ — copied to the test
output directory via a None CopyToOutputDirectory entry in the csproj.

The Sleep button section is present in the fixture but intentionally
ignored by the parser; it'll be a no-cost extension if Sleep button is
ever added to the feature scope."
```

---

## Task 7: `HardwareActionsController.ReadCurrent` + E2E test

`ReadCurrent` shells out to unelevated `powercfg /q SCHEME_CURRENT SUB_BUTTONS`, captures stdout, and feeds it to the parser. This requires a real powercfg invocation, so the meaningful test lives in the E2E project.

**Files:**
- Modify: `apps\BatteryTray\BatteryTray\HardwareActionsController.cs` (add `ReadCurrent`)
- Create: `apps\BatteryTray\BatteryTray.E2ETests\HardwareActionsControllerE2ETests.cs`

- [ ] **Step 1: Write the failing E2E test**

Create `apps\BatteryTray\BatteryTray.E2ETests\HardwareActionsControllerE2ETests.cs`:

```csharp
using FluentAssertions;
using Xunit;

namespace BatteryTray.E2ETests;

/// <summary>
/// Hits real powercfg.exe. Read path runs unelevated; ApplyToAllPlans
/// path is admin-gated and skipped when the test process is unelevated.
/// </summary>
public class HardwareActionsControllerE2ETests
{
    [WindowsFact]
    public void ReadCurrent_OnRealMachine_ReturnsPopulatedSnapshot()
    {
        var snapshot = HardwareActionsController.ReadCurrent();

        snapshot.Should().NotBeNull(
            because: "powercfg /q SCHEME_CURRENT SUB_BUTTONS works for standard users on every supported Windows build");
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test apps\BatteryTray\BatteryTray.E2ETests\BatteryTray.E2ETests.csproj --filter FullyQualifiedName~HardwareActionsControllerE2ETests`
Expected: Build error — `HardwareActionsController.ReadCurrent` does not exist.

- [ ] **Step 3: Implement `ReadCurrent` in `HardwareActionsController.cs`**

Add to the existing class (after `ParseSubButtonsQuery`):

```csharp
/// <summary>
/// Reads the current power-button and lid-close actions from the active
/// power scheme. Unelevated; powercfg /q works for standard users.
/// Returns null on any failure (timeout, non-zero exit, parser failure) —
/// callers should treat null as "feature unavailable" and disable the UI
/// with a friendly message.
/// </summary>
public static HardwareActionsSnapshot? ReadCurrent()
{
    var stdout = RunPowerCfg("/q SCHEME_CURRENT " + SubButtons);
    if (stdout is null) return null;

    var snapshot = ParseSubButtonsQuery(stdout);
    if (snapshot is null)
    {
        AppLogIfAvailable("hwactions.parse.failed", LogLevel.Warn, new { outputLength = stdout.Length });
    }
    return snapshot;
}

private static string? RunPowerCfg(string args)
{
    try
    {
        using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "powercfg.exe",
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });
        if (p is null) return null;
        var stdout = p.StandardOutput.ReadToEnd();
        p.WaitForExit(5000);
        return p.ExitCode == 0 ? stdout : null;
    }
    catch (Exception ex)
    {
        CrashLogger.Write("hwactions.read", ex);
        return null;
    }
}

// AppLog is created in Program.cs and not statically accessible here.
// We use a static delegate that Program wires up at startup. If unset
// (e.g. in tests), the call is a no-op.
internal static Action<string, LogLevel, object?>? LogSink;

internal enum LogLevel { Info, Warn }

private static void AppLogIfAvailable(string evt, LogLevel level, object? data)
{
    try { LogSink?.Invoke(evt, level, data); } catch { /* never let logging break the controller */ }
}
```

Note: `LogLevel` is intentionally a small local enum to avoid coupling the controller to `WindowsAppCore.AppLog`. `Program.cs` will wire `LogSink` to forward into the real AppLog. The wiring change to `Program.cs` happens in Task 10 where SettingsForm gets wired up.

- [ ] **Step 4: Run the E2E test to verify it passes**

Run: `dotnet test apps\BatteryTray\BatteryTray.E2ETests\BatteryTray.E2ETests.csproj --filter FullyQualifiedName~HardwareActionsControllerE2ETests`
Expected: 1 test passes (on Windows). On non-Windows CI it skips.

- [ ] **Step 5: Run the full BatteryTray.Tests suite to confirm no regression**

Run: `dotnet test apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj`
Expected: All previous tests still pass.

- [ ] **Step 6: Commit**

```bash
git add apps/BatteryTray/BatteryTray/HardwareActionsController.cs apps/BatteryTray/BatteryTray.E2ETests/HardwareActionsControllerE2ETests.cs
git commit -m "BatteryTray: HardwareActionsController.ReadCurrent + E2E coverage

Reads the current power-button and lid-close actions from the active
scheme via unelevated \`powercfg /q SCHEME_CURRENT SUB_BUTTONS\` and
feeds stdout to ParseSubButtonsQuery. Returns null on any failure mode
(process start error, non-zero exit, timeout, parser returned null);
callers treat null as 'feature unavailable' and disable the Settings
tab with a friendly message.

LogSink delegate seam lets Program.cs forward parse failures into the
real WindowsAppCore.AppLog without coupling the controller to the
logging infrastructure (which would make it hard to test in isolation).
The wiring happens later when the Settings dialog is wired up.

E2E test confirms the unelevated read path returns a populated snapshot
on a real Windows machine."
```

---

## Task 8: `HardwareActionsController.ApplyToAllPlans` + admin-gated E2E test

`ApplyToAllPlans` is the elevated write path: enumerate installed plans, call `BuildCmdArgs`, spawn cmd.exe via the `runas` verb, wait for exit, return `ApplyResult`.

**Files:**
- Modify: `apps\BatteryTray\BatteryTray\HardwareActionsController.cs` (add `ApplyToAllPlans` + `ApplyResult`)
- Modify: `apps\BatteryTray\BatteryTray.E2ETests\HardwareActionsControllerE2ETests.cs` (add admin-gated test)

- [ ] **Step 1: Write the failing admin-gated E2E test**

Open `apps\BatteryTray\BatteryTray.E2ETests\HardwareActionsControllerE2ETests.cs` and append:

```csharp
    private static bool IsElevated()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(identity)
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    [WindowsFact]
    public void ApplyToAllPlans_OnElevatedSession_RoundTripsThroughReadCurrent()
    {
        if (!IsElevated())
        {
            // Not an xUnit Skip (those need attribute-level config) — the test
            // succeeds as a no-op when unelevated, with a marker assertion that
            // shows up in the test output for transparency.
            true.Should().BeTrue(because: "skipped: requires admin to write powercfg values");
            return;
        }

        var original = HardwareActionsController.ReadCurrent();
        original.Should().NotBeNull(because: "baseline read must succeed before we mutate");

        try
        {
            var newPolicy = new HardwareActionPolicy
            {
                PowerButton          = HardwareAction.DoNothing,
                LidClose             = HardwareAction.Hibernate,
                DifferOnBattery      = true,
                PowerButtonOnBattery = HardwareAction.Sleep,
                LidCloseOnBattery    = HardwareAction.ShutDown,
            };

            var result = HardwareActionsController.ApplyToAllPlans(newPolicy);

            result.Ok.Should().BeTrue(because: "elevated apply on a real machine should succeed");

            var after = HardwareActionsController.ReadCurrent();
            after.Should().NotBeNull();
            after!.Value.PowerButtonAc.Should().Be(HardwareAction.DoNothing);
            after.Value.PowerButtonDc.Should().Be(HardwareAction.Sleep);
            after.Value.LidCloseAc.Should().Be(HardwareAction.Hibernate);
            after.Value.LidCloseDc.Should().Be(HardwareAction.ShutDown);
        }
        finally
        {
            // Restore original values regardless of outcome.
            if (original.HasValue)
            {
                var restore = new HardwareActionPolicy
                {
                    PowerButton          = original.Value.PowerButtonAc,
                    LidClose             = original.Value.LidCloseAc,
                    DifferOnBattery      = true,
                    PowerButtonOnBattery = original.Value.PowerButtonDc,
                    LidCloseOnBattery    = original.Value.LidCloseDc,
                };
                HardwareActionsController.ApplyToAllPlans(restore);
            }
        }
    }

    [WindowsFact]
    public void ApplyToAllPlans_OnUnelevatedSession_ReturnsFailureWithoutCrashing()
    {
        if (IsElevated())
        {
            // Mirror of the elevation gate above — we can only verify the
            // failure path when actually unelevated.
            true.Should().BeTrue(because: "skipped: requires NON-elevated session to exercise failure path");
            return;
        }

        var policy = new HardwareActionPolicy();
        var act = () => HardwareActionsController.ApplyToAllPlans(policy);

        act.Should().NotThrow(
            because: "the controller must surface elevation failure as ApplyResult.Ok=false, never an exception");
        // We don't assert Ok=false here because some Windows configurations let cmd.exe
        // launch without UAC (e.g. AlwaysInstallElevated policies) — the contract is
        // simply "no crash".
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test apps\BatteryTray\BatteryTray.E2ETests\BatteryTray.E2ETests.csproj --filter FullyQualifiedName~HardwareActionsControllerE2ETests`
Expected: Build error — `ApplyToAllPlans` and `ApplyResult` do not exist.

- [ ] **Step 3: Implement `ApplyResult` and `ApplyToAllPlans` in `HardwareActionsController.cs`**

Add to the existing class (after `ReadCurrent`):

```csharp
/// <summary>
/// Result of an <see cref="ApplyToAllPlans"/> call.
/// </summary>
public readonly record struct ApplyResult(bool Ok, string? FailureReason);

/// <summary>
/// Writes a HardwareActionPolicy to every installed power plan via a single
/// elevated cmd.exe invocation chained over powercfg. One UAC prompt per
/// call, regardless of plan count.
///
/// Returns ApplyResult with Ok=true on success, or Ok=false and a human
/// readable FailureReason for each failure mode (elevation declined,
/// powercfg non-zero exit, timeout, unexpected exception, no plans).
/// </summary>
public static ApplyResult ApplyToAllPlans(HardwareActionPolicy policy)
{
    var plans = PowerPlanController.List();
    if (plans.Count == 0)
    {
        AppLogIfAvailable("hwactions.apply.failed", LogLevel.Warn, new { reason = "no plans" });
        return new ApplyResult(false, "No power plans installed");
    }

    var planGuids = new Guid[plans.Count];
    for (int i = 0; i < plans.Count; i++) planGuids[i] = plans[i].Guid;

    var args = BuildCmdArgs(planGuids, policy);

    try
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = args,
            Verb = "runas",                                       // triggers UAC
            UseShellExecute = true,                                // required for Verb
            CreateNoWindow = true,
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var p = System.Diagnostics.Process.Start(psi);
        if (p is null)
        {
            AppLogIfAvailable("hwactions.apply.failed", LogLevel.Warn, new { reason = "Process.Start returned null" });
            return new ApplyResult(false, "Could not start elevated process");
        }

        if (!p.WaitForExit(30_000))
        {
            try { p.Kill(); } catch { /* best-effort */ }
            AppLogIfAvailable("hwactions.apply.failed", LogLevel.Warn, new { reason = "timeout" });
            return new ApplyResult(false, "powercfg timed out");
        }

        sw.Stop();
        if (p.ExitCode != 0)
        {
            AppLogIfAvailable("hwactions.apply.failed", LogLevel.Warn, new { reason = "non-zero exit", exitCode = p.ExitCode });
            return new ApplyResult(false, $"powercfg exited with code {p.ExitCode}");
        }

        AppLogIfAvailable("hwactions.applied", LogLevel.Info, new
        {
            policy,
            planCount = plans.Count,
            durationMs = sw.ElapsedMilliseconds,
        });
        return new ApplyResult(true, null);
    }
    catch (System.ComponentModel.Win32Exception wx) when (wx.NativeErrorCode == 1223)
    {
        // ERROR_CANCELLED — user clicked No on the UAC prompt.
        AppLogIfAvailable("hwactions.apply.failed", LogLevel.Warn, new { reason = "elevation declined" });
        return new ApplyResult(false, "Elevation declined");
    }
    catch (Exception ex)
    {
        CrashLogger.Write("hwactions.apply", ex);
        AppLogIfAvailable("hwactions.apply.failed", LogLevel.Warn, new { reason = ex.GetType().Name });
        return new ApplyResult(false, ex.Message);
    }
}
```

- [ ] **Step 4: Run the E2E tests to verify they pass**

Run: `dotnet test apps\BatteryTray\BatteryTray.E2ETests\BatteryTray.E2ETests.csproj --filter FullyQualifiedName~HardwareActionsControllerE2ETests`
Expected on an admin shell: 3 tests pass (`ReadCurrent`, `ApplyToAllPlans_OnElevatedSession`, `ApplyToAllPlans_OnUnelevatedSession` which is a no-op when elevated). The elevated round-trip test will pop a UAC prompt during the elevated apply; consent it.
Expected on a non-admin shell: 3 tests pass (`ReadCurrent` succeeds, `ApplyToAllPlans_OnElevatedSession` no-ops with `true.Should().BeTrue`, `ApplyToAllPlans_OnUnelevatedSession` runs and should be a no-throw).

- [ ] **Step 5: Run the full BatteryTray.Tests suite to confirm no regression**

Run: `dotnet test apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj`
Expected: All previous unit tests still pass.

- [ ] **Step 6: Commit**

```bash
git add apps/BatteryTray/BatteryTray/HardwareActionsController.cs apps/BatteryTray/BatteryTray.E2ETests/HardwareActionsControllerE2ETests.cs
git commit -m "BatteryTray: HardwareActionsController.ApplyToAllPlans + admin E2E

Writes a HardwareActionPolicy to every installed power plan via a single
elevated cmd.exe invocation chained over powercfg. One UAC prompt per
call, regardless of plan count. Returns ApplyResult with Ok=false plus
a human-readable FailureReason for every failure mode (elevation
declined via Win32 error 1223, non-zero exit, 30s timeout, Process.Start
returning null, no plans installed, unexpected exception).

The E2E test for the round-trip saves the original values, applies a
distinct-AC/DC test policy, reads back to verify, then restores in
finally. It skips no-op-style on unelevated sessions and exercises the
full elevated path otherwise."
```

---

## Task 9: SettingsForm — rename Power tab + add Hardware Actions tab structure

Splits the structural UI change (tab exists, dropdowns wired to the data source but no behaviour) from the load/save wiring (Task 10). This task focuses on layout and theming.

**Files:**
- Modify: `apps\BatteryTray\BatteryTray\SettingsForm.cs`

- [ ] **Step 1: Read the existing `BuildLayout()` and tab-construction code**

Run: `dotnet build apps\BatteryTray\BatteryTray\BatteryTray.csproj`
Expected: Baseline build succeeds before any changes.

Open `apps\BatteryTray\BatteryTray\SettingsForm.cs` and locate:
- The `tabPower` declaration (currently around line 76 with `var tabPower = new TabPage("Power");`)
- The `tabs.TabPages.AddRange(...)` call that registers tabs
- The fields block where existing controls are declared

- [ ] **Step 2: Add field declarations for the new tab's controls**

Find the existing `// Power` fields block (around line 37-40 in the current file). Insert a new block immediately after it:

```csharp
    // Hardware actions
    private ComboBox _hwPowerButtonAc       = null!;
    private ComboBox _hwLidCloseAc          = null!;
    private CheckBox _hwDifferOnBattery     = null!;
    private ComboBox _hwPowerButtonDc       = null!;
    private ComboBox _hwLidCloseDc          = null!;
    private Label    _hwOnBatteryPowerLabel = null!;
    private Label    _hwOnBatteryLidLabel   = null!;
    private Label    _hwDriftHint           = null!;
```

- [ ] **Step 3: Rename the Power tab and insert Hardware Actions tab**

Find:

```csharp
var tabPower         = new TabPage("Power");
```

Change to:

```csharp
var tabPower         = new TabPage("Power plans");
var tabHwActions     = new TabPage("Hardware actions");
```

Find the `AddRange` call (e.g. `tabs.TabPages.AddRange(new TabPage[] { tabGeneral, tabNotifications, tabAppearance, tabPower, tabSystem });`) and insert `tabHwActions` between `tabPower` and `tabSystem`:

```csharp
tabs.TabPages.AddRange(new TabPage[] { tabGeneral, tabNotifications, tabAppearance, tabPower, tabHwActions, tabSystem });
```

Find the `tabPower.Controls.Add(BuildPowerPanel());` line (or equivalent) and add one line below it:

```csharp
tabHwActions.Controls.Add(BuildHardwareActionsPanel());
```

- [ ] **Step 4: Add `BuildHardwareActionsPanel()` method**

Add to the `SettingsForm` class (place near the other `Build...Panel` methods):

```csharp
private TableLayoutPanel BuildHardwareActionsPanel()
{
    var panel = new TableLayoutPanel
    {
        Dock = DockStyle.Fill,
        Padding = new Padding(12),
        ColumnCount = 2,
        RowCount = 7,
        AutoSize = false,
    };
    panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
    panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
    for (int i = 0; i < 7; i++) panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

    var actionDataSource = BuildActionItems();

    _hwPowerButtonAc = MakeActionCombo(actionDataSource);
    _hwLidCloseAc    = MakeActionCombo(actionDataSource);

    panel.Controls.Add(new Label { Text = "Power button:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
    panel.Controls.Add(_hwPowerButtonAc, 1, 0);

    panel.Controls.Add(new Label { Text = "Lid close:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
    panel.Controls.Add(_hwLidCloseAc, 1, 1);

    _hwDifferOnBattery = new CheckBox
    {
        Text = "Use different action on battery",
        AutoSize = true,
        Anchor = AnchorStyles.Left,
    };
    panel.Controls.Add(_hwDifferOnBattery, 0, 2);
    panel.SetColumnSpan(_hwDifferOnBattery, 2);

    _hwOnBatteryPowerLabel = new Label { Text = "Power button on battery:", AutoSize = true, Anchor = AnchorStyles.Left, Visible = false };
    _hwPowerButtonDc       = MakeActionCombo(actionDataSource);
    _hwPowerButtonDc.Visible = false;
    panel.Controls.Add(_hwOnBatteryPowerLabel, 0, 3);
    panel.Controls.Add(_hwPowerButtonDc, 1, 3);

    _hwOnBatteryLidLabel = new Label { Text = "Lid close on battery:", AutoSize = true, Anchor = AnchorStyles.Left, Visible = false };
    _hwLidCloseDc        = MakeActionCombo(actionDataSource);
    _hwLidCloseDc.Visible = false;
    panel.Controls.Add(_hwOnBatteryLidLabel, 0, 4);
    panel.Controls.Add(_hwLidCloseDc, 1, 4);

    _hwDifferOnBattery.CheckedChanged += (_, _) =>
    {
        var on = _hwDifferOnBattery.Checked;
        _hwOnBatteryPowerLabel.Visible = on;
        _hwPowerButtonDc.Visible       = on;
        _hwOnBatteryLidLabel.Visible   = on;
        _hwLidCloseDc.Visible          = on;
    };

    _hwDriftHint = new Label
    {
        Text = "Windows values were changed outside BatteryTray. Saving will overwrite them.",
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Visible = false,
        ForeColor = SystemColors.GrayText,
    };
    panel.Controls.Add(_hwDriftHint, 0, 6);
    panel.SetColumnSpan(_hwDriftHint, 2);

    return panel;
}

private static System.Collections.Generic.List<HardwareActionItem> BuildActionItems() => new()
{
    new HardwareActionItem(HardwareAction.DoNothing,      "Do nothing"),
    new HardwareActionItem(HardwareAction.Sleep,          "Sleep"),
    new HardwareActionItem(HardwareAction.Hibernate,      "Hibernate"),
    new HardwareActionItem(HardwareAction.ShutDown,       "Shut down"),
    new HardwareActionItem(HardwareAction.TurnOffDisplay, "Turn off the display"),
};

private static ComboBox MakeActionCombo(System.Collections.Generic.List<HardwareActionItem> items)
{
    var combo = new ComboBox
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 220,
        Anchor = AnchorStyles.Left,
    };
    combo.DataSource = new System.Collections.Generic.List<HardwareActionItem>(items);
    combo.DisplayMember = nameof(HardwareActionItem.Display);
    combo.ValueMember   = nameof(HardwareActionItem.Value);
    return combo;
}

private sealed record HardwareActionItem(HardwareAction Value, string Display);
```

- [ ] **Step 5: Verify the form builds**

Run: `dotnet build apps\BatteryTray\BatteryTray\BatteryTray.csproj`
Expected: 0 warnings, 0 errors. The form now has the new tab but no behaviour wired up.

- [ ] **Step 6: Verify nothing else broke**

Run: `dotnet test apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj`
Expected: All unit tests pass.

- [ ] **Step 7: Commit**

```bash
git add apps/BatteryTray/BatteryTray/SettingsForm.cs
git commit -m "BatteryTray: SettingsForm structural changes for Hardware actions tab

Renames the existing 'Power' tab to 'Power plans' (no content change to
its panel) and inserts a new 'Hardware actions' tab between Power plans
and System.

The new tab's TableLayoutPanel has two AC dropdowns visible by default,
a 'Use different action on battery' checkbox that toggles visibility
of the two DC dropdowns, and a drift-hint Label that stays hidden in
this commit. The dropdowns are bound to a HardwareActionItem record
list mapping HardwareAction enum values to their display strings.

No load/save behaviour yet — those come in the next commit so the
diff stays reviewable."
```

---

## Task 10: SettingsForm — Load/Save wiring + Program.cs LogSink

Wires the new tab to read from the live powercfg state and the persisted policy on dialog open, and to apply via UAC on Save.

**Files:**
- Modify: `apps\BatteryTray\BatteryTray\SettingsForm.cs`
- Modify: `apps\BatteryTray\BatteryTray\Program.cs` (set the LogSink delegate)

- [ ] **Step 1: Read the existing `LoadValues()` and save handler**

Inspect `apps\BatteryTray\BatteryTray\SettingsForm.cs` for:
- The `LoadValues()` method (around line 60+)
- The save handler (typically an `OnOkClicked` or similar method that sets fields back on `_settings`, calls `_settings.Save()`, raises `SettingsSaved`, then closes the form)

- [ ] **Step 2: Add `LoadHardwareActions()` and call it from `LoadValues()`**

Append to the `SettingsForm` class:

```csharp
private HardwareActionsSnapshot? _hwLiveSnapshot;

private void LoadHardwareActions()
{
    _hwLiveSnapshot = HardwareActionsController.ReadCurrent();

    if (_hwLiveSnapshot is null)
    {
        // powercfg failed — disable the whole tab and surface the reason inline.
        _hwPowerButtonAc.Enabled       = false;
        _hwLidCloseAc.Enabled          = false;
        _hwDifferOnBattery.Enabled     = false;
        _hwPowerButtonDc.Enabled       = false;
        _hwLidCloseDc.Enabled          = false;
        _hwDriftHint.Text              = "Couldn't read current Windows power settings.";
        _hwDriftHint.Visible           = true;
        return;
    }

    var live = _hwLiveSnapshot.Value;
    var persisted = _settings.HardwareActions;

    HardwareAction acPb, acLid, dcPb, dcLid;
    bool differ;

    if (persisted is null)
    {
        // First-run-on-this-feature: populate from the live Windows values.
        acPb   = live.PowerButtonAc;
        dcPb   = live.PowerButtonDc;
        acLid  = live.LidCloseAc;
        dcLid  = live.LidCloseDc;
        differ = (acPb != dcPb) || (acLid != dcLid);
        _hwDriftHint.Visible = false;
    }
    else
    {
        // Re-opening: populate from the persisted policy.
        acPb   = persisted.PowerButton;
        acLid  = persisted.LidClose;
        differ = persisted.DifferOnBattery;
        dcPb   = persisted.DifferOnBattery ? persisted.PowerButtonOnBattery : persisted.PowerButton;
        dcLid  = persisted.DifferOnBattery ? persisted.LidCloseOnBattery    : persisted.LidClose;

        // Show drift hint iff any live value disagrees with the policy's effective value.
        var drift =
            live.PowerButtonAc != acPb ||
            live.PowerButtonDc != dcPb ||
            live.LidCloseAc    != acLid ||
            live.LidCloseDc    != dcLid;
        _hwDriftHint.Visible = drift;
    }

    _hwPowerButtonAc.SelectedValue = acPb;
    _hwLidCloseAc.SelectedValue    = acLid;
    _hwDifferOnBattery.Checked     = differ;
    _hwPowerButtonDc.SelectedValue = dcPb;
    _hwLidCloseDc.SelectedValue    = dcLid;
    _hwOnBatteryPowerLabel.Visible = differ;
    _hwPowerButtonDc.Visible       = differ;
    _hwOnBatteryLidLabel.Visible   = differ;
    _hwLidCloseDc.Visible          = differ;
}
```

Find the existing `LoadValues()` method and add a call to `LoadHardwareActions();` at the bottom (or wherever per-tab loaders are called).

- [ ] **Step 3: Add `TryApplyHardwareActions()` and integrate into the save path**

Append to the `SettingsForm` class:

```csharp
/// <summary>
/// Reads the new policy from the dropdowns, decides whether a powercfg
/// apply is needed, and runs it if so. Returns true if the dialog should
/// proceed to persist _settings (either no change was needed or apply
/// succeeded). Returns false to abort the save (apply failed or user
/// declined UAC) — caller must NOT call _settings.Save() in that case.
/// </summary>
private bool TryApplyHardwareActions(Button saveButton)
{
    if (_hwLiveSnapshot is null)
    {
        // Tab was disabled because we couldn't read — leave _settings.HardwareActions
        // untouched and let the rest of the save proceed.
        return true;
    }

    var candidate = new HardwareActionPolicy
    {
        PowerButton          = (HardwareAction)_hwPowerButtonAc.SelectedValue!,
        LidClose             = (HardwareAction)_hwLidCloseAc.SelectedValue!,
        DifferOnBattery      = _hwDifferOnBattery.Checked,
        PowerButtonOnBattery = (HardwareAction)_hwPowerButtonDc.SelectedValue!,
        LidCloseOnBattery    = (HardwareAction)_hwLidCloseDc.SelectedValue!,
    };

    var live = _hwLiveSnapshot.Value;
    var liveMatchesCandidate =
        live.PowerButtonAc == candidate.PowerButton &&
        live.LidCloseAc    == candidate.LidClose &&
        live.PowerButtonDc == (candidate.DifferOnBattery ? candidate.PowerButtonOnBattery : candidate.PowerButton) &&
        live.LidCloseDc    == (candidate.DifferOnBattery ? candidate.LidCloseOnBattery    : candidate.LidClose);

    var persistedMatchesCandidate = HardwareActionPolicyEquals(_settings.HardwareActions, candidate);

    if (liveMatchesCandidate && persistedMatchesCandidate)
    {
        return true; // nothing to do
    }

    var originalText = saveButton.Text;
    saveButton.Enabled = false;
    saveButton.Text = "Applying...";
    try
    {
        var result = HardwareActionsController.ApplyToAllPlans(candidate);
        if (!result.Ok)
        {
            MessageBox.Show(
                this,
                $"Couldn't apply hardware action settings: {result.FailureReason}. Other settings were not saved.",
                "BatteryTray",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        _settings.HardwareActions = candidate;
        _hwLiveSnapshot = HardwareActionsController.ReadCurrent(); // refresh for any subsequent re-open
        _hwDriftHint.Visible = false;
        return true;
    }
    finally
    {
        saveButton.Enabled = true;
        saveButton.Text = originalText;
    }
}

private static bool HardwareActionPolicyEquals(HardwareActionPolicy? a, HardwareActionPolicy b)
{
    if (a is null) return false;
    return a.PowerButton          == b.PowerButton
        && a.LidClose             == b.LidClose
        && a.DifferOnBattery      == b.DifferOnBattery
        && a.PowerButtonOnBattery == b.PowerButtonOnBattery
        && a.LidCloseOnBattery    == b.LidCloseOnBattery;
}
```

- [ ] **Step 4: Wire `TryApplyHardwareActions` into the save handler**

Locate the existing save handler (the method invoked when the Save / OK button is clicked, which currently reads fields back into `_settings` and calls `_settings.Save()`). Identify the Save button reference (likely a local var inside `BuildLayout` such as `var saveBtn = ...` or a class field).

If the Save button is a local var, lift it to a class field first:

```csharp
private Button _saveBtn = null!;
```

And change the local construction to `_saveBtn = new Button { Text = "&Save", ... };`.

Then at the top of the save handler, add the gate:

```csharp
if (!TryApplyHardwareActions(_saveBtn))
{
    return; // abort save entirely
}
```

This must run BEFORE the rest of the handler reads the other tabs' values back into `_settings` and BEFORE `_settings.Save()`. If `TryApplyHardwareActions` returns false, the entire dialog save is aborted — `_settings` is not persisted at all, the dialog stays open.

- [ ] **Step 5: Wire `Program.cs` to forward into the real AppLog**

Find the `WindowsAppCore.AppLog` field in `apps\BatteryTray\BatteryTray\Program.cs` (or wherever the log is constructed near the top of `Main`). Immediately after construction, register the controller's LogSink:

```csharp
HardwareActionsController.LogSink = (evt, level, data) =>
{
    switch (level)
    {
        case HardwareActionsController.LogLevel.Info: log.Info(evt, data);  break;
        case HardwareActionsController.LogLevel.Warn: log.Warn(evt, data);  break;
    }
};
```

Where `log` is the existing `AppLog` instance. If the local variable has a different name, substitute. If `AppLog.Info` / `AppLog.Warn` have different signatures in this codebase, adapt the bodies to match — the contract is just "if the controller emits a log event, forward it to AppLog".

- [ ] **Step 6: Build and run the unit tests**

Run: `dotnet build apps\BatteryTray\BatteryTray\BatteryTray.csproj`
Expected: 0 warnings, 0 errors.

Run: `dotnet test apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj`
Expected: All unit tests pass. No tests regressed.

- [ ] **Step 7: Smoke launch (manual)**

Run: `dotnet run --project apps\BatteryTray\BatteryTray\BatteryTray.csproj` (or `dotnet build` followed by launching the produced exe)
Expected:
- Tray icon appears
- Right-click -> Settings opens the form
- Power plans tab exists (renamed from Power) with auto-switch controls
- Hardware actions tab exists with two dropdowns visible, checkbox unchecked, on-battery rows hidden
- Toggling the checkbox shows/hides the on-battery rows
- Dropdowns are populated with five options

Close the app via tray-menu Exit (do NOT click Save yet — the Save path will be smoke-tested in Task 11).

- [ ] **Step 8: Commit**

```bash
git add apps/BatteryTray/BatteryTray/SettingsForm.cs apps/BatteryTray/BatteryTray/Program.cs
git commit -m "BatteryTray: wire Hardware actions tab to load/save flow

LoadHardwareActions populates dropdowns from the persisted policy if
present, else from the live powercfg snapshot. Sets the drift hint
when the live state disagrees with the persisted policy. Disables the
whole tab if powercfg /q failed.

TryApplyHardwareActions reads dropdowns into a candidate policy,
short-circuits if both the live state and persisted policy already
match it, otherwise spawns the elevated apply through the UAC prompt.
On apply failure (declined UAC, non-zero powercfg exit, timeout) it
aborts the entire dialog save — no settings are persisted from this
dialog open. On success the policy is written to _settings and the
existing save path proceeds to persist all tabs together.

Program.cs wires HardwareActionsController.LogSink to forward into the
existing AppLog instance so apply/parse events show up in the JSONL
log file alongside the other lifecycle events."
```

---

## Task 11: Final smoke, full test suite, WORKLOG seal

End-to-end verification on a real machine plus the WORKLOG entry.

**Files:**
- Modify: `WORKLOG.md`

- [ ] **Step 1: Full build of every project**

Run: `dotnet build`
Expected: 0 warnings, 0 errors across all four apps and the shared libraries.

- [ ] **Step 2: Full test suite (excluding E2E)**

Run: `dotnet test apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj`
Run: `dotnet test shared\WindowsAppCore.Tests\WindowsAppCore.Tests.csproj`
Run: `dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj`
Run: `dotnet test apps\NetProfileSwitcher.Tests\NetProfileSwitcher.Tests.csproj`
Run: `dotnet test apps\SoundTracker\SoundTracker.Tests\SoundTracker.Tests.csproj`
Run: `dotnet test apps\ProgramHider\tests\ProgramHider.Tests\ProgramHider.Tests.csproj`
Expected: All tests pass. Record the totals.

- [ ] **Step 3: BatteryTray E2E (admin shell)**

In an **elevated** PowerShell:

Run: `dotnet test apps\BatteryTray\BatteryTray.E2ETests\BatteryTray.E2ETests.csproj`
Expected:
- The new `HardwareActionsControllerE2ETests` pass
- A UAC prompt MAY appear during `ApplyToAllPlans_OnElevatedSession_RoundTripsThroughReadCurrent` (since the test calls `ApplyToAllPlans` which spawns cmd.exe with `Verb=runas`; even from an already-elevated process, `runas` may show a one-time confirmation on some Windows configurations — consent it)
- Final state of the system: original power-button and lid-close values, restored by the test's `finally` block

- [ ] **Step 4: Manual smoke**

Launch the BatteryTray exe from the build output:

Run: `apps\BatteryTray\BatteryTray\bin\Debug\net8.0-windows10.0.19041.0\BatteryTray.exe`

Then:
1. Tray right-click -> Settings -> Hardware actions tab
2. Note current values
3. Change Power button to "Do nothing", Lid close to "Sleep"
4. Click Save -> UAC prompt -> consent
5. Confirm dialog closes cleanly
6. Re-open Settings -> Hardware actions -> values match what you set
7. Close laptop lid -> system sleeps
8. Wake; press power button -> nothing happens (DoNothing in effect)
9. Re-open Settings -> Hardware actions -> set Power button back to "Sleep"
10. Click Save -> UAC -> consent
11. Verify by checking Control Panel -> Power Options -> "Choose what the power button does" matches

- [ ] **Step 5: Write WORKLOG entry**

Open `WORKLOG.md`. Find the entry added in the spec commit (`Phase 27 design`) and append a new dated entry immediately below it (between that entry and `## Phase Checklist`):

```markdown
## 2026-05-14

**Did:** Phase 27 implementation — configurable power-button and lid-close actions for BatteryTray.
- `HardwareAction` enum (matches powercfg integer indices), `HardwareActionsSnapshot` record struct, `HardwareActionPolicy` persistence class
- `HardwareActionsController`: `BuildCmdArgs` (pure args builder), `ParseSubButtonsQuery` (powercfg /q parser), `ReadCurrent` (unelevated read), `ApplyToAllPlans` (elevated write via single cmd.exe runas chaining every required powercfg call). Failure modes (declined UAC via Win32 1223, non-zero exit, timeout, parser failure) all surface as `ApplyResult.Ok=false` with a human-readable reason.
- `AppSettings` schema bump v3 -> v4. `AppSettingsMigrationV3ToV4` no-op preserves all v3 fields and lands HardwareActions=null (sentinel for "user has never configured this").
- `SettingsForm`: Power tab renamed to "Power plans"; new "Hardware actions" tab with two AC dropdowns, "Use different action on battery" checkbox revealing two DC dropdowns, and a drift-hint Label that surfaces when the live Windows state disagrees with the persisted policy. Save flow short-circuits if no change is needed; otherwise triggers UAC, applies, and aborts the whole dialog save on apply failure.
- `Program.cs` wires `HardwareActionsController.LogSink` into the existing `AppLog` so `hwactions.applied` / `hwactions.apply.failed` / `hwactions.parse.failed` events land in the JSONL log alongside the rest of the lifecycle.
- Logs and elevation handled per spec. One UAC prompt per Save regardless of plan count.

**Tests:** unit (policy roundtrip, migration v3->v4, integration, args builder, parser fixture) plus E2E (real powercfg read; admin-gated round-trip apply with restore in finally).

**Committed:** (see git log between the spec commit and this entry)

**Next:** Phase 28 (tbd)
```

(Replace `Phase 28 (tbd)` with the actual next item if known.)

- [ ] **Step 6: Final commit**

```bash
git add WORKLOG.md
git commit -m "Worklog: Phase 27 implementation complete (Hardware actions feature)

Wraps up the implementation series for the configurable power-button
and lid-close actions feature. Full BatteryTray.Tests suite green
including the new HardwareActionPolicy, AppSettingsMigrationV3ToV4,
HardwareActionsControllerArgsBuilder and HardwareActionsControllerParser
test classes. E2E read path verified on real hardware; elevated apply
round-trip exercised end-to-end with restore-in-finally.

Manual smoke confirmed: close lid -> Sleep, power button -> Do Nothing,
revert via dialog -> Windows Control Panel reflects the change."
```

---

## Self-Review Notes (run by the plan author after writing)

**Spec coverage check:**
- All five locked decisions (scope, plan model, AC/DC split, elevation, UI placement) and the powercfg-shell-out implementation approach are covered by Tasks 1-11.
- Settings schema v3->v4 with `HardwareActions` nullable property: Tasks 2-4.
- Argument builder including the `&&` chaining detail: Task 5.
- Parser including the GUID-anchored, value-line-substring approach: Task 6.
- ReadCurrent unelevated path: Task 7.
- ApplyToAllPlans elevated path with all five failure modes (1223 declined, non-zero exit, 30s timeout, Process.Start null, unexpected exception): Task 8.
- UI rename + new tab + load/save with drift hint and apply-on-Save: Tasks 9-10.
- Logging events (`hwactions.read`, `hwactions.applied`, `hwactions.apply.failed`, `hwactions.parse.failed`): wired via Tasks 7-10. Note: `hwactions.read` is the one event in the spec not explicitly emitted by this plan; it's covered implicitly by `ReadCurrent` returning a snapshot. If the user wants the explicit `hwactions.read` info event, it can be added with a one-liner in `ReadCurrent`. Worth flagging during review.
- Unit and E2E test coverage: Tasks 2, 3, 4, 5, 6, 7, 8.
- Manual smoke checklist: Task 11.

**Placeholder scan:** No "TBD", "TODO", "fill in later", "similar to Task N" markers. Every step has complete code or a complete command with expected output.

**Type consistency:**
- `HardwareAction` enum used identically across all tasks.
- `HardwareActionsSnapshot` field names (`PowerButtonAc`, `PowerButtonDc`, `LidCloseAc`, `LidCloseDc`) used consistently in Tasks 6, 7, 8, 10.
- `HardwareActionPolicy` field names (`PowerButton`, `LidClose`, `DifferOnBattery`, `PowerButtonOnBattery`, `LidCloseOnBattery`) used consistently in Tasks 2, 4, 5, 8, 10.
- Controller method names (`BuildCmdArgs`, `ParseSubButtonsQuery`, `ReadCurrent`, `ApplyToAllPlans`) used consistently.
- `ApplyResult.Ok` / `ApplyResult.FailureReason` field names used consistently in Tasks 8, 10.

**Single deviation from spec (documented):** spec says "hwactions.read Info" event on dialog open; the plan emits only `hwactions.parse.failed` (Warn) from `ReadCurrent`. The dialog-open Info event is a minor omission; the executing session may add a single-line `AppLogIfAvailable("hwactions.read", LogLevel.Info, snapshot)` at the end of `ReadCurrent` if desired.

---

## Execution

This plan is intended for execution in a **fresh session** with elevated permissions, started by the user. See the companion handoff brief at `docs/specs/2026-05-14-power-button-lid-actions-kickoff.md` for the prompt that boots the executing session.
