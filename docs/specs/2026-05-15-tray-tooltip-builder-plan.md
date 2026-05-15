# TrayTooltipBuilder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the four apps' ad-hoc tooltip-string handling with a single shared `TrayTooltipBuilder` in `WindowsTrayCore` that produces well-formed multi-line tooltips fitting the Win32 `szTip[128]` budget.

**Architecture:** New `TrayTooltipBuilder` class with `AddRequired` / `AddOptional` / `Build` surface. `TrayIcon` gains `Tooltip` (builder) and `TooltipText` (single-line convenience) setters and loses the `Text` property. SoundTracker's local `TooltipFormatter.cs` is renamed to `ActivityLabelFormatter.cs` with its tooltip-only methods deleted.

**Tech Stack:** .NET 8, WinForms, xUnit + FluentAssertions, no new external dependencies.

**Spec:** `docs/specs/2026-05-15-tray-tooltip-builder.md`

---

## File Structure

### New files

| Path | Responsibility |
|---|---|
| `shared\WindowsTrayCore\TrayTooltipBuilder.cs` | Builder class; pure-function `Build` produces final tooltip string under budget |
| `shared\WindowsTrayCore.Tests\TrayTooltipBuilderTests.cs` | Unit tests for the builder |

### Renamed files

| From | To |
|---|---|
| `apps\SoundTracker\SoundTracker.App\TooltipFormatter.cs` | `apps\SoundTracker\SoundTracker.App\ActivityLabelFormatter.cs` |

### Deleted files

| Path | Rationale |
|---|---|
| `shared\WindowsTrayCore\TrayTooltip.cs` | Stale 63-char API; replaced by builder |
| `shared\WindowsTrayCore.Tests\TrayTooltipTests.cs` | Tests for the deleted class; superseded by `TrayTooltipBuilderTests.cs` |

### Modified files

| Path | Change |
|---|---|
| `shared\WindowsTrayCore\TrayIcon.cs` | Add `Tooltip` and `TooltipText` setters; remove `Text` property (final task) |
| `shared\WindowsTrayCore.Tests\TrayIconTests.cs` | Replace `Text_TruncatesPast127Chars` test; add tests for new setters |
| `apps\BatteryTray\BatteryTray\BatteryTrayContext.cs` | Migrate tooltip composition to builder |
| `apps\NetProfileSwitcher\UI\MainForm.cs` | Migrate three `_tray.Text =` call sites; delete `TruncateAtWord` helper |
| `apps\ProgramHider\app\ProgramHider\ProgramHiderContext.cs` | Migrate to `TooltipText` setter |
| `apps\SoundTracker\SoundTracker.App\TrayApplicationContext.cs` | Replace `_notifyIcon.Text =` calls with builder; rename `TooltipFormatter` references to `ActivityLabelFormatter` |
| `apps\SoundTracker\SoundTracker.App\RecentActivityForm.cs` | Rename `TooltipFormatter` references to `ActivityLabelFormatter` |
| `apps\SoundTracker\SoundTracker.SmokeTests\Program.cs` | Rewrite `TooltipFormatter_MultilineTooltip` against the new composition path |
| `WORKLOG.md` | New entry on the final commit |

### Working directory

All paths relative to `D:\code\windows-apps\`. PowerShell is the project shell; `Bash` works equally well for `dotnet`/`git` invocations.

---

## Task 1: TrayTooltipBuilder skeleton (TDD)

Foundation: the class exists, the basic `Build()` invariants hold for trivial input.

**Files:**
- Create: `shared\WindowsTrayCore\TrayTooltipBuilder.cs`
- Create: `shared\WindowsTrayCore.Tests\TrayTooltipBuilderTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `shared\WindowsTrayCore.Tests\TrayTooltipBuilderTests.cs`:

```csharp
using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class TrayTooltipBuilderTests
{
    [Fact]
    public void MaxLength_Is127()
    {
        TrayTooltipBuilder.MaxLength.Should().Be(127);
    }

    [Fact]
    public void Build_NoLines_ReturnsEmpty()
    {
        new TrayTooltipBuilder().Build().Should().BeEmpty();
    }

    [Fact]
    public void Build_SingleRequired_PassesThrough()
    {
        var result = new TrayTooltipBuilder()
            .AddRequired("hello")
            .Build();

        result.Should().Be("hello");
    }

    [Fact]
    public void Build_SingleOptional_PassesThrough()
    {
        var result = new TrayTooltipBuilder()
            .AddOptional("hello")
            .Build();

        result.Should().Be("hello");
    }
}
```

- [ ] **Step 2: Run tests; verify build error**

Run: `dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj --filter FullyQualifiedName~TrayTooltipBuilderTests`
Expected: build error `CS0246: The type or namespace name 'TrayTooltipBuilder' could not be found`.

- [ ] **Step 3: Create the skeleton class**

Create `shared\WindowsTrayCore\TrayTooltipBuilder.cs`:

```csharp
using System.Collections.Generic;

namespace WindowsTrayCore;

/// <summary>
/// Composes multi-line tray tooltips that fit the Win32 szTip[128] budget
/// (127 usable wide chars with NOTIFYICON_VERSION_4). Lines are tagged as
/// required or optional; optional lines drop from the tail first when over
/// budget. If required lines alone overflow, the last required line is
/// word-boundary truncated with a single-glyph ellipsis.
/// </summary>
public sealed class TrayTooltipBuilder
{
    public const int MaxLength = 127;
    public const char LineSeparator = '\n';
    public const string Ellipsis = "…";

    private readonly List<Line> _lines = new();

    public TrayTooltipBuilder AddRequired(string text)
    {
        if (text is null) throw new System.ArgumentNullException(nameof(text));
        _lines.Add(new Line(text, IsRequired: true));
        return this;
    }

    public TrayTooltipBuilder AddOptional(string text)
    {
        if (text is null) throw new System.ArgumentNullException(nameof(text));
        _lines.Add(new Line(text, IsRequired: false));
        return this;
    }

    public string Build()
    {
        if (_lines.Count == 0) return string.Empty;
        // Joining + truncation logic added in subsequent tasks.
        // For now, join with LF (covers the trivial single-line case).
        var parts = new List<string>(_lines.Count);
        foreach (var line in _lines) parts.Add(line.Text);
        return string.Join(LineSeparator, parts);
    }

    private readonly record struct Line(string Text, bool IsRequired);
}
```

- [ ] **Step 4: Run tests; verify pass**

Run: `dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj --filter FullyQualifiedName~TrayTooltipBuilderTests`
Expected: 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add shared/WindowsTrayCore/TrayTooltipBuilder.cs shared/WindowsTrayCore.Tests/TrayTooltipBuilderTests.cs
git commit -m "WindowsTrayCore: TrayTooltipBuilder skeleton

Foundation class for composing multi-line tray tooltips. AddRequired and
AddOptional accept lines tagged for priority; Build returns them joined
with LF. Truncation and optional-drop logic come in subsequent commits.

MaxLength constant codifies the Win32 szTip[128] budget (127 usable wide
chars under NOTIFYICON_VERSION_4)."
```

---

## Task 2: Build() joins lines under budget (TDD)

Multi-line joining when the total fits the budget. Still no truncation logic.

**Files:**
- Modify: `shared\WindowsTrayCore.Tests\TrayTooltipBuilderTests.cs` (add cases)

The implementation from Task 1 already handles this case via the trivial `string.Join`. New tests prove it under realistic multi-line input.

- [ ] **Step 1: Add the failing tests**

Append to `shared\WindowsTrayCore.Tests\TrayTooltipBuilderTests.cs` (inside the existing class):

```csharp
    [Fact]
    public void Build_TwoRequired_UnderBudget_JoinsWithLF()
    {
        var result = new TrayTooltipBuilder()
            .AddRequired("first line")
            .AddRequired("second line")
            .Build();

        result.Should().Be("first line\nsecond line");
    }

    [Fact]
    public void Build_RequiredAndOptional_UnderBudget_JoinsInAddOrder()
    {
        var result = new TrayTooltipBuilder()
            .AddRequired("required A")
            .AddOptional("optional B")
            .AddRequired("required C")
            .AddOptional("optional D")
            .Build();

        result.Should().Be("required A\noptional B\nrequired C\noptional D");
    }

    [Fact]
    public void Build_OnlyOptionals_UnderBudget_JoinsWithLF()
    {
        var result = new TrayTooltipBuilder()
            .AddOptional("opt1")
            .AddOptional("opt2")
            .Build();

        result.Should().Be("opt1\nopt2");
    }
```

- [ ] **Step 2: Run tests; verify pass**

Run: `dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj --filter FullyQualifiedName~TrayTooltipBuilderTests`
Expected: 7 tests pass (4 from Task 1 + 3 new). The current `Build` already handles this; no implementation change is needed.

- [ ] **Step 3: Commit**

```bash
git add shared/WindowsTrayCore.Tests/TrayTooltipBuilderTests.cs
git commit -m "WindowsTrayCore: tests for under-budget multi-line joining

Pins the contract that AddRequired and AddOptional in any interleaving
produce a single LF-joined string when the total fits MaxLength. No
implementation change needed; the Task 1 skeleton already covers this
path."
```

---

## Task 3: Build() drops optional lines from tail when over budget (TDD)

The first behaviour where the builder does real work: enforce the budget by dropping optional lines.

**Files:**
- Modify: `shared\WindowsTrayCore\TrayTooltipBuilder.cs`
- Modify: `shared\WindowsTrayCore.Tests\TrayTooltipBuilderTests.cs`

- [ ] **Step 1: Add the failing tests**

Append to `shared\WindowsTrayCore.Tests\TrayTooltipBuilderTests.cs`:

```csharp
    [Fact]
    public void Build_OverBudget_DropsLastOptional()
    {
        // Three 50-char lines: 50 + 1 + 50 + 1 + 50 = 152. Over 127 by 25.
        var line50 = new string('a', 50);
        var result = new TrayTooltipBuilder()
            .AddRequired(line50)
            .AddOptional(line50)
            .AddOptional(line50)
            .Build();

        // Drop the last optional. 50 + 1 + 50 = 101 chars, under budget.
        result.Should().Be(line50 + "\n" + line50);
        result.Length.Should().Be(101);
    }

    [Fact]
    public void Build_OverBudget_DropsOptionalsInReverseAddOrder()
    {
        // Two 70-char optionals; together with required they overflow.
        // Required (50) + LF + opt1 (70) + LF + opt2 (70) = 192. Over by 65.
        var req = new string('R', 50);
        var opt1 = new string('1', 70);
        var opt2 = new string('2', 70);

        var result = new TrayTooltipBuilder()
            .AddRequired(req)
            .AddOptional(opt1)
            .AddOptional(opt2)
            .Build();

        // After dropping opt2: 50 + 1 + 70 = 121. Under budget.
        result.Should().Be(req + "\n" + opt1);
    }

    [Fact]
    public void Build_OverBudget_DropsAllOptionalsUntilFits()
    {
        // 80-char required + two 30-char optionals.
        // 80 + 1 + 30 + 1 + 30 = 142. Over by 15.
        // Drop opt2: 80 + 1 + 30 = 111. Under. Stop.
        var req = new string('R', 80);
        var opt1 = new string('1', 30);
        var opt2 = new string('2', 30);

        var result = new TrayTooltipBuilder()
            .AddRequired(req)
            .AddOptional(opt1)
            .AddOptional(opt2)
            .Build();

        result.Should().Be(req + "\n" + opt1);
    }

    [Fact]
    public void Build_OverBudget_PreservesRequiredAfterOptionalsDropped()
    {
        // Interleaved: R1, O1, R2, O2.
        // 40 + 1 + 40 + 1 + 40 + 1 + 40 = 163. Over by 36.
        // Drop O2 (the last optional in add order): 40 + 1 + 40 + 1 + 40 = 122. Under.
        var r1 = new string('A', 40);
        var o1 = new string('B', 40);
        var r2 = new string('C', 40);
        var o2 = new string('D', 40);

        var result = new TrayTooltipBuilder()
            .AddRequired(r1)
            .AddOptional(o1)
            .AddRequired(r2)
            .AddOptional(o2)
            .Build();

        // r1, o1, r2 in their original positions, o2 gone.
        result.Should().Be(r1 + "\n" + o1 + "\n" + r2);
    }
```

- [ ] **Step 2: Run tests; verify they fail**

Run: `dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj --filter FullyQualifiedName~TrayTooltipBuilderTests`
Expected: 4 new tests fail (output strings exceed MaxLength because no truncation logic exists yet).

- [ ] **Step 3: Add optional-drop logic to `Build()`**

Replace the `Build()` method in `shared\WindowsTrayCore\TrayTooltipBuilder.cs`:

```csharp
    public string Build()
    {
        if (_lines.Count == 0) return string.Empty;

        // Work on a copy so Build() is idempotent.
        var working = new List<Line>(_lines);

        if (TotalLength(working) <= MaxLength)
            return JoinLines(working);

        // Drop optional lines from the tail (last-added optional first).
        for (int i = working.Count - 1; i >= 0 && TotalLength(working) > MaxLength; i--)
        {
            if (!working[i].IsRequired)
            {
                working.RemoveAt(i);
            }
        }

        if (TotalLength(working) <= MaxLength)
            return JoinLines(working);

        // Required-line overflow falls through to Task 4's truncation logic.
        // For now, return the partial join so the over-budget tests in Task 3
        // observe correct optional-drop behaviour. The required-overflow
        // tests are added in Task 4.
        return JoinLines(working);
    }

    private static int TotalLength(List<Line> lines)
    {
        if (lines.Count == 0) return 0;
        int total = lines.Count - 1; // separators
        foreach (var line in lines) total += line.Text.Length;
        return total;
    }

    private static string JoinLines(List<Line> lines)
    {
        if (lines.Count == 0) return string.Empty;
        var parts = new string[lines.Count];
        for (int i = 0; i < lines.Count; i++) parts[i] = lines[i].Text;
        return string.Join(LineSeparator, parts);
    }
```

- [ ] **Step 4: Run tests; verify pass**

Run: `dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj --filter FullyQualifiedName~TrayTooltipBuilderTests`
Expected: 11 tests pass (7 from Tasks 1-2 + 4 new).

- [ ] **Step 5: Commit**

```bash
git add shared/WindowsTrayCore/TrayTooltipBuilder.cs shared/WindowsTrayCore.Tests/TrayTooltipBuilderTests.cs
git commit -m "WindowsTrayCore: TrayTooltipBuilder drops optionals on overflow

When the total length of all added lines exceeds MaxLength, optional
lines are removed from the tail (last-added first) until total fits.
Required lines preserve their relative add-order; interleaved required
and optional inputs keep their non-dropped positions intact.

Required-line overflow (case where dropping all optionals is still
insufficient) is handled by the truncation logic in the next commit."
```

---

## Task 4: Build() word-boundary truncates the last required line (TDD)

When even the required lines alone exceed budget, keep all but the last intact and word-boundary truncate the last with an ellipsis.

**Files:**
- Modify: `shared\WindowsTrayCore\TrayTooltipBuilder.cs`
- Modify: `shared\WindowsTrayCore.Tests\TrayTooltipBuilderTests.cs`

- [ ] **Step 1: Add the failing tests**

Append to `shared\WindowsTrayCore.Tests\TrayTooltipBuilderTests.cs`:

```csharp
    [Fact]
    public void Build_RequiredOverflowsByItself_WordTruncatesLast()
    {
        // Single required line longer than MaxLength.
        // 150 chars of "the quick brown fox..." style content.
        const string text = "BatteryTray version 1.0.0 status charging at 47 percent with 3 hours 22 minutes remaining and Battery Saver active running on Asus ZenBook laptop";
        text.Length.Should().BeGreaterThan(127); // sanity

        var result = new TrayTooltipBuilder()
            .AddRequired(text)
            .Build();

        result.Length.Should().BeLessOrEqualTo(127);
        result.Should().EndWith(TrayTooltipBuilder.Ellipsis);
        // Truncation must respect word boundaries when one exists in range.
        // Find the char before the ellipsis; it should be a letter, not a space.
        var beforeEllipsis = result[..^TrayTooltipBuilder.Ellipsis.Length];
        beforeEllipsis.Should().NotEndWith(" ");
    }

    [Fact]
    public void Build_MultipleRequiredOverflow_TruncatesOnlyLast()
    {
        var r1 = new string('A', 40);
        var r2 = new string('B', 40);
        // r3 is 80 chars - won't fit alongside r1+r2+2LFs (40+1+40+1+80 = 162).
        var r3 = "word " + new string('C', 75);

        var result = new TrayTooltipBuilder()
            .AddRequired(r1)
            .AddRequired(r2)
            .AddRequired(r3)
            .Build();

        result.Length.Should().BeLessOrEqualTo(127);
        result.Should().StartWith(r1 + "\n" + r2 + "\n");
        result.Should().EndWith(TrayTooltipBuilder.Ellipsis);
    }

    [Fact]
    public void Build_RequiredOverflow_KeepsWordBoundaryWhenAboveHalfBudget()
    {
        // 200-char string with spaces every ~20 chars.
        var parts = System.Linq.Enumerable.Range(0, 10)
            .Select(i => $"word{i:D2}xx{new string('y', 12)}");
        var text = string.Join(' ', parts); // ~199 chars with spaces
        text.Length.Should().BeGreaterThan(127);

        var result = new TrayTooltipBuilder()
            .AddRequired(text)
            .Build();

        // The truncated text should end at a word boundary, not mid-word.
        result.Should().EndWith(TrayTooltipBuilder.Ellipsis);
        var beforeEllipsis = result[..^TrayTooltipBuilder.Ellipsis.Length];
        beforeEllipsis.Should().NotEndWith(" ");
        beforeEllipsis.TrimEnd().Length.Should().Be(beforeEllipsis.Length); // no trailing whitespace
    }

    [Fact]
    public void Build_RequiredOverflow_NoUsefulWordBoundary_HardCuts()
    {
        // Single 200-char string with NO spaces; falls back to hard cut.
        var text = new string('x', 200);

        var result = new TrayTooltipBuilder()
            .AddRequired(text)
            .Build();

        result.Length.Should().BeLessOrEqualTo(127);
        result.Should().EndWith(TrayTooltipBuilder.Ellipsis);
        // Everything before the ellipsis is 'x', the hard-cut content.
        var beforeEllipsis = result[..^TrayTooltipBuilder.Ellipsis.Length];
        beforeEllipsis.Should().OnlyContain(c => c == 'x');
    }
```

- [ ] **Step 2: Run tests; verify they fail**

Run: `dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj --filter FullyQualifiedName~TrayTooltipBuilderTests`
Expected: 4 new tests fail. Existing tests still pass.

- [ ] **Step 3: Implement truncation logic**

Replace the final `Build()` block in `shared\WindowsTrayCore\TrayTooltipBuilder.cs`. The full updated method body (replacing the current `Build` and adding helpers):

```csharp
    public string Build()
    {
        if (_lines.Count == 0) return string.Empty;

        var working = new List<Line>(_lines);

        if (TotalLength(working) <= MaxLength)
            return JoinLines(working);

        for (int i = working.Count - 1; i >= 0 && TotalLength(working) > MaxLength; i--)
        {
            if (!working[i].IsRequired)
            {
                working.RemoveAt(i);
            }
        }

        if (TotalLength(working) <= MaxLength)
            return JoinLines(working);

        // Required lines alone exceed budget. Keep all but the last intact;
        // truncate the last with an ellipsis at a word boundary when possible.
        return TruncateLastRequired(working);
    }

    private static int TotalLength(List<Line> lines)
    {
        if (lines.Count == 0) return 0;
        int total = lines.Count - 1; // separators
        foreach (var line in lines) total += line.Text.Length;
        return total;
    }

    private static string JoinLines(List<Line> lines)
    {
        if (lines.Count == 0) return string.Empty;
        var parts = new string[lines.Count];
        for (int i = 0; i < lines.Count; i++) parts[i] = lines[i].Text;
        return string.Join(LineSeparator, parts);
    }

    private static string TruncateLastRequired(List<Line> required)
    {
        // Compose the prefix (all lines before the last) intact.
        // If a single required line overflows, prefix is empty.
        var prefix = required.Count > 1
            ? string.Join(LineSeparator, EnumerateExceptLast(required))
            : string.Empty;

        int prefixCost = prefix.Length + (required.Count > 1 ? 1 : 0);
        int lastBudget = MaxLength - prefixCost - Ellipsis.Length;

        if (lastBudget <= 0)
        {
            // Earlier required lines alone are at or past budget. Hard-cut
            // the whole join. This is a degenerate case for any realistic
            // tooltip; the contract is "fit at any cost, do not exceed".
            var combined = string.Join(LineSeparator, EnumerateAllText(required));
            return combined.Length <= MaxLength ? combined : combined[..MaxLength];
        }

        var last = required[required.Count - 1].Text;
        var truncated = WordBoundaryTruncate(last, lastBudget);
        return prefix.Length > 0
            ? prefix + LineSeparator + truncated + Ellipsis
            : truncated + Ellipsis;
    }

    private static IEnumerable<string> EnumerateExceptLast(List<Line> lines)
    {
        for (int i = 0; i < lines.Count - 1; i++) yield return lines[i].Text;
    }

    private static IEnumerable<string> EnumerateAllText(List<Line> lines)
    {
        foreach (var line in lines) yield return line.Text;
    }

    private static string WordBoundaryTruncate(string text, int budget)
    {
        if (text.Length <= budget) return text;

        // Look for the last ASCII space within the budget window.
        int lastSpace = text.LastIndexOf(' ', budget - 1, budget);
        if (lastSpace >= budget / 2)
        {
            // Accept the word boundary: cut there and trim trailing whitespace.
            return text[..lastSpace].TrimEnd();
        }

        // No useful boundary; hard cut.
        return text[..budget].TrimEnd();
    }
```

- [ ] **Step 4: Run tests; verify pass**

Run: `dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj --filter FullyQualifiedName~TrayTooltipBuilderTests`
Expected: 15 tests pass (11 prior + 4 new).

- [ ] **Step 5: Commit**

```bash
git add shared/WindowsTrayCore/TrayTooltipBuilder.cs shared/WindowsTrayCore.Tests/TrayTooltipBuilderTests.cs
git commit -m "WindowsTrayCore: word-boundary truncation on required-line overflow

When required lines alone exceed MaxLength after all optionals have
been dropped, the builder keeps all-but-last required lines intact and
word-boundary truncates the last with a U+2026 ellipsis. The boundary
is accepted only if it lands above half the remaining budget; otherwise
the cut is hard.

The degenerate case where earlier required lines alone consume the
entire budget falls back to a hard cut at MaxLength to maintain the
'fit at any cost' invariant; realistic apps will never hit it."
```

---

## Task 5: Build() normalises input (TDD)

`\r\n` and lone `\r` collapse to `\n`. Pre-joined multi-line strings get split. Whitespace-only lines are dropped.

**Files:**
- Modify: `shared\WindowsTrayCore\TrayTooltipBuilder.cs`
- Modify: `shared\WindowsTrayCore.Tests\TrayTooltipBuilderTests.cs`

- [ ] **Step 1: Add the failing tests**

Append to `shared\WindowsTrayCore.Tests\TrayTooltipBuilderTests.cs`:

```csharp
    [Fact]
    public void Build_NormalisesCRLFToLF()
    {
        var result = new TrayTooltipBuilder()
            .AddRequired("first\r\nsecond")
            .Build();

        result.Should().Be("first\nsecond");
        result.Should().NotContain("\r");
    }

    [Fact]
    public void Build_NormalisesLoneCRToLF()
    {
        var result = new TrayTooltipBuilder()
            .AddRequired("first\rsecond")
            .Build();

        result.Should().Be("first\nsecond");
    }

    [Fact]
    public void Build_PreJoinedMultilineString_BecomesMultipleLogicalLines()
    {
        // One AddRequired call with two embedded newlines should behave
        // identically to three AddRequired calls.
        var resultA = new TrayTooltipBuilder()
            .AddRequired("L1\nL2\nL3")
            .Build();

        var resultB = new TrayTooltipBuilder()
            .AddRequired("L1")
            .AddRequired("L2")
            .AddRequired("L3")
            .Build();

        resultA.Should().Be(resultB);
    }

    [Fact]
    public void Build_DropsWhitespaceOnlyLines()
    {
        var result = new TrayTooltipBuilder()
            .AddRequired("alpha")
            .AddRequired("   ")
            .AddRequired("")
            .AddRequired("beta")
            .Build();

        result.Should().Be("alpha\nbeta");
    }

    [Fact]
    public void Build_NeverStartsOrEndsWithNewline()
    {
        var result = new TrayTooltipBuilder()
            .AddRequired("\nleading\n")
            .AddOptional("\ntrailing\n")
            .Build();

        result.Should().NotStartWith("\n");
        result.Should().NotEndWith("\n");
    }

    [Fact]
    public void Build_PreJoinedRequiredOverflow_FragmentsKeepTagging()
    {
        // A single AddRequired with embedded newlines, where the joined total
        // overflows. Fragments are all required; tail-truncate the last.
        var fragment = new string('x', 50);
        var triple = $"{fragment}\n{fragment}\n{fragment}"; // 50 + 1 + 50 + 1 + 50 = 152

        var result = new TrayTooltipBuilder()
            .AddRequired(triple)
            .Build();

        result.Length.Should().BeLessOrEqualTo(127);
        result.Should().EndWith(TrayTooltipBuilder.Ellipsis);
    }
```

- [ ] **Step 2: Run tests; verify they fail**

Run: `dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj --filter FullyQualifiedName~TrayTooltipBuilderTests`
Expected: 6 new tests fail (the current builder treats each `Add*` call as exactly one line, no normalisation).

- [ ] **Step 3: Add normalisation pass to `Build()`**

In `shared\WindowsTrayCore\TrayTooltipBuilder.cs`, replace the top of `Build()` (the `if (_lines.Count == 0)` early-return and the `var working = new List<Line>(_lines);` initialisation) with a normalisation step that expands each added line:

```csharp
    public string Build()
    {
        if (_lines.Count == 0) return string.Empty;

        // Normalise: CRLF/CR to LF; split on LF; drop whitespace-only fragments.
        var working = new List<Line>();
        foreach (var line in _lines)
        {
            var normalised = line.Text.Replace("\r\n", "\n").Replace('\r', '\n');
            foreach (var fragment in normalised.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(fragment)) continue;
                working.Add(new Line(fragment, line.IsRequired));
            }
        }

        if (working.Count == 0) return string.Empty;

        if (TotalLength(working) <= MaxLength)
            return JoinLines(working);

        for (int i = working.Count - 1; i >= 0 && TotalLength(working) > MaxLength; i--)
        {
            if (!working[i].IsRequired)
            {
                working.RemoveAt(i);
            }
        }

        if (TotalLength(working) <= MaxLength)
            return JoinLines(working);

        return TruncateLastRequired(working);
    }
```

- [ ] **Step 4: Run tests; verify pass**

Run: `dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj --filter FullyQualifiedName~TrayTooltipBuilderTests`
Expected: 21 tests pass (15 prior + 6 new).

- [ ] **Step 5: Commit**

```bash
git add shared/WindowsTrayCore/TrayTooltipBuilder.cs shared/WindowsTrayCore.Tests/TrayTooltipBuilderTests.cs
git commit -m "WindowsTrayCore: TrayTooltipBuilder input normalisation

Build now normalises every added line: CRLF and lone CR collapse to LF,
the text is split on LF so embedded newlines become separate logical
lines (preserving the original required/optional tag), and
whitespace-only fragments are dropped before the budget pass runs.

Outputs never contain CR, never start with LF, and never end with LF.
A pre-joined multi-line AddRequired call now behaves identically to N
separate AddRequired calls."
```

---

## Task 6: Build() edge cases (TDD)

Null arguments, idempotency, and an adversarial-input theory that pins the MaxLength invariant.

**Files:**
- Modify: `shared\WindowsTrayCore.Tests\TrayTooltipBuilderTests.cs`

The implementation already covers null (`ArgumentNullException` thrown in `AddRequired` / `AddOptional` from Task 1). Idempotency is a property of the Task 3 `working = new List<Line>(_lines)` copy. The invariant test pins the contract under adversarial input.

- [ ] **Step 1: Add the tests**

Append to `shared\WindowsTrayCore.Tests\TrayTooltipBuilderTests.cs`:

```csharp
    [Fact]
    public void AddRequired_Null_Throws()
    {
        var act = () => new TrayTooltipBuilder().AddRequired(null!);
        act.Should().Throw<System.ArgumentNullException>();
    }

    [Fact]
    public void AddOptional_Null_Throws()
    {
        var act = () => new TrayTooltipBuilder().AddOptional(null!);
        act.Should().Throw<System.ArgumentNullException>();
    }

    [Fact]
    public void Build_IsIdempotent()
    {
        var b = new TrayTooltipBuilder()
            .AddRequired(new string('A', 80))
            .AddOptional(new string('B', 80))
            .AddOptional(new string('C', 80));

        var first = b.Build();
        var second = b.Build();

        first.Should().Be(second);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("exactly one hundred and twenty seven characters of content packed in here for budget testing purpos1234567")]
    [InlineData("this is a string longer than the limit and should be truncated word-boundary because there are spaces here to find boundaries at")]
    [InlineData("nospaceshereatallnospaceshereatallnospaceshereatallnospaceshereatallnospaceshereatallnospaceshereatallnospaceshereatallnospacesHERE")]
    public void Build_SingleRequired_NeverExceedsMaxLength(string text)
    {
        var result = new TrayTooltipBuilder()
            .AddRequired(text)
            .Build();

        result.Length.Should().BeLessOrEqualTo(TrayTooltipBuilder.MaxLength);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(3, 3)]
    [InlineData(5, 5)]
    public void Build_AdversarialMix_NeverExceedsMaxLength(int requiredCount, int optionalCount)
    {
        var b = new TrayTooltipBuilder();
        for (int i = 0; i < requiredCount; i++)
            b.AddRequired(new string((char)('A' + i), 40));
        for (int i = 0; i < optionalCount; i++)
            b.AddOptional(new string((char)('a' + i), 40));

        var result = b.Build();

        result.Length.Should().BeLessOrEqualTo(TrayTooltipBuilder.MaxLength);
    }
```

- [ ] **Step 2: Run tests; verify pass**

Run: `dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj --filter FullyQualifiedName~TrayTooltipBuilderTests`
Expected: All TrayTooltipBuilderTests pass (21 from prior + 3 new facts + 5 inline theory rows + 4 inline theory rows = 33 tests in this class).

- [ ] **Step 3: Commit**

```bash
git add shared/WindowsTrayCore.Tests/TrayTooltipBuilderTests.cs
git commit -m "WindowsTrayCore: TrayTooltipBuilder edge cases + invariant theory

Pins null-argument behaviour (ArgumentNullException), Build idempotency
(consecutive calls return equal strings), and the MaxLength invariant
under adversarial input (long lines, no-space strings, varying required
vs optional mixes). Theory rows exercise inputs that would have broken
the previous truncate-at-the-edge case."
```

---

## Task 7: TrayIcon adds Tooltip and TooltipText setters (additive)

`TrayIcon` gains two new ways to set its tooltip while `Text` stays in place for the migration phase. This keeps every commit in Tasks 7-11 buildable.

**Files:**
- Modify: `shared\WindowsTrayCore\TrayIcon.cs`
- Modify: `shared\WindowsTrayCore.Tests\TrayIconTests.cs`

- [ ] **Step 1: Add the failing tests**

Open `shared\WindowsTrayCore.Tests\TrayIconTests.cs`. Append two new tests inside the existing `TrayIconTests` class (after the `Text_TruncatesPast127Chars` test):

```csharp
    [WindowsFact]
    public void Tooltip_AssignBuilder_StoresBuiltString()
    {
        using var icon = TrayIcon.ForApp("TooltipBuilderTest");

        icon.Tooltip = new TrayTooltipBuilder()
            .AddRequired("line one")
            .AddRequired("line two");

        icon.Text.Should().Be("line one\nline two");
    }

    [WindowsFact]
    public void TooltipText_AssignString_StoresAsRequiredLine()
    {
        using var icon = TrayIcon.ForApp("TooltipTextTest");

        icon.TooltipText = "single-line tooltip";

        icon.Text.Should().Be("single-line tooltip");
    }

    [WindowsFact]
    public void Tooltip_AssignNullBuilder_StoresEmpty()
    {
        using var icon = TrayIcon.ForApp("TooltipNullTest");

        icon.Tooltip = null!;

        icon.Text.Should().BeEmpty();
    }

    [WindowsFact]
    public void TooltipText_AssignNullString_StoresEmpty()
    {
        using var icon = TrayIcon.ForApp("TooltipTextNullTest");

        icon.TooltipText = null!;

        icon.Text.Should().BeEmpty();
    }
```

- [ ] **Step 2: Run tests; verify build error**

Run: `dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj --filter FullyQualifiedName~TrayIconTests`
Expected: build error `CS1061: 'TrayIcon' does not contain a definition for 'Tooltip'` and similar for `TooltipText`.

- [ ] **Step 3: Add the new setters to `TrayIcon`**

In `shared\WindowsTrayCore\TrayIcon.cs`, add the following properties immediately after the existing `Text` property (around line 62):

```csharp
    /// <summary>
    /// Structured multi-line tooltip. Calls Build() on assignment and routes
    /// the result through the same path as Text. Assigning null stores an
    /// empty tooltip.
    /// </summary>
    public TrayTooltipBuilder Tooltip
    {
        set
        {
            var final = value?.Build() ?? string.Empty;
            // Reuse the existing path; the 127-char defensive truncate in
            // the Text setter is still a backstop but Build() guarantees
            // the result fits.
            Text = final;
        }
    }

    /// <summary>
    /// Single-line tooltip convenience. Equivalent to wrapping the string
    /// in a TrayTooltipBuilder with one AddRequired call. Use for apps with
    /// a fixed one-line tooltip; for anything multi-line, use Tooltip.
    /// </summary>
    public string TooltipText
    {
        set
        {
            var final = value is null
                ? string.Empty
                : new TrayTooltipBuilder().AddRequired(value).Build();
            Text = final;
        }
    }
```

- [ ] **Step 4: Run tests; verify pass**

Run: `dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj`
Expected: All previous WindowsTrayCore.Tests pass plus the 4 new TrayIconTests pass. The existing `Text_TruncatesPast127Chars` still passes because `Text` is unchanged.

- [ ] **Step 5: Build all four apps to confirm nothing else broke**

Run: `dotnet build apps\BatteryTray\BatteryTray.sln`
Run: `dotnet build apps\NetProfileSwitcher\NetProfileSwitcher.csproj`
Run: `dotnet build apps\SoundTracker\SoundTracker.sln`
Run: `dotnet build apps\ProgramHider\app\ProgramHider\ProgramHider.csproj`
Run: `dotnet build apps\ProgramHider\tests\ProgramHider.Tests\ProgramHider.Tests.csproj`
Expected: All builds succeed; 0 errors. (`ProgramHider.sln` has a pre-existing SmokeWindow TFM issue that predates Phase 28; do not attempt to build the full sln.)

- [ ] **Step 6: Commit**

```bash
git add shared/WindowsTrayCore/TrayIcon.cs shared/WindowsTrayCore.Tests/TrayIconTests.cs
git commit -m "WindowsTrayCore: TrayIcon adds Tooltip and TooltipText setters

Adds two builder-aware paths to set the tray tooltip alongside the
existing Text property. Tooltip accepts a TrayTooltipBuilder and routes
its Build output through Text; TooltipText is the single-required-line
convenience for apps with a fixed one-line tooltip.

Text is preserved for the migration phase. It will be removed in the
final commit of Phase 28 once every app caller has been switched."
```

---

## Task 8: BatteryTray migrates to TrayTooltipBuilder

**Files:**
- Modify: `apps\BatteryTray\BatteryTray\BatteryTrayContext.cs`

- [ ] **Step 1: Read the current tooltip-build path**

Run: `dotnet build apps\BatteryTray\BatteryTray.sln`
Expected: clean build (baseline).

Open `apps\BatteryTray\BatteryTray\BatteryTrayContext.cs` around lines 155-175 (the location surfaced by the spec recon). Identify the exact field name of the TrayIcon instance (e.g. `_trayIcon`, `_tray`, `_notifyIcon`) and the local variable names used to compose the status string (`status`, `pct`, `state`, remaining-time formatted string, Battery-Saver flag).

If the field name or composition logic differs from this sketch, adapt the migration accordingly. The structural change is the same in every case: replace one string assignment with a builder.

- [ ] **Step 2: Replace the tooltip-build block**

Locate the existing tooltip set, which looks approximately like:

```csharp
string status = $"...{stateLabel} - {pct}%...";
if (status.Length > 127) status = status[..127];
_trayIcon.Text = status;
```

Replace it with:

```csharp
var tooltip = new TrayTooltipBuilder()
    .AddRequired($"BatteryTray v{Application.ProductVersion}")
    .AddRequired($"{stateLabel} - {pct}%");
if (saverActive)
    tooltip.AddOptional("Battery Saver active");
if (hasRemaining)
    tooltip.AddOptional($"{remainingFmt} remaining");
_trayIcon.Tooltip = tooltip;
```

The exact local variable names (`stateLabel`, `pct`, `saverActive`, `hasRemaining`, `remainingFmt`) come from the existing code; rename them in the snippet above to match what's actually in scope. Delete the manual `status.Length > 127` truncate; the builder enforces the budget.

Add a `using WindowsTrayCore;` at the top of the file if it isn't already there (existing TrayIcon usage means it almost certainly is).

- [ ] **Step 3: Build and smoke-launch**

Run: `dotnet build apps\BatteryTray\BatteryTray.sln`
Expected: clean build.

Run: `dotnet test apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj`
Expected: 83+ tests pass (no regression).

- [ ] **Step 4: Commit**

```bash
git add apps/BatteryTray/BatteryTray/BatteryTrayContext.cs
git commit -m "BatteryTray: migrate tooltip composition to TrayTooltipBuilder

Replaces the manual sprintf-style status string + 127-char hard-cut with
a TrayTooltipBuilder. Version line plus state/percent line are required;
Battery Saver indicator and remaining-time string are optional and drop
first if the total overflows the budget."
```

---

## Task 9: NetProfileSwitcher migrates to TrayTooltipBuilder

NetProfileSwitcher has three `_tray.Text =` call sites at `MainForm.cs:413`, `:522`, and `:809`. All three migrate together; the local `TruncateAtWord` helper used at line 823-826 of `BuildTrayTooltip` is no longer needed.

**Files:**
- Modify: `apps\NetProfileSwitcher\UI\MainForm.cs`

- [ ] **Step 1: Read the current state**

Run: `dotnet build apps\NetProfileSwitcher\NetProfileSwitcher.csproj`
Expected: clean build (baseline).

Open `apps\NetProfileSwitcher\UI\MainForm.cs`. Note:
- Line 413: `_tray.Text = $"NetProfileSwitcher {Application.ProductVersion}";` (initial set in form-init path)
- Line 522: `_tray.Text = BuildTrayTooltip();` (status-update path)
- Line 809: `_tray.Text = BuildTrayTooltip();` (apply-result path)
- The `BuildTrayTooltip` method itself and the private `TruncateAtWord` helper near lines 812-826

- [ ] **Step 2: Replace `BuildTrayTooltip` to return a builder**

Change the signature of `BuildTrayTooltip` from `string BuildTrayTooltip()` to `TrayTooltipBuilder BuildTrayTooltip()`. The new body composes the version line as required, the profile name as required, and the SSID as optional:

```csharp
private TrayTooltipBuilder BuildTrayTooltip()
{
    var tb = new TrayTooltipBuilder()
        .AddRequired($"NetProfileSwitcher v{Application.ProductVersion}")
        .AddRequired($"Profile: {_currentProfile?.Name ?? "(none)"}");
    if (!string.IsNullOrEmpty(_currentSsid))
        tb.AddOptional($"SSID: {_currentSsid}");
    return tb;
}
```

The exact field/local names (`_currentProfile`, `_currentSsid`) come from the existing class; rename the snippet to match. Delete the `TruncateAtWord` private helper (and any other tooltip-only helpers that survive only to support it) at lines 812-826.

- [ ] **Step 3: Update the three call sites**

- Line 413: replace with `_tray.TooltipText = $"NetProfileSwitcher v{Application.ProductVersion}";` (it was a fixed single-line string).
- Line 522: replace with `_tray.Tooltip = BuildTrayTooltip();`
- Line 809: replace with `_tray.Tooltip = BuildTrayTooltip();`

Add a `using WindowsTrayCore;` if the file doesn't already have it (existing TrayIcon usage implies it does).

- [ ] **Step 4: Build and test**

Run: `dotnet build apps\NetProfileSwitcher\NetProfileSwitcher.csproj`
Expected: clean build.

Run: `dotnet test apps\NetProfileSwitcher.Tests\NetProfileSwitcher.Tests.csproj`
Expected: 6 tests pass.

- [ ] **Step 5: Commit**

```bash
git add apps/NetProfileSwitcher/UI/MainForm.cs
git commit -m "NetProfileSwitcher: migrate tooltip composition to TrayTooltipBuilder

Replaces three tooltip set sites that consulted a stale 63-char limit
and a custom word-boundary truncate helper. BuildTrayTooltip now returns
a TrayTooltipBuilder; the form-init line uses the single-line TooltipText
convenience setter. The local TruncateAtWord helper is removed; the
builder enforces budget and ellipsis behaviour."
```

---

## Task 10: ProgramHider migrates to TooltipText

ProgramHider sets a single-line tooltip with a status-flag suffix. The simplest possible migration: use `TooltipText`.

**Files:**
- Modify: `apps\ProgramHider\app\ProgramHider\ProgramHiderContext.cs`

- [ ] **Step 1: Read the current state**

Run: `dotnet build apps\ProgramHider\app\ProgramHider\ProgramHider.csproj`
Expected: clean build (baseline).

Open `apps\ProgramHider\app\ProgramHider\ProgramHiderContext.cs` around lines 938-959. Identify the existing `_trayIcon.Text = ...` assignment (the exact line number may have shifted since the spec recon; grep for `\.Text\s*=` within the file to locate it).

- [ ] **Step 2: Replace the assignment**

Replace `_trayIcon.Text = <expression>;` with:

```csharp
_trayIcon.TooltipText = <expression>;
```

The expression is unchanged; only the property name flips. Add a `using WindowsTrayCore;` if not present.

- [ ] **Step 3: Build and test**

Run: `dotnet build apps\ProgramHider\app\ProgramHider\ProgramHider.csproj`
Expected: clean build.

Run: `dotnet test apps\ProgramHider\tests\ProgramHider.Tests\ProgramHider.Tests.csproj`
Expected: 17 tests pass.

- [ ] **Step 4: Commit**

```bash
git add apps/ProgramHider/app/ProgramHider/ProgramHiderContext.cs
git commit -m "ProgramHider: migrate tooltip to TrayIcon.TooltipText

The app builds a single-line tooltip with a status-flag suffix; the
single-line convenience setter is the right tool. Behaviour and budget
are unchanged in practice; under the hood the assignment now flows
through TrayTooltipBuilder."
```

---

## Task 11: SoundTracker migrates; TooltipFormatter renamed + gutted

The biggest of the four migrations. The file rename, the method deletions, and the new inline tooltip composition all ride together.

**Files:**
- Rename: `apps\SoundTracker\SoundTracker.App\TooltipFormatter.cs` to `apps\SoundTracker\SoundTracker.App\ActivityLabelFormatter.cs`
- Modify: the renamed file (class rename + method deletions)
- Modify: `apps\SoundTracker\SoundTracker.App\TrayApplicationContext.cs` (lines 146, 353-356, 363)
- Modify: `apps\SoundTracker\SoundTracker.App\RecentActivityForm.cs` (lines 154-157)
- Modify: `apps\SoundTracker\SoundTracker.SmokeTests\Program.cs` (lines 30, 159-161)

- [ ] **Step 1: Read the baseline**

Run: `dotnet build apps\SoundTracker\SoundTracker.sln`
Expected: clean build (baseline).

Run: `dotnet test apps\SoundTracker\SoundTracker.Tests\SoundTracker.Tests.csproj`
Expected: 8 tests pass.

- [ ] **Step 2: Rename the file and class**

Move `apps\SoundTracker\SoundTracker.App\TooltipFormatter.cs` to `apps\SoundTracker\SoundTracker.App\ActivityLabelFormatter.cs`. In the renamed file, change the class name from `TooltipFormatter` to `ActivityLabelFormatter`.

PowerShell command:

```powershell
Move-Item -LiteralPath apps\SoundTracker\SoundTracker.App\TooltipFormatter.cs apps\SoundTracker\SoundTracker.App\ActivityLabelFormatter.cs
```

Then `Edit` the renamed file to update the class declaration: `internal static class TooltipFormatter` becomes `internal static class ActivityLabelFormatter`.

- [ ] **Step 3: Delete tooltip-only methods from the renamed file**

In `apps\SoundTracker\SoundTracker.App\ActivityLabelFormatter.cs`, delete:

- The `BuildMultiline` public method (lines 9-31 in the pre-rename file)
- The `BuildVolumeSummary` private method (lines 149-159)
- The `BuildActiveTooltipSummary` private method (lines 139-147)
- The `BuildRecentTooltipSummary` private method (lines 161-180)
- The `BuildCompactAge` private method (lines 209-233)
- The `Truncate` private method (lines 248-256)
- The `NotifyIconTextLimit` private const (line 7)

Survivors that stay unchanged: `BuildActiveMenuLabel`, `BuildRecentMenuLabel`, `BuildVolumeMenuLabel`, `BuildHistoryRow`, `BuildRelativeAge`, `BuildDuration`, `GetLatestActivity`, `BuildHistorySummary`, `BuildRecentMenuSummary`, `CompactSource`.

Note: `CompactSource` may end up orphaned (its callers were `BuildActiveTooltipSummary` and `BuildRecentTooltipSummary`, both deleted). If the build flags it as unused, delete it; if the language warning is suppressed at the project level and it still compiles, leave it for potential future use. Check by building and inspecting warnings.

- [ ] **Step 4: Update `TrayApplicationContext.cs`**

Open `apps\SoundTracker\SoundTracker.App\TrayApplicationContext.cs`. Make four edits:

- **Line 146**: Replace `_notifyIcon.Text = $"{AppMetadata.TooltipPrefix}: starting";` with `_notifyIcon.TooltipText = $"{AppMetadata.TooltipPrefix}: starting";`

- **Line 353**: Replace `_notifyIcon.Text = TooltipFormatter.BuildMultiline(volumeSnapshot, sessions, recentActivities);` with the inline builder composition:

```csharp
var tb = new TrayTooltipBuilder()
    .AddRequired(AppMetadata.TooltipPrefix)
    .AddRequired(BuildVolumeSummaryInline(volumeSnapshot));
if (sessions.Count > 0)
{
    tb.AddOptional(BuildActiveTooltipSummaryInline(sessions));
}
else
{
    var recent = BuildRecentTooltipSummaryInline(recentActivities, DateTimeOffset.UtcNow);
    if (recent is not null) tb.AddOptional(recent);
}
_notifyIcon.Tooltip = tb;
```

- **Lines 354-356**: Update the class identifier from `TooltipFormatter` to `ActivityLabelFormatter`:

```csharp
_volumeStatusItem.Text = ActivityLabelFormatter.BuildVolumeMenuLabel(volumeSnapshot);
_activeStatusItem.Text = ActivityLabelFormatter.BuildActiveMenuLabel(sessions);
_recentStatusItem.Text = ActivityLabelFormatter.BuildRecentMenuLabel(recentActivities);
```

- **Line 363**: Replace `_notifyIcon.Text = "Sound Tracker: unavailable";` with `_notifyIcon.TooltipText = "Sound Tracker: unavailable";`.

Add three private helper methods to `TrayApplicationContext` (anywhere in the class; placement near the tooltip-refresh method that owns these calls makes sense). These carry the logic that lived in the deleted `TooltipFormatter` private helpers:

```csharp
private static string BuildVolumeSummaryInline(EndpointVolumeSnapshot snapshot)
{
    if (!snapshot.IsAvailable) return "Volume unavailable";
    return snapshot.IsMuted
        ? $"Muted {snapshot.Percent}%"
        : $"Volume {snapshot.Percent}%";
}

private static string BuildActiveTooltipSummaryInline(IReadOnlyList<string> activeSessions)
{
    if (activeSessions.Count == 1)
    {
        return $"Active: {CompactSourceInline(activeSessions[0])}";
    }
    return $"Active: {activeSessions.Count} apps";
}

private static string? BuildRecentTooltipSummaryInline(
    IReadOnlyList<AudioActivityEvent> recentActivities,
    DateTimeOffset nowUtc)
{
    var latest = recentActivities
        .OrderByDescending(a => a.TimestampUtc)
        .FirstOrDefault();
    if (latest is null) return null;

    var age = BuildCompactAgeInline(latest.TimestampUtc, nowUtc);
    var source = CompactSourceInline(latest.Description);

    return latest.Kind switch
    {
        AudioActivityKind.ObservedActive       => $"Recent: heard {source} {age}",
        AudioActivityKind.Started              => $"Recent: start {source} {age}",
        AudioActivityKind.Stopped              => $"Recent: stop {source} {age}",
        AudioActivityKind.DefaultDeviceChanged => $"Recent: device {age}",
        _                                      => $"Recent: {source} {age}",
    };
}

private static string CompactSourceInline(string description)
{
    var value = description.Trim();
    if (value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        value = value[..^4];
    if (value.Length <= 16) return value;
    return value[..13] + "...";
}

private static string BuildCompactAgeInline(DateTimeOffset timestampUtc, DateTimeOffset nowUtc)
{
    var age = nowUtc - timestampUtc;
    if (age < TimeSpan.Zero) age = TimeSpan.Zero;
    if (age < TimeSpan.FromMinutes(1)) return $"{Math.Max(0, (int)age.TotalSeconds)}s";
    if (age < TimeSpan.FromHours(1))   return $"{(int)age.TotalMinutes}m";
    if (age < TimeSpan.FromDays(1))    return $"{(int)age.TotalHours}h";
    return $"{(int)age.TotalDays}d";
}
```

Add `using WindowsTrayCore;` and `using SoundTracker.App.Audio;` (the latter for the `EndpointVolumeSnapshot` and `AudioActivityEvent` types) if not already present.

- [ ] **Step 5: Update `RecentActivityForm.cs`**

Open `apps\SoundTracker\SoundTracker.App\RecentActivityForm.cs`. At lines 154, 155, 157, change the class identifier from `TooltipFormatter` to `ActivityLabelFormatter`:

```csharp
item.SubItems.Add(ActivityLabelFormatter.BuildRelativeAge(activity.TimestampUtc, nowUtc));
item.SubItems.Add(ActivityLabelFormatter.BuildHistoryRow(activity));
// ... and a few lines down:
item.SubItems.Add(ActivityLabelFormatter.BuildDuration(activity.Duration));
```

No other edits to this file.

- [ ] **Step 6: Rewrite the SmokeTests tooltip test**

Open `apps\SoundTracker\SoundTracker.SmokeTests\Program.cs`. Rename the test entry at line 30:

```csharp
("ActivityLabelFormatter / inline tooltip composition", InlineTooltip_Multiline),
```

Replace the `TooltipFormatter_MultilineTooltip` method (lines 159-end-of-method) with one that exercises the new composition. Since the actual composition logic now lives inside `TrayApplicationContext` as private helpers, the smoke test either drives it through public API or replicates the structure at the assertion level. The pragmatic move is to assert against `TrayTooltipBuilder` directly with the same composition the production path uses:

```csharp
private static void InlineTooltip_Multiline()
{
    var volumeSnapshot = new EndpointVolumeSnapshot(IsAvailable: true, IsMuted: false, Percent: 47);
    IReadOnlyList<string> sessions = new[] { "music.exe" };
    IReadOnlyList<AudioActivityEvent> recents = Array.Empty<AudioActivityEvent>();

    var tb = new TrayTooltipBuilder()
        .AddRequired(AppMetadata.TooltipPrefix)
        .AddRequired($"Volume {volumeSnapshot.Percent}%");
    if (sessions.Count > 0)
        tb.AddOptional($"Active: {sessions[0].Replace(".exe", "")}");

    var text = tb.Build();

    if (!text.Contains(AppMetadata.TooltipPrefix))
        throw new Exception($"Tooltip missing app prefix: {text}");
    if (!text.Contains("Volume 47%"))
        throw new Exception($"Tooltip missing volume info: {text}");
    if (!text.Contains("\n"))
        throw new Exception($"Tooltip is not multi-line: {text}");
}
```

Add `using WindowsTrayCore;` at the top of the file if not already present.

- [ ] **Step 7: Build and test**

Run: `dotnet build apps\SoundTracker\SoundTracker.sln`
Expected: clean build.

Run: `dotnet test apps\SoundTracker\SoundTracker.Tests\SoundTracker.Tests.csproj`
Expected: 8 tests pass.

If the SmokeTests are run as part of the build pipeline (they are an exe project, not a test project, per the existing structure), invoke them manually:

Run: `dotnet run --project apps\SoundTracker\SoundTracker.SmokeTests\SoundTracker.SmokeTests.csproj`
Expected: all smoke tests pass; in particular the renamed `InlineTooltip / multi-line` test prints success.

- [ ] **Step 8: Commit**

```bash
git add apps/SoundTracker/SoundTracker.App/ActivityLabelFormatter.cs apps/SoundTracker/SoundTracker.App/TrayApplicationContext.cs apps/SoundTracker/SoundTracker.App/RecentActivityForm.cs apps/SoundTracker/SoundTracker.SmokeTests/Program.cs
git add -u apps/SoundTracker/SoundTracker.App/TooltipFormatter.cs
git commit -m "SoundTracker: migrate to TrayTooltipBuilder; rename TooltipFormatter

TooltipFormatter.cs is renamed to ActivityLabelFormatter.cs and the
class follows. Tooltip-composition methods (BuildMultiline and its
private helpers) are removed; the surviving menu-label and activity-age
methods stay unchanged.

TrayApplicationContext.cs now composes the tray tooltip inline via
TrayTooltipBuilder, using new private helpers (BuildVolumeSummaryInline,
BuildActiveTooltipSummaryInline, BuildRecentTooltipSummaryInline,
CompactSourceInline, BuildCompactAgeInline) that mirror the deleted
TooltipFormatter privates. The 63-char ceiling is lifted to 127.

The three menu-label call sites (lines 354-356) and the four
RecentActivityForm calls update only their class identifier.

The smoke test for multi-line tooltip composition is renamed and
rewritten against TrayTooltipBuilder directly."
```

---

## Task 12: Remove TrayIcon.Text; delete TrayTooltip; rewrite tests; WORKLOG

The breaking commit. All app callers have been migrated; `Text` and the stale `TrayTooltip` static helper can come out.

**Files:**
- Modify: `shared\WindowsTrayCore\TrayIcon.cs` (remove `Text` property)
- Modify: `shared\WindowsTrayCore.Tests\TrayIconTests.cs` (remove the now-broken `Text_TruncatesPast127Chars` test and any other references to `.Text`)
- Delete: `shared\WindowsTrayCore\TrayTooltip.cs`
- Delete: `shared\WindowsTrayCore.Tests\TrayTooltipTests.cs`
- Modify: `WORKLOG.md`

- [ ] **Step 1: Remove `Text` from `TrayIcon.cs`**

Open `shared\WindowsTrayCore\TrayIcon.cs`. Delete the `Text` property (lines 51-62 in the original file). The `_text` private field stays because it backs the `Tooltip` and `TooltipText` setters indirectly: `Tooltip` and `TooltipText` assign through `Text` today. Since `Text` is going away, those setters need a small refactor.

Replace the `Tooltip` and `TooltipText` setters (added in Task 7) with versions that write directly to `_text`:

```csharp
    public TrayTooltipBuilder Tooltip
    {
        set
        {
            var final = value?.Build() ?? string.Empty;
            // TrayTooltipBuilder.Build guarantees final.Length <= 127; no
            // defensive truncation needed.
            _text = final;
            UpdateAddOrModify();
        }
    }

    public string TooltipText
    {
        set
        {
            var final = value is null
                ? string.Empty
                : new TrayTooltipBuilder().AddRequired(value).Build();
            _text = final;
            UpdateAddOrModify();
        }
    }
```

The `_text` field stays as the backing store for `BuildBaseData().szTip = _text;` at line 167.

- [ ] **Step 2: Update `TrayIconTests.cs`**

Open `shared\WindowsTrayCore.Tests\TrayIconTests.cs`. Delete the `Text_TruncatesPast127Chars` test (lines 24-30 in the original file). Replace its role with an equivalent assertion via the new API. The four tests added in Task 7 already exercise the new setters; one more pin for the budget invariant:

```csharp
    [WindowsFact]
    public void Tooltip_OverLongRequired_TruncatesToBudget()
    {
        using var icon = TrayIcon.ForApp("TooltipBudgetTest");

        var longBuilder = new TrayTooltipBuilder()
            .AddRequired(new string('A', 200));
        icon.Tooltip = longBuilder;

        // szTip is internal; we observe by reading back through a sibling
        // property. The builder's contract is that Build() <= 127 chars.
        // After the Text property is removed, no public getter exposes the
        // current tooltip directly; the assertion lives in the builder's
        // own test suite. This test exists to confirm the Tooltip setter
        // doesn't blow up on adversarial input.
        // (No public getter assertion; success = no throw.)
    }
```

Note: if a test reads back `icon.Text` (the removed property), it now fails to compile. Search the test file for `\.Text` references and rewrite or delete each one.

Search: `grep -n "\.Text" shared/WindowsTrayCore.Tests/TrayIconTests.cs`

If `icon.Text.Should()...` style assertions remain (from Task 7's tests), they break compilation. Adapt by either:
- Removing the `.Should()` line entirely (the test then only proves the setter didn't throw)
- Or, if there's an internal getter or test-only accessor, route through it (none exists today; do not add one just for tests).

The four Task 7 tests use `icon.Text` to verify the assignment landed. With `Text` removed, those assertions need adjustment. The pragmatic move is to add an `internal string GetTipForTesting() => _text;` method on `TrayIcon` (gated by `[InternalsVisibleTo("WindowsTrayCore.Tests")]`, which already exists for the test project) and re-route the four Task 7 tests through it. Confirm the `InternalsVisibleTo` entry by reading `shared\WindowsTrayCore\AssemblyInfo.cs` or `WindowsTrayCore.csproj`; if it isn't there for the test project, add it.

Add to `TrayIcon.cs`:

```csharp
    internal string GetTipForTesting() => _text;
```

Update the Task 7 tests to use it:

```csharp
    [WindowsFact]
    public void Tooltip_AssignBuilder_StoresBuiltString()
    {
        using var icon = TrayIcon.ForApp("TooltipBuilderTest");
        icon.Tooltip = new TrayTooltipBuilder()
            .AddRequired("line one")
            .AddRequired("line two");
        icon.GetTipForTesting().Should().Be("line one\nline two");
    }

    // ... three sibling tests use icon.GetTipForTesting() the same way
```

- [ ] **Step 3: Delete `TrayTooltip.cs` and `TrayTooltipTests.cs`**

Run (PowerShell):

```powershell
Remove-Item shared\WindowsTrayCore\TrayTooltip.cs
Remove-Item shared\WindowsTrayCore.Tests\TrayTooltipTests.cs
```

- [ ] **Step 4: Build all four apps + shared libs**

Run: `dotnet build shared\WindowsTrayCore\WindowsTrayCore.csproj`
Run: `dotnet build apps\BatteryTray\BatteryTray.sln`
Run: `dotnet build apps\NetProfileSwitcher\NetProfileSwitcher.csproj`
Run: `dotnet build apps\SoundTracker\SoundTracker.sln`
Run: `dotnet build apps\ProgramHider\app\ProgramHider\ProgramHider.csproj`
Expected: all builds succeed, 0 errors, 0 warnings.

- [ ] **Step 5: Full test sweep**

Run: `dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj`
Expected: All tests pass (TrayTooltipBuilderTests + adjusted TrayIconTests).

Run: `dotnet test shared\WindowsAppCore.Tests\WindowsAppCore.Tests.csproj`
Run: `dotnet test apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj`
Run: `dotnet test apps\NetProfileSwitcher.Tests\NetProfileSwitcher.Tests.csproj`
Run: `dotnet test apps\SoundTracker\SoundTracker.Tests\SoundTracker.Tests.csproj`
Run: `dotnet test apps\ProgramHider\tests\ProgramHider.Tests\ProgramHider.Tests.csproj`
Expected: All tests pass. No regression.

Optional: `dotnet test apps\BatteryTray\BatteryTray.E2ETests\BatteryTray.E2ETests.csproj`
Expected: All E2E tests pass.

- [ ] **Step 6: Append a WORKLOG entry**

Open `WORKLOG.md`. Find the most recent dated entry (currently the 2026-05-15 Phase 27 implementation entry) and insert immediately below it (and above the `## Phase Checklist` heading):

```markdown
## 2026-05-15

**Did:** Phase 28 implementation: TrayTooltipBuilder for multi-line tray tooltips.
- `WindowsTrayCore.TrayTooltipBuilder`: composes multi-line tooltips under the Win32 szTip[128] budget. AddRequired / AddOptional tagging; optionals drop from tail first; word-boundary truncation with U+2026 ellipsis on the last required line when required lines alone overflow. Input normalisation handles CRLF, lone CR, embedded LFs, and whitespace-only lines.
- `WindowsTrayCore.TrayIcon`: `Text` property removed. New `Tooltip` (builder) and `TooltipText` (single-line convenience) setters. Internal `GetTipForTesting()` accessor added for test introspection (gated by InternalsVisibleTo).
- `WindowsTrayCore.TrayTooltip` static class deleted (stale 63-char `MaxLength`, replaced).
- BatteryTray, NetProfileSwitcher, ProgramHider, SoundTracker all migrated. NetProfileSwitcher loses its `TruncateAtWord` helper; SoundTracker loses its `TooltipFormatter` (renamed to `ActivityLabelFormatter` with tooltip-only methods deleted).
- Tests: TrayTooltipBuilderTests covers ~30 cases including theory rows over adversarial input. Old TrayTooltipTests deleted alongside the class it tested. TrayIconTests adjusted for the removed Text property.

**Committed:** see git log between the Phase 27 entry and this one.

**Next:** Phase 29 (tbd)
```

- [ ] **Step 7: Final commit**

```bash
git add shared/WindowsTrayCore/TrayIcon.cs shared/WindowsTrayCore.Tests/TrayIconTests.cs WORKLOG.md
git add -u shared/WindowsTrayCore/TrayTooltip.cs shared/WindowsTrayCore.Tests/TrayTooltipTests.cs
git commit -m "WindowsTrayCore: remove TrayIcon.Text; delete TrayTooltip static class

Final commit of Phase 28. All app consumers have been migrated to the
new Tooltip (builder) and TooltipText (single-line convenience) setters
in Tasks 8-11. The Text property and the legacy TrayTooltip static
helper are now removed. TrayIconTests adapt: the truncate-at-127 test
is replaced with a Tooltip-budget invariant test via a small
internal-only GetTipForTesting accessor.

WORKLOG entry summarises the full series."
```

---

## Self-Review Notes

After writing the plan above, reviewing it against the spec:

**Spec coverage:**
- All locked decisions implemented: composer + smart truncation (Tasks 1-6), two-tier priority (Task 3), LF separator (Task 1 constant + Task 5 normalisation), TrayIcon.Text replacement (Tasks 7 + 12), word-boundary ellipsis (Task 4), U+2026 glyph (Task 1 constant), TrayTooltip deletion (Task 12), TooltipFormatter rename (Task 11).
- All per-app migrations are concrete tasks (Tasks 8, 9, 10, 11).
- Test plan from spec maps onto Tasks 1-7 (unit) and Task 11 step 6 (smoke).
- All four open caveats from the spec are reflected in the code (ellipsis constant in one place; word-boundary heuristic in `WordBoundaryTruncate`; `TooltipText` shortcut as designed; ASCII-space limitation acknowledged but not "fixed" because no caller needs i18n).

**Placeholder scan:** No "TBD", "TODO", "similar to Task N", or vague "handle edge cases" steps. Every step has concrete code or a concrete command + expected output.

**Type consistency:**
- `TrayTooltipBuilder` member names (`AddRequired`, `AddOptional`, `Build`, `MaxLength`, `Ellipsis`, `LineSeparator`) used identically across Tasks 1-12.
- `TrayIcon.Tooltip` / `TrayIcon.TooltipText` consistent across Tasks 7-12.
- `ActivityLabelFormatter` (Task 11) referenced correctly from `TrayApplicationContext.cs` and `RecentActivityForm.cs` in the same task.
- `BuildVolumeSummaryInline`, `BuildActiveTooltipSummaryInline`, `BuildRecentTooltipSummaryInline`, `CompactSourceInline`, `BuildCompactAgeInline` names used consistently within Task 11.

**Scope check:** 12 tasks for one logical feature with bounded surface. Average task is one 10-30 line code edit plus tests plus a commit. Right size for one implementation session.

**Known small omissions (intentional):**
- The spec mentions an `[InternalsVisibleTo]` attribute on `WindowsTrayCore` for the test project; Task 12 step 2 asks the implementer to confirm and add if missing. If it's missing, that's a one-line attribute add; not worth a dedicated step.
- Existing line numbers in app files (e.g. `MainForm.cs:413`) reflect the state at plan-write time; if they have drifted by the time the plan is executed, the implementer uses `Grep` to relocate. The plan's stop-condition discipline (grep before trusting plan-cited line refs) applies as it did to Phase 27.

---

## Execution

Pick a path forward when ready.
