# BatteryTray: Configurable Power-Button and Lid-Close Actions

**Status:** Design approved 2026-05-14. Implementation pending.
**Scope:** BatteryTray app only. No changes to other apps or to `WindowsAppCore` / `WindowsTrayCore`.

## Summary

Adds a settings surface to BatteryTray that lets the user choose what Windows does when:

1. The hardware power button is pressed
2. The laptop lid is closed

The user picks from the standard Windows action set: Do Nothing, Sleep, Hibernate, Shut Down, Turn Off Display. Values are applied to every installed power plan so the choice survives Windows' active-plan switching (manual or via the existing `PowerPlanAutoSwitch`). A single UAC prompt is incurred per Save.

## Context

BatteryTray already wraps `powercfg.exe` in `PowerPlanController` (read-only `/list`, `/getactivescheme`; write `/setactive`). It runs unelevated. Power-button and lid-close actions are stored per power plan with separate AC and DC indices; modifying them requires admin. The existing settings dialog (`SettingsForm`) is tabbed (General / Notifications / Appearance / Power / System) with the "Power" tab currently hosting the AC vs Battery plan auto-switch controls.

## Locked Decisions

| Axis | Decision |
|---|---|
| Configurable hardware events | Power button + Lid close |
| Available actions on each | Do Nothing, Sleep, Hibernate, Shut Down, Turn Off Display (5 values, integer indices match powercfg) |
| Plan model | Apply preference to **all** installed plans at Save time (functional equivalent of "global override" without runtime enforcement) |
| AC / DC split | Single dropdown per action by default; **Use different action on battery** checkbox reveals second dropdowns |
| Elevation | UAC on Save only. Batch every required write into a single elevated `cmd.exe` invocation via `runas`. BatteryTray itself stays unelevated. |
| UI placement | New **Hardware actions** tab in `SettingsForm`. Existing **Power** tab renamed to **Power plans**. |
| Implementation | `powercfg.exe` shell-out, matching the existing `PowerPlanController` idiom. No P/Invoke. No new shared library. |

## Architecture

### New types (all in `apps\BatteryTray\BatteryTray\`)

#### `HardwareAction.cs`

```csharp
namespace BatteryTray;

public enum HardwareAction
{
    DoNothing      = 0,
    Sleep          = 1,
    Hibernate      = 2,
    ShutDown       = 3,
    TurnOffDisplay = 4,
}
```

Integer values match the Windows powercfg index for each action. The enum value **is** the index passed to `/setacvalueindex` / `/setdcvalueindex`.

#### `HardwareActionPolicy.cs`

```csharp
namespace BatteryTray;

public sealed class HardwareActionPolicy
{
    public HardwareAction PowerButton { get; set; } = HardwareAction.Sleep;
    public HardwareAction LidClose    { get; set; } = HardwareAction.Sleep;

    public bool DifferOnBattery { get; set; }
    public HardwareAction PowerButtonOnBattery { get; set; } = HardwareAction.Sleep;
    public HardwareAction LidCloseOnBattery    { get; set; } = HardwareAction.Hibernate;
}
```

Mutable class (not record) to match the existing `AppSettings` / `JsonSettingsStore<T>` pattern. When `DifferOnBattery` is false, the AC values are written to both AC and DC powercfg indices on Save.

#### `HardwareActionsSnapshot.cs`

```csharp
namespace BatteryTray;

public readonly record struct HardwareActionsSnapshot(
    HardwareAction PowerButtonAc,
    HardwareAction PowerButtonDc,
    HardwareAction LidCloseAc,
    HardwareAction LidCloseDc);
```

Read-only return type for `HardwareActionsController.ReadCurrent()`.

#### `HardwareActionsController.cs`

Static class. Three responsibilities, each independently testable.

**Constants:**

```csharp
public static readonly Guid SubButtons    = new("4f971e89-eebd-4455-a8de-9e59040e7347");
public static readonly Guid PButtonAction = new("7648efa3-dd9c-4e3e-b566-50f929386280");
public static readonly Guid LidAction     = new("5ca83367-6e45-459f-a27b-476b1d01c936");
```

**Public surface:**

```csharp
public static HardwareActionsSnapshot? ReadCurrent();
public static ApplyResult ApplyToAllPlans(HardwareActionPolicy policy);
```

**Internal seam for testing:**

```csharp
internal static string BuildCmdArgs(IReadOnlyList<Guid> planGuids, HardwareActionPolicy policy);
internal static HardwareActionsSnapshot? ParseSubButtonsQuery(string powerCfgOutput);
```

`ApplyResult`:

```csharp
public readonly record struct ApplyResult(bool Ok, string? FailureReason);
```

### Data flow

**Dialog open:**

1. `SettingsForm.LoadValues()` calls `HardwareActionsController.ReadCurrent()`. Unelevated `powercfg /q SCHEME_CURRENT SUB_BUTTONS` is parsed into a `HardwareActionsSnapshot`.
2. Decision tree:
   - `_settings.HardwareActions == null`: populate dropdowns from the live snapshot. Hint label hidden. `DifferOnBattery` checkbox initialised from whether live AC ≠ DC for either action.
   - `_settings.HardwareActions != null`: populate dropdowns from the persisted policy. If any live value disagrees with the corresponding policy value, show the drift hint.
   - `ReadCurrent()` returns null: disable the whole tab, show an error label, keep the rest of the dialog functional.

**Save:**

1. `SaveValues()` reads dropdowns into a new `HardwareActionPolicy` instance.
2. Compares to `_settings.HardwareActions`. If unchanged **and** the live snapshot matches, skip the powercfg dance entirely.
3. Otherwise calls `HardwareActionsController.ApplyToAllPlans(policy)`:
   - Builds the chained `cmd /c` arg string covering every installed plan
   - Spawns elevated via `ProcessStartInfo { FileName = "cmd.exe", Verb = "runas", UseShellExecute = true, CreateNoWindow = true, WindowStyle = Hidden }`
   - Waits up to 30 s for exit
   - Returns `ApplyResult`
4. On success: assign `_settings.HardwareActions = policy`, continue normal `_settings.Save()`, close dialog. Log `hwactions.applied`.
5. On failure: `MessageBox` with the failure reason, dialog stays open, other tabs' changes are **not** persisted (single transaction). Log `hwactions.apply.failed`.

### Failure modes

| Mode | Detection | Behaviour |
|---|---|---|
| User declines UAC | `Win32Exception.NativeErrorCode == 1223` | `Ok=false, FailureReason="Elevation declined"` |
| powercfg non-zero exit | `Process.ExitCode != 0` | `Ok=false, FailureReason="powercfg exited with code {n}"` |
| 30 s timeout | `WaitForExit(30000)` returns false | Kill cmd.exe, `Ok=false, FailureReason="powercfg timed out"` |
| Unexpected exception (Process.Start blows up) | catch all | `CrashLogger.Write("hwactions.apply", ex)`, `Ok=false, FailureReason=ex.Message` |
| powercfg /q parsing fails (e.g. malformed output) | `ParseSubButtonsQuery` returns null | `ReadCurrent()` returns null, tab disabled with friendly error |

## Settings schema (v3 -> v4)

**`AppSettings` change:**

```csharp
public const int CurrentSchemaVersion = 4;

// ---- Hardware actions ----
// Null = user has never configured this; dialog shows live Windows values.
// Non-null = user's last saved preference.
public HardwareActionPolicy? HardwareActions { get; set; }
```

**Migration:** `Settings\AppSettingsMigrationV3ToV4.cs`. Functional no-op (no field rename or value remap). It exists to bump `SchemaVersion` to 4 and preserve the migration-chain pattern used by V1->V2 and V2->V3. Loading a v3 file produces a fully-populated v4 instance with `HardwareActions == null`.

**`AppSettings.Store` registration** adds the new migration after V2ToV3:

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

## UI behaviour

### SettingsForm structural changes

1. Rename `tabPower` text from `"Power"` to `"Power plans"`. No content changes to the plan auto-switch panel.
2. Insert new `tabHwActions = new TabPage("Hardware actions")` between `tabPower` and `tabSystem` in `BuildLayout()`.
3. Add `BuildHardwareActionsPanel()` returning a `TableLayoutPanel` themed via `WindowsTrayCore.ThemeApplier`.

### Hardware actions panel layout

```
Power button:           [Sleep         v]
Lid close:              [Sleep         v]

[ ] Use different action on battery

  Power button on battery:  [Hibernate  v]   <- hidden until checkbox set
  Lid close on battery:     [Hibernate  v]   <- hidden until checkbox set

(i) <hint label, normally hidden>             <- bottom of panel
```

### Dropdown bindings

Each `ComboBox`:
- `DropDownStyle = DropDownList`
- DataSource: a small static `IReadOnlyList<(HardwareAction Value, string Display)>` defined on the form. Display strings: `"Do nothing"`, `"Sleep"`, `"Hibernate"`, `"Shut down"`, `"Turn off the display"`.
- `DisplayMember = "Display"`, `ValueMember = "Value"`

### Differ-on-battery toggle

- Checkbox `CheckedChanged` handler toggles `Visible` on the two "on battery" combo rows
- Tab height is fixed via `RowStyle` rows reserved for the hidden controls, so the form does not resize when the checkbox is flipped
- When unchecked, on Save the AC values are written to **both** AC and DC powercfg indices

### Drift hint

A single `Label` at the bottom of the panel, normally hidden. Text when shown: `"Windows values were changed outside BatteryTray. Saving will overwrite them."` Shown only when `_settings.HardwareActions != null` and any field of the live snapshot disagrees with the corresponding field of the persisted policy.

### Save UX adjustments

`SettingsForm.SaveValues()` (or whatever the existing OK-button handler is named) gets a tri-state hardware-actions check at the top:

1. Build candidate `HardwareActionPolicy` from the dropdowns
2. If `policy.Equals(_settings.HardwareActions)` and `policy.Equals(live snapshot)`: nothing to do, skip step 3 and 4
3. Disable Save button, change its text to `"Applying..."`, call `ApplyToAllPlans` synchronously (it's short; no need for `async void` plumbing here)
4. Re-enable Save with original text. If `Ok==false`: show MessageBox, **return** without saving anything else. If `Ok==true`: assign `_settings.HardwareActions = policy` and fall through to the existing save path.

This keeps the UI thread blocked for the 1-3 s the elevated powercfg takes. Acceptable: the user just clicked Save expecting persistence.

### Theme integration

The new tab uses the same `WindowsTrayCore.ThemeApplier` call already used by the other tabs. No new theme code.

## Argument-builder spec

`HardwareActionsController.BuildCmdArgs(planGuids, policy)` produces the string passed to `cmd.exe /c`. For each plan `P` in `planGuids`, the builder emits four powercfg invocations chained with `&&` (abort-on-first-failure semantics, so the elevated process's exit code reflects the first powercfg failure rather than only the last):

```
powercfg /setacvalueindex {P} 4f971e89-eebd-4455-a8de-9e59040e7347 7648efa3-dd9c-4e3e-b566-50f929386280 {acPower}
powercfg /setdcvalueindex {P} 4f971e89-eebd-4455-a8de-9e59040e7347 7648efa3-dd9c-4e3e-b566-50f929386280 {dcPower}
powercfg /setacvalueindex {P} 4f971e89-eebd-4455-a8de-9e59040e7347 5ca83367-6e45-459f-a27b-476b1d01c936 {acLid}
powercfg /setdcvalueindex {P} 4f971e89-eebd-4455-a8de-9e59040e7347 5ca83367-6e45-459f-a27b-476b1d01c936 {dcLid}
```

After all per-plan writes, a final `&& powercfg /setactive SCHEME_CURRENT` refreshes the live state.

Where:
- `{acPower}` = `(int)policy.PowerButton`
- `{dcPower}` = `(int)(policy.DifferOnBattery ? policy.PowerButtonOnBattery : policy.PowerButton)`
- `{acLid}`   = `(int)policy.LidClose`
- `{dcLid}`   = `(int)(policy.DifferOnBattery ? policy.LidCloseOnBattery : policy.LidClose)`

The chained string is then prefixed with `/c "` and suffixed with `"`, passed as `cmd.exe`'s `Arguments`.

Final arg string for one plan, `DifferOnBattery=false`, `PowerButton=Sleep`, `LidClose=Hibernate`:

```
/c "powercfg /setacvalueindex 381b4222-f694-41f0-9685-ff5bb260df2e 4f971e89-eebd-4455-a8de-9e59040e7347 7648efa3-dd9c-4e3e-b566-50f929386280 1 && powercfg /setdcvalueindex 381b4222-f694-41f0-9685-ff5bb260df2e 4f971e89-eebd-4455-a8de-9e59040e7347 7648efa3-dd9c-4e3e-b566-50f929386280 1 && powercfg /setacvalueindex 381b4222-f694-41f0-9685-ff5bb260df2e 4f971e89-eebd-4455-a8de-9e59040e7347 5ca83367-6e45-459f-a27b-476b1d01c936 2 && powercfg /setdcvalueindex 381b4222-f694-41f0-9685-ff5bb260df2e 4f971e89-eebd-4455-a8de-9e59040e7347 5ca83367-6e45-459f-a27b-476b1d01c936 2 && powercfg /setactive SCHEME_CURRENT"
```

## Parser spec

`HardwareActionsController.ParseSubButtonsQuery(powerCfgOutput)` parses output of `powercfg /q SCHEME_CURRENT SUB_BUTTONS`. Sample (English locale):

```
Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)
  Subgroup GUID: 4f971e89-eebd-4455-a8de-9e59040e7347  (Power buttons and lid)
    Power Setting GUID: 5ca83367-6e45-459f-a27b-476b1d01c936  (Lid close action)
      Possible Setting Index: 000
      ...
      Current AC Power Setting Index: 0x00000001
      Current DC Power Setting Index: 0x00000001
    Power Setting GUID: 7648efa3-dd9c-4e3e-b566-50f929386280  (Power button action)
      ...
      Current AC Power Setting Index: 0x00000001
      Current DC Power Setting Index: 0x00000003
```

Parser walks line-by-line, holding the "current setting GUID" state, and assigns AC/DC ints to the matching field of a builder snapshot. Returns the snapshot iff all four fields were populated; returns null if any are missing (taken as "unparseable" rather than risking partial values).

The parser primarily anchors on GUIDs (invariant across locales) and on the literal substrings `"Current AC Power Setting Index:"` and `"Current DC Power Setting Index:"`. Those value-line strings are produced by powercfg's own format strings; they have been stable across Windows 10 and 11 builds in English locale and are reported as language-invariant on Windows 11 24H2 too. If a future build localises them, the parser will return null and the tab will disable with a friendly error rather than misreporting values. See caveat #1 for the mitigation plan.

## Logging

Adds five new event names to `AppLog` via the existing `WindowsAppCore.AppLog` instance in `BatteryTray.Program`:

| Event | Level | Data |
|---|---|---|
| `hwactions.read` | Info | `{ liveAc:{pb,lc}, liveDc:{pb,lc}, hintShown:bool }` (dialog open) |
| `hwactions.applied` | Info | `{ policy, planCount, durationMs }` (Save success) |
| `hwactions.apply.failed` | Warn | `{ reason, exitCode? }` (any failure mode) |
| `hwactions.parse.failed` | Warn | `{ outputLength }` (parser returned null) |

`CrashLogger.Write("hwactions.apply", ex)` is used for unexpected exceptions inside `ApplyToAllPlans`, matching the pattern in `PowerPlanController`.

## Testing

### Unit (`apps\BatteryTray\BatteryTray.Tests`)

1. **`HardwareActionPolicyTests`**
   - `Defaults_AreSleepAndSleep`
   - `JsonRoundtrip_PreservesAllFields` (uses the same `System.Text.Json` options as `JsonSettingsStore<T>`)
   - `DifferOnBattery_FalseCollapsesAcToDc` (asserted via the args builder, not the policy itself)

2. **`HardwareActionsControllerArgsBuilderTests`**
   - `BuildCmdArgs_NoPlans_ReturnsOnlySetactive`
   - `BuildCmdArgs_OnePlan_DifferOnBatteryFalse_WritesAcEqualsDc`
   - `BuildCmdArgs_OnePlan_DifferOnBatteryTrue_WritesDistinctAcDc`
   - `BuildCmdArgs_ThreePlans_EmitsTwelveValueWritesPlusSetactive`
   - `[Theory]` over all five `HardwareAction` values verifying the integer index in the emitted args

3. **`HardwareActionsControllerParserTests`** (uses fixture text under `apps\BatteryTray\BatteryTray.Tests\fixtures\`)
   - `Parse_BalancedPlanFixture_ReturnsExpectedSnapshot` (committed fixture: Balanced plan on a typical laptop)
   - `Parse_MissingLidSection_ReturnsNull`
   - `Parse_EmptyString_ReturnsNull`
   - `Parse_ExtraWhitespace_StillSucceeds`

4. **`AppSettingsMigrationV3ToV4Tests`**
   - `Migrate_FromV3Json_BumpsSchemaVersionToFour`
   - `Migrate_FromV3Json_LeavesHardwareActionsNull`
   - `Migrate_AlreadyV4_NoOp`

5. **`AppSettingsTests` (existing) gets one new test:**
   - `Load_V3OnDiskFile_MigratesToV4WithNullHardwareActions`

### E2E (`apps\BatteryTray\BatteryTray.E2ETests`)

Marked `[WindowsFact]` and admin-skipped on CI. Real powercfg, real plans, real registry writes. Snapshot original values in `[Fact]` setup, restore in `finally`.

1. **`ReadCurrent_ReturnsLiveValues_Unelevated`** (does not require admin)
2. **`ApplyToAllPlans_RoundTrips_OnElevatedSession`** (skipped if not admin; flips Power=Sleep, Lid=Hibernate, reads back, asserts)
3. **`ApplyToAllPlans_OnUnelevatedSession_ReturnsFailureWithoutCrashing`** (skipped if admin)

### Smoke (manual, documented in WORKLOG)

After Phase complete:
1. Launch BatteryTray from a fresh build
2. Settings -> Hardware actions tab
3. Change Power button to `Do Nothing`, Lid close to `Sleep`
4. Save -> consent UAC
5. Close lid -> system sleeps
6. Press power button -> nothing happens
7. Revert via the dialog

## File manifest

### New files

| Path | Purpose |
|---|---|
| `apps\BatteryTray\BatteryTray\HardwareAction.cs` | enum |
| `apps\BatteryTray\BatteryTray\HardwareActionPolicy.cs` | persisted preference class |
| `apps\BatteryTray\BatteryTray\HardwareActionsSnapshot.cs` | read-back record struct |
| `apps\BatteryTray\BatteryTray\HardwareActionsController.cs` | powercfg wrapper, args builder, parser |
| `apps\BatteryTray\BatteryTray\Settings\AppSettingsMigrationV3ToV4.cs` | schema migration |
| `apps\BatteryTray\BatteryTray.Tests\HardwareActionPolicyTests.cs` | |
| `apps\BatteryTray\BatteryTray.Tests\HardwareActionsControllerArgsBuilderTests.cs` | |
| `apps\BatteryTray\BatteryTray.Tests\HardwareActionsControllerParserTests.cs` | |
| `apps\BatteryTray\BatteryTray.Tests\AppSettingsMigrationV3ToV4Tests.cs` | |
| `apps\BatteryTray\BatteryTray.Tests\fixtures\powercfg-sub-buttons-balanced.txt` | parser fixture |
| `apps\BatteryTray\BatteryTray.E2ETests\HardwareActionsControllerE2ETests.cs` | |

### Modified files

| Path | Change |
|---|---|
| `apps\BatteryTray\BatteryTray\AppSettings.cs` | bump `CurrentSchemaVersion` to 4; add `HardwareActions` property; register V3->V4 migration |
| `apps\BatteryTray\BatteryTray\SettingsForm.cs` | rename Power tab; insert Hardware actions tab; add `BuildHardwareActionsPanel`; add `LoadHardwareActions` and `SaveHardwareActions`; wire `ApplyToAllPlans` into Save |
| `WORKLOG.md` | new entry per commit |

### Unchanged

- `WindowsAppCore`, `WindowsTrayCore`, `WindowsAppTesting` — no shared-library changes
- Other apps — untouched
- `install.ps1`, `.github/workflows/*` — no changes needed
- `Directory.Build.props` — no changes

## Open caveats

1. **Non-English Windows.** `powercfg /q` value lines are emitted in English on all Windows builds regardless of UI language, so the parser is safe. But if Microsoft ever localises those lines, the parser breaks. Mitigation: parser failure is non-fatal (tab disables with a friendly message); add a fixture file per language if it ever happens.
2. **Hibernation disabled at OS level.** If `powercfg /hibernate off` is set, the Hibernate action falls back to Sleep in Windows. We do not detect or warn about this; the dropdown still offers Hibernate. Acceptable as a beta-test omission, may revisit.
3. **New plans installed after Save.** If a third party installs a new power plan after the user has saved, that plan will not have our values. Acceptable trade-off given the apply-to-all-plans-on-save model. If the user re-saves, the new plan gets covered.
4. **Reading powercfg as a standard user.** `powercfg /q SCHEME_CURRENT SUB_BUTTONS` works unelevated; verified by the existing `PowerPlanController.List()` precedent. No regression expected.
5. **Live UAC test.** Cannot be exercised in automated CI. Manual smoke required before declaring the feature complete.
