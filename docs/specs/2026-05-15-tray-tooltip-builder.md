# WindowsTrayCore: TrayTooltipBuilder for Multi-Line Tooltip Composition

**Status:** Design approved 2026-05-15. Implementation pending.
**Scope:** `shared\WindowsTrayCore\` plus all four consuming apps (BatteryTray, NetProfileSwitcher, ProgramHider, SoundTracker). No changes to `WindowsAppCore` or `WindowsAppTesting`.

## Summary

Replaces the ad-hoc tooltip-string handling currently scattered across the four apps with a single `TrayTooltipBuilder` in `WindowsTrayCore`. The builder accepts lines tagged as required or optional, joins them with `\n`, and produces a final string that fits the Win32 `szTip[128]` budget (127 usable chars). Optional lines are dropped from the tail first; if required lines still exceed budget, the last required line is word-boundary truncated with a single-glyph ellipsis. `TrayIcon.Text` (raw-string setter) is removed; callers go through the builder or a single-line `TooltipText` convenience setter.

## Context

`WindowsTrayCore.TrayIcon` is the Shell_NotifyIcon wrapper introduced in Phase 18.5; it uses `NOTIFYICON_VERSION_4` which gives `szTip[128]` (127 wide chars plus null terminator). The current `TrayIcon.Text { set; }` truncates blindly at 127. A separate `TrayTooltip` static helper exists with `MaxLength = 63` and a `Truncate` method, anchored on the legacy `NOTIFYICON_VERSION_1` budget. That constant no longer reflects reality but is still consulted by NetProfileSwitcher and SoundTracker, which means those apps publish shorter tooltips than the OS would actually display.

Per-app composition diverges:

| App | Composition | Limit consulted | Truncation strategy |
|---|---|---|---|
| BatteryTray | single-line, sprintf-style | 127 | hard cut at 127 |
| NetProfileSwitcher | single-line, sprintf-style | 63 (stale) | word-boundary + ASCII ellipsis |
| ProgramHider | single-line plus `[status]` suffix | 127 | hard cut |
| SoundTracker | multi-line via `Environment.NewLine` (`\r\n`) | 63 (stale) | per-line ASCII ellipsis |

Four apps, four idioms, two stale constants, one app (SoundTracker) doing genuine multi-line composition with an app-local `TooltipFormatter` class that nothing else can reuse.

## Locked Decisions

| Axis | Decision |
|---|---|
| Architecture | One new class `TrayTooltipBuilder` in `WindowsTrayCore`, replacing the existing `TrayTooltip` static helper. |
| Priority model | Two-tier: `AddRequired` and `AddOptional`. Optional lines drop from the tail first. |
| Line separator | `\n` (LF only). Tighter against the 127-char budget than `\r\n` and renders identically under modern Shell. |
| Truncation fallback | When required lines alone exceed budget, keep all but the last intact and word-boundary truncate the last with `…` (U+2026). |
| `TrayIcon.Text` raw setter | Removed. Replaced with `TrayIcon.Tooltip` (builder) and `TrayIcon.TooltipText` (single-line convenience). |
| Ellipsis glyph | `…` (U+2026, one char). Saves two chars per truncation against the budget; renders correctly in tray fonts on every supported Windows build. |
| Existing `TrayTooltip` static class | Deleted (not renamed). New name `TrayTooltipBuilder` makes the API shape obvious at call sites. |
| Per-app `TooltipFormatter` (SoundTracker) | Deleted. SoundTracker rewires through the new builder. |

## Architecture

### New type

```csharp
// shared/WindowsTrayCore/TrayTooltipBuilder.cs
namespace WindowsTrayCore;

public sealed class TrayTooltipBuilder
{
    /// <summary>
    /// Maximum total characters in the final string the builder produces,
    /// matching Win32 szTip[128] with NOTIFYICON_VERSION_4 (one wide char
    /// reserved for the null terminator).
    /// </summary>
    public const int MaxLength = 127;

    /// <summary>LF; cheaper against the budget than CRLF.</summary>
    public const char LineSeparator = '\n';

    /// <summary>Single-glyph ellipsis appended when a line is truncated.</summary>
    public const string Ellipsis = "…";

    public TrayTooltipBuilder AddRequired(string text);
    public TrayTooltipBuilder AddOptional(string text);
    public string Build();
}
```

### Modified type

```csharp
// shared/WindowsTrayCore/TrayIcon.cs (relevant excerpt)

// REMOVED: public string Text { get; set; }

public TrayTooltipBuilder Tooltip
{
    set => SetTipFromString(value?.Build() ?? string.Empty);
}

/// <summary>
/// Single-required-line convenience. Apps with a one-line tooltip don't need
/// to allocate a builder; assigning a string here builds an internal one.
/// </summary>
public string TooltipText
{
    set => SetTipFromString(
        new TrayTooltipBuilder().AddRequired(value ?? string.Empty).Build());
}

private void SetTipFromString(string final) { /* existing szTip path */ }
```

`TrayIcon.Text`'s defensive 127-truncate disappears with the property because every code path now flows through `TrayTooltipBuilder.Build()`, which is the canonical budget enforcer.

### Removed types

- `shared\WindowsTrayCore\TrayTooltip.cs` (static class with stale `MaxLength = 63` and `Truncate`). Tests in `TrayTooltipTests.cs` rewritten against the new builder.
- `apps\SoundTracker\SoundTracker.App\TooltipFormatter.cs` (app-local multi-line composer; functionality now in the shared builder).

## Build algorithm

`TrayTooltipBuilder.Build()` is a pure function over the accumulated line list:

```
budget = MaxLength (= 127)

1. NORMALISE
   For every line in (required ∪ optional):
     a. Replace \r\n and lone \r with \n.
     b. Split on \n. Each resulting fragment becomes its own logical line,
        preserving its required/optional tag.
     c. Drop fragments that are empty or whitespace-only after Trim().
        (Tag does NOT influence drop behaviour at this stage; whitespace-only
        lines are never carried.)

2. SIZE CHECK
   total = sum(line.Length) + max(0, lineCount - 1)     // LFs between lines

   If total <= budget: emit join(lines, '\n') and return.

3. DROP OPTIONALS
   While total > budget AND there exists at least one optional line:
     Remove the last optional line (in original add-order).
     Recompute total.

   If total <= budget: emit join(remaining, '\n') and return.

4. TRUNCATE LAST REQUIRED
   Let R = remaining required lines (in original add-order).
   Let prefix = join(R[0..R.Count - 1], '\n') if R.Count > 1, else "".
   Let prefixCost = prefix.Length + (R.Count > 1 ? 1 : 0)    // trailing LF
   Let lastBudget = budget - prefixCost - Ellipsis.Length

   If lastBudget <= 0:
     // Earlier required lines alone exceed budget. Final fallback:
     // hard-cut the whole join at budget. Should be vanishingly rare given
     // typical app tooltips (longest line ≈ 60 chars).
     Return join(R, '\n').Substring(0, budget)

   Let last = R[R.Count - 1]
   Let truncated = WordBoundaryTruncate(last, lastBudget) + Ellipsis
   Return (prefix.Length > 0 ? prefix + '\n' : "") + truncated

5. EDGE: empty input
   If both required and optional lists are empty after normalisation,
   return "".

WordBoundaryTruncate(text, budget):
   If text.Length <= budget: return text
   Find lastSpace = text.LastIndexOf(' ', budget - 1, budget)
   If lastSpace >= budget / 2:   // accept the word boundary
     return text.Substring(0, lastSpace).TrimEnd()
   Else:                          // no useful boundary; hard cut
     return text.Substring(0, budget).TrimEnd()
```

Key invariants:

- `Build()` is idempotent; calling it twice on the same builder returns the same string.
- The output is never longer than `MaxLength`.
- The output never contains `\r`; only `\n` separators.
- The output never starts or ends with `\n` (whitespace-only lines are dropped before joining).
- Required lines preserve their relative add-order; optional lines preserve theirs.

## Failure modes

| Mode | Detection | Behaviour |
|---|---|---|
| Null passed to AddRequired/AddOptional | argument null check | `ArgumentNullException` (programmer error; should be caught in tests) |
| Caller adds a single line longer than MaxLength | trivially detected in step 2/4 | word-boundary truncate with ellipsis (step 4) |
| Whitespace-only line added | step 1c | silently dropped, no exception |
| Both lists empty | step 5 | returns `""` (TrayIcon will set an empty szTip, which is valid) |
| Pre-joined multi-line string passed in one call | step 1b splits on `\n` | each fragment becomes its own line with the original tag |
| Multi-line string whose required tag protects fragments that exceed budget | step 4 | last fragment word-truncated; earlier fragments preserved |

No `try/catch` in `Build()`. It is a pure string-manipulation function with no I/O.

## API ergonomics: when to use which

```csharp
// Multi-line, mixed priority: use the builder
_tray.Tooltip = new TrayTooltipBuilder()
    .AddRequired($"BatteryTray v{ver}")
    .AddRequired($"{state} - {pct}%")
    .AddOptional($"{remainingFmt} remaining")
    .AddOptional("Battery Saver active");

// Static single-line: use TooltipText
_tray.TooltipText = $"Program Hider v{ver} [{flagSuffix}]";

// Conditional single-line vs multi-line: still use the builder, branch
// inside AddOptional calls
var tb = new TrayTooltipBuilder().AddRequired($"NetProfileSwitcher: {profile}");
if (!string.IsNullOrEmpty(ssid)) tb.AddOptional($"on {ssid}");
_tray.Tooltip = tb;
```

The two setters exist in parallel; the `TooltipText` name signals "raw string, single line" and the `Tooltip` name signals "structured, possibly multi-line". A grep for `.TooltipText =` is an audit lever for catching apps that grew past the single-line idiom.

## Per-app migration

### BatteryTray

`apps\BatteryTray\BatteryTray\BatteryTrayContext.cs` builds the tooltip from a status string of the shape `"Charging - 50%"` or `"On battery - 30%  ·  Battery Saver  ·  2h 15m remaining"`. Rework to:

```csharp
var tb = new TrayTooltipBuilder()
    .AddRequired($"BatteryTray v{Application.ProductVersion}")
    .AddRequired($"{stateLabel} - {percent}%");
if (saverActive)    tb.AddOptional("Battery Saver active");
if (hasRemaining)   tb.AddOptional($"{remainingFmt} remaining");
_trayIcon.Tooltip = tb;
```

Manual `if (status.Length > 127)` truncate is deleted.

### NetProfileSwitcher

`apps\NetProfileSwitcher\UI\MainForm.cs` currently consults the stale 63-char limit and word-truncates. Rework:

```csharp
var tb = new TrayTooltipBuilder()
    .AddRequired($"NPS: {profileName ?? "no profile"}");
if (!string.IsNullOrEmpty(ssid))  tb.AddOptional($"on {ssid}");
_trayIcon.Tooltip = tb;
```

The custom `TruncateAtWord` helper in MainForm.cs is deleted; `Build()` handles the truncation.

### ProgramHider

`apps\ProgramHider\app\ProgramHider\ProgramHiderContext.cs` builds a single-line string like `"Program Hider v1.2.0 [Admin, Locked, Safe]"`. The simplest possible migration:

```csharp
_trayIcon.TooltipText = $"Program Hider v{version} [{flagSuffix}]";
```

If flag suffix proliferation pushes this past 127 chars in future, the call site upgrades to a builder. Until then, the convenience setter is the right tool.

### SoundTracker

`apps\SoundTracker\SoundTracker.App\TooltipFormatter.cs` is deleted entirely. The composition logic moves inline into the tray-tooltip refresh path:

```csharp
var tb = new TrayTooltipBuilder()
    .AddRequired($"{AppMetadata.TooltipPrefix} v{AppMetadata.DisplayVersion}")
    .AddRequired($"{(muted ? "Muted" : $"Volume {volumePercent}%")}");
if (!string.IsNullOrEmpty(activeAppName))
    tb.AddOptional($"Active: {activeAppName}");
if (recentApps.Count > 0)
    tb.AddOptional($"Recent: {string.Join(", ", recentApps.Take(2))}");
_trayIcon.Tooltip = tb;
```

`NotifyIconTextLimit` and the per-line ellipsis logic that lived in `TooltipFormatter` come for free from the builder; the 63-char ceiling is implicitly lifted to 127.

## Testing

### Unit (`shared\WindowsTrayCore.Tests\`)

`TrayTooltipTests.cs` is rewritten end-to-end. New test names and intents:

- `Build_NoLines_ReturnsEmpty`
- `Build_SingleRequired_PassesThrough`
- `Build_SingleRequired_LongerThanBudget_WordTruncatesWithEllipsis`
- `Build_RequiredAndOptional_AllUnderBudget_JoinsWithLF`
- `Build_RequiredAndOptional_OverBudget_DropsOptionalsFromTail`
- `Build_OnlyOptionals_AllUnderBudget_JoinsWithLF`
- `Build_MultipleRequiredOverBudget_TruncatesLastWithEllipsis`
- `Build_PreservesRequiredOrderAfterOptionalDrop`
- `Build_NormalisesCRLFToLF`
- `Build_DropsWhitespaceOnlyLines`
- `Build_NullToAddRequired_Throws`
- `Build_NullToAddOptional_Throws`
- `Build_ResultNeverExceedsMaxLength` (parameterised theory with adversarial inputs)
- `Build_Idempotent` (calling twice returns equal strings)
- `WordBoundaryTruncate_LastSpaceBelowHalfBudget_HardCuts` (internal; via `[InternalsVisibleTo]`)

`TrayIconTests.cs` adjusts:

- The current `.Text` truncation test (lines 24-29) is rewritten to drive the new `TooltipText` setter and assert it produces the same szTip behaviour.
- A new test covers `Tooltip = builder` forwarding correctly.

### Smoke (`apps\SoundTracker\SoundTracker.SmokeTests\`)

The existing assertion that the tooltip is multi-line and contains version/volume info is preserved but the test's setup migrates to the new builder API.

### No new E2E coverage

This change has no Win32-side behaviour beyond what `TrayIcon` already verifies via existing tests. No new E2E project files.

## File manifest

### New files

| Path | Purpose |
|---|---|
| `shared\WindowsTrayCore\TrayTooltipBuilder.cs` | New builder class |

### Deleted files

| Path | Rationale |
|---|---|
| `shared\WindowsTrayCore\TrayTooltip.cs` | Stale 63-char API; replaced |
| `apps\SoundTracker\SoundTracker.App\TooltipFormatter.cs` | Logic moved into the shared builder |

### Modified files

| Path | Change |
|---|---|
| `shared\WindowsTrayCore\TrayIcon.cs` | Remove `Text` property; add `Tooltip` and `TooltipText` setters |
| `shared\WindowsTrayCore.Tests\TrayTooltipTests.cs` | Rewritten against the builder |
| `shared\WindowsTrayCore.Tests\TrayIconTests.cs` | Truncate test migrated; new Tooltip-forwarding test |
| `apps\BatteryTray\BatteryTray\BatteryTrayContext.cs` | Builder migration |
| `apps\NetProfileSwitcher\UI\MainForm.cs` | Builder migration; delete `TruncateAtWord` helper |
| `apps\ProgramHider\app\ProgramHider\ProgramHiderContext.cs` | `TooltipText` shortcut migration |
| `apps\SoundTracker\SoundTracker.App\*.cs` (tooltip caller) | Builder migration; delete TooltipFormatter usage |
| `apps\SoundTracker\SoundTracker.SmokeTests\*.cs` | Test setup migrated |
| `WORKLOG.md` | New entry on the final commit |

### Unchanged

- `WindowsAppCore`, `WindowsAppTesting`: no shared-library changes outside `WindowsTrayCore`.
- Native interop (`shared\WindowsTrayCore\Native\TrayNativeMethods.cs`): unchanged. The 128-wide-char szTip already supports everything the new builder needs.
- `install.ps1`, `.github\workflows\*`, `Directory.Build.props`: no changes.

## Open caveats

1. **Single-glyph ellipsis on legacy fonts.** `…` (U+2026) renders correctly in Segoe UI Variable (Windows 11) and Segoe UI (Windows 10). If a future build runs on a host with a heavily customised tray font that lacks U+2026, the glyph would render as a tofu box. Mitigation: the constant lives in one place (`TrayTooltipBuilder.Ellipsis`) and can be flipped to `"..."` with a one-line change.
2. **Word-boundary heuristic.** The `WordBoundaryTruncate` rule (accept the boundary only if it lands above `budget / 2`) is a judgement call. Tighter or looser heuristics may produce nicer-looking truncations on specific inputs. The 50% floor is a balance between "too aggressive a truncate" and "leaving too little useful text". Revisit if user testing surfaces awkward cuts.
3. **`TooltipText` drift risk.** The single-line convenience setter exists in parallel to the builder. If an app outgrows a single line it must remember to migrate to `Tooltip = new TrayTooltipBuilder()...`. The audit lever is `grep -r "\.TooltipText"` in `apps\`; flag any call site whose interpolation has grown past one logical concept.
4. **No locale-aware word boundary.** `WordBoundaryTruncate` looks for ASCII space (`' '`). It will not find boundary at U+00A0 (non-breaking space), U+3000 (ideographic space), or zero-width spaces. Acceptable for English-locale tray tooltips; revisit if internationalisation becomes a goal.
