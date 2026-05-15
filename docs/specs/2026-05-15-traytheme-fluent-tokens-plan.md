# TrayTheme Fluent Tokens Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restructure `WindowsTrayCore.TrayTheme` to expose 12 Fluent-style semantic tokens, follow the user's actual Windows accent color, react live to system theme and accent changes, and apply dark title bars on every form.

**Architecture:** New `ThemePreference` enum + `AccentColors` static helper. `TrayTheme` rewritten with the 12 tokens, an internal message window listening for `WM_SETTINGCHANGE` and `WM_DWMCOLORIZATIONCOLORCHANGED`, a `SetOverride(ThemePreference)` API, and `IsHighContrast` detection. `ThemeApplier` gains an `ApplyTitleBar` pass via `DwmSetWindowAttribute`. Migration runs additively: new tokens land alongside old, every consumer migrates, the old tokens are deleted in the final commit.

**Tech Stack:** .NET 8, WinForms, xUnit + FluentAssertions, P/Invoke to `dwmapi.dll`. No new packages.

**Spec:** `docs/specs/2026-05-15-traytheme-fluent-tokens.md`

---

## File Structure

### New files

| Path | Responsibility |
|---|---|
| `shared\WindowsTrayCore\ThemePreference.cs` | `Auto / Light / Dark` enum |
| `shared\WindowsTrayCore\AccentColors.cs` | DWM + registry accent reader; `AccentOn` and `AccentSubtle` derivation; WCAG luminance |
| `shared\WindowsTrayCore.Tests\AccentColorsTests.cs` | Unit tests for the helper |
| `shared\WindowsTrayCore.Tests\ThemePreferenceTests.cs` | Trivial enum sanity test |

### Modified files

| Path | Change |
|---|---|
| `shared\WindowsTrayCore\TrayTheme.cs` | Rewritten: 12 tokens, message window, override, high-contrast |
| `shared\WindowsTrayCore\ThemeApplier.cs` | New tokens; new `ApplyTitleBar` pass |
| `shared\WindowsTrayCore\Native\TrayNativeMethods.cs` | Add `DwmSetWindowAttribute`, `DwmGetColorizationColor` |
| `shared\WindowsTrayCore.Tests\TrayThemeTests.cs` | Rewritten for new tokens + override + simulation hooks |
| `shared\WindowsTrayCore.Tests\ThemeApplierTests.cs` | Rewritten for new tokens; title-bar test |
| `shared\WindowsTrayCore\AboutDialog.cs` | Token migration |
| `shared\WindowsTrayCore\FirstRunBalloon.cs` | Token migration |
| `apps\BatteryTray\BatteryTray\BatteryTrayContext.cs` | Token migration |
| `apps\BatteryTray\BatteryTray\SettingsForm.cs` | Token migration + wire `_themeCombo` to `SetOverride` |
| `apps\NetProfileSwitcher\UI\MainForm.cs` | Token migration |
| `apps\ProgramHider\app\ProgramHider\ProgramHiderContext.cs` | Token migration |
| `apps\ProgramHider\app\ProgramHider\SettingsForm.cs` | Token migration |
| `apps\SoundTracker\SoundTracker.App\TrayApplicationContext.cs` | Token migration |
| `apps\SoundTracker\SoundTracker.App\SettingsForm.cs` | Token migration |
| `apps\SoundTracker\SoundTracker.App\RecentActivityForm.cs` | Token migration |
| `WORKLOG.md` | New entry on the final commit |

### Working directory

All paths relative to `D:\code\windows-apps\`. PowerShell or Bash both fine for build / test invocation.

---

## Task 1: ThemePreference enum (TDD)

Trivial type that the override API and the BatteryTray combo will both reference.

**Files:**
- Create: `shared\WindowsTrayCore\ThemePreference.cs`
- Create: `shared\WindowsTrayCore.Tests\ThemePreferenceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `shared\WindowsTrayCore.Tests\ThemePreferenceTests.cs`:

```csharp
using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class ThemePreferenceTests
{
    [Fact]
    public void Enum_HasThreeMembers_InExpectedOrder()
    {
        ((int)ThemePreference.Auto).Should().Be(0);
        ((int)ThemePreference.Light).Should().Be(1);
        ((int)ThemePreference.Dark).Should().Be(2);
    }
}
```

- [ ] **Step 2: Run tests; verify build error**

Run: `dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj --filter FullyQualifiedName~ThemePreferenceTests`
Expected: `CS0246: The type or namespace name 'ThemePreference' could not be found`.

- [ ] **Step 3: Create the enum**

Create `shared\WindowsTrayCore\ThemePreference.cs`:

```csharp
namespace WindowsTrayCore;

/// <summary>
/// Per-app theme override. <see cref="Auto"/> follows the system
/// AppsUseLightTheme flag; Light and Dark force the corresponding palette
/// regardless of system state.
/// </summary>
public enum ThemePreference
{
    Auto = 0,
    Light = 1,
    Dark = 2,
}
```

- [ ] **Step 4: Run tests; verify pass**

Expected: 1 test passes.

- [ ] **Step 5: Commit**

```bash
git add shared/WindowsTrayCore/ThemePreference.cs shared/WindowsTrayCore.Tests/ThemePreferenceTests.cs
git commit -m "WindowsTrayCore: ThemePreference enum

Per-app theme override flag. Auto follows the system theme; Light and
Dark force the corresponding palette regardless of system state.
Consumed by the upcoming TrayTheme.SetOverride API and the existing
unwired BatteryTray theme combo."
```

---

## Task 2: AccentColors pure helpers (TDD)

Pure functions: luminance, `DeriveOn`, `DeriveSubtle`. No I/O. Test-friendly.

**Files:**
- Create: `shared\WindowsTrayCore\AccentColors.cs` (initial skeleton with pure helpers; `Read()` added in Task 3)
- Create: `shared\WindowsTrayCore.Tests\AccentColorsTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `shared\WindowsTrayCore.Tests\AccentColorsTests.cs`:

```csharp
using System.Drawing;
using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class AccentColorsTests
{
    [Theory]
    [InlineData(0xFF, 0xFF, 0xFF, 1.0)]   // white
    [InlineData(0x00, 0x00, 0x00, 0.0)]   // black
    [InlineData(0x80, 0x80, 0x80, 0.21586)] // mid grey ~0.216
    public void Luminance_KnownValues_MatchWCAGFormula(int r, int g, int b, double expected)
    {
        var l = AccentColors.Luminance(Color.FromArgb(r, g, b));
        l.Should().BeApproximately(expected, 0.005);
    }

    [Theory]
    [InlineData(0xFB, 0xBF, 0x24)] // amber
    [InlineData(0xAD, 0xD8, 0xE6)] // light blue
    [InlineData(0xFF, 0xFF, 0xFF)] // white
    public void DeriveOn_LightAccent_ReturnsBlack(int r, int g, int b)
    {
        var on = AccentColors.DeriveOn(Color.FromArgb(r, g, b));
        on.Should().Be(Color.FromArgb(0, 0, 0));
    }

    [Theory]
    [InlineData(0x00, 0x78, 0xD4)] // Windows blue
    [InlineData(0x5A, 0x4F, 0xD4)] // old purple
    [InlineData(0x00, 0x00, 0x00)] // black
    public void DeriveOn_DarkAccent_ReturnsWhite(int r, int g, int b)
    {
        var on = AccentColors.DeriveOn(Color.FromArgb(r, g, b));
        on.Should().Be(Color.FromArgb(0xFF, 0xFF, 0xFF));
    }

    [Fact]
    public void DeriveSubtle_BlendsAccentAt24PercentOverSurface()
    {
        // 24% of (255, 0, 0) over (255, 255, 255):
        //   R: 0.24 * 255 + 0.76 * 255 = 255
        //   G: 0.24 * 0   + 0.76 * 255 = 193.8 -> 194
        //   B: 0.24 * 0   + 0.76 * 255 = 193.8 -> 194
        var accent = Color.FromArgb(0xFF, 0, 0);
        var surface = Color.FromArgb(0xFF, 0xFF, 0xFF);

        var subtle = AccentColors.DeriveSubtle(accent, surface);

        subtle.R.Should().Be(0xFF);
        subtle.G.Should().BeInRange((byte)0xC1, (byte)0xC3); // ~194 with rounding tolerance
        subtle.B.Should().BeInRange((byte)0xC1, (byte)0xC3);
    }

    [Fact]
    public void DeriveSubtle_AccentEqualSurface_PassesThrough()
    {
        var c = Color.FromArgb(0x80, 0x80, 0x80);
        AccentColors.DeriveSubtle(c, c).Should().Be(c);
    }
}
```

- [ ] **Step 2: Run tests; verify build error**

Run: `dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj --filter FullyQualifiedName~AccentColorsTests`
Expected: `CS0103: The name 'AccentColors' does not exist`.

- [ ] **Step 3: Create the skeleton with pure helpers**

Create `shared\WindowsTrayCore\AccentColors.cs`:

```csharp
using System;
using System.Drawing;

namespace WindowsTrayCore;

internal static class AccentColors
{
    /// <summary>
    /// WCAG 2.1 relative luminance for an sRGB color.
    /// Returns 0.0 (black) to 1.0 (white).
    /// </summary>
    internal static double Luminance(Color c)
    {
        static double Channel(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    /// <summary>
    /// Picks black or white text for adequate contrast against the supplied
    /// accent. Threshold: relative luminance > 0.55 yields black; else white.
    /// </summary>
    internal static Color DeriveOn(Color accent) =>
        Luminance(accent) > 0.55 ? Color.FromArgb(0, 0, 0) : Color.FromArgb(0xFF, 0xFF, 0xFF);

    /// <summary>
    /// Alpha-blends the accent at 24% opacity over the surface.
    /// Used for hover / focus rings.
    /// </summary>
    internal static Color DeriveSubtle(Color accent, Color surface)
    {
        const double alpha = 0.24;
        int r = (int)Math.Round(alpha * accent.R + (1 - alpha) * surface.R);
        int g = (int)Math.Round(alpha * accent.G + (1 - alpha) * surface.G);
        int b = (int)Math.Round(alpha * accent.B + (1 - alpha) * surface.B);
        return Color.FromArgb(Clamp(r), Clamp(g), Clamp(b));
    }

    private static int Clamp(int v) => v < 0 ? 0 : v > 255 ? 255 : v;
}
```

Note: `AccentColors` is `internal`; `[InternalsVisibleTo("WindowsTrayCore.Tests")]` is already wired (verified earlier in the codebase). If not, add it before building.

- [ ] **Step 4: Run tests; verify pass**

Expected: 11 tests pass (3 luminance theory rows + 3 light accent + 3 dark accent + 2 subtle facts).

- [ ] **Step 5: Commit**

```bash
git add shared/WindowsTrayCore/AccentColors.cs shared/WindowsTrayCore.Tests/AccentColorsTests.cs
git commit -m "WindowsTrayCore: AccentColors pure helpers

Luminance computes WCAG 2.1 relative luminance for color contrast
decisions. DeriveOn picks black or white text for readable contrast
against an accent. DeriveSubtle alpha-blends an accent at 24% over a
surface for hover / focus rings.

The accent-reading I/O path (DWM + registry) comes in the next commit."
```

---

## Task 3: AccentColors.Read (DWM + registry + fallback)

Adds the I/O path: read the system accent via DWM, fall back to registry, fall back to a fixed default.

**Files:**
- Modify: `shared\WindowsTrayCore\AccentColors.cs`
- Modify: `shared\WindowsTrayCore\Native\TrayNativeMethods.cs` (add `DwmGetColorizationColor`)

- [ ] **Step 1: Add the P/Invoke**

Open `shared\WindowsTrayCore\Native\TrayNativeMethods.cs` and add (near the other DllImport declarations):

```csharp
[DllImport("dwmapi.dll", PreserveSig = true)]
internal static extern int DwmGetColorizationColor(out uint pcrColorization, out bool pfOpaqueBlend);
```

- [ ] **Step 2: Add `Read()` to AccentColors**

Append to `shared\WindowsTrayCore\AccentColors.cs` (inside the class):

```csharp
    /// <summary>
    /// Reads the current Windows accent color. Tries DwmGetColorizationColor
    /// first (the documented API; tracks accent even when "Show accent color
    /// on title bars" is off). Falls back to the DWM AccentColor registry
    /// value, then to Windows' canonical blue (#0078D4) if both fail.
    /// </summary>
    public static Color Read()
    {
        // Path 1: DWM API.
        try
        {
            if (Native.TrayNativeMethods.DwmGetColorizationColor(out uint argb, out _) == 0)
            {
                // argb is 0xAARRGGBB; strip alpha.
                byte r = (byte)((argb >> 16) & 0xFF);
                byte g = (byte)((argb >> 8) & 0xFF);
                byte b = (byte)(argb & 0xFF);
                return Color.FromArgb(r, g, b);
            }
        }
        catch { /* swallow; fall through */ }

        // Path 2: registry.
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser
                .OpenSubKey(@"Software\Microsoft\Windows\DWM", writable: false);
            if (key?.GetValue("AccentColor") is int accent)
            {
                // Registry stores as 0xAABBGGRR (note: BGR not RGB).
                byte r = (byte)(accent & 0xFF);
                byte g = (byte)((accent >> 8) & 0xFF);
                byte b = (byte)((accent >> 16) & 0xFF);
                return Color.FromArgb(r, g, b);
            }
        }
        catch { /* swallow */ }

        // Path 3: canonical Windows blue.
        return Color.FromArgb(0x00, 0x78, 0xD4);
    }
```

- [ ] **Step 3: Add a smoke test**

Append to `AccentColorsTests.cs`:

```csharp
    [Fact]
    public void Read_OnRealMachine_ReturnsNonDefaultOrFallback()
    {
        // Smoke: contract is "never throws, returns some color".
        var act = () => AccentColors.Read();
        var color = act.Should().NotThrow().Subject;

        // We can't assert a specific value (it's the user's system pick),
        // but a fully-opaque color with A=255 is the contract.
        color.A.Should().Be(0xFF);
    }
```

- [ ] **Step 4: Run tests**

Run: `dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj --filter FullyQualifiedName~AccentColorsTests`
Expected: 12 tests pass.

- [ ] **Step 5: Commit**

```bash
git add shared/WindowsTrayCore/AccentColors.cs shared/WindowsTrayCore/Native/TrayNativeMethods.cs shared/WindowsTrayCore.Tests/AccentColorsTests.cs
git commit -m "WindowsTrayCore: AccentColors.Read with DWM + registry fallback

Reads the user's Windows accent color via DwmGetColorizationColor.
Falls back to HKCU\Software\Microsoft\Windows\DWM\AccentColor if the
DWM call fails, then to a hardcoded Windows blue (#0078D4) if both
fail.

DWM is preferred because it tracks the user's accent pick even when
'Show accent color on title bars' is disabled (in that case the
registry stores the chosen accent but title bars revert to default).
The contract is 'never throws, always returns a fully-opaque color'.
"
```

---

## Task 4: TrayTheme adds 12 new tokens alongside old (TDD)

Additive: old tokens stay temporarily, 12 new tokens land. Old consumers still build.

**Files:**
- Modify: `shared\WindowsTrayCore\TrayTheme.cs`
- Modify: `shared\WindowsTrayCore.Tests\TrayThemeTests.cs`

- [ ] **Step 1: Write the failing tests**

Open `shared\WindowsTrayCore.Tests\TrayThemeTests.cs` and append (inside the existing test class, or create one if it doesn't yet test new tokens):

```csharp
    [Fact]
    public void Surface_LightTheme_IsCanonicalLight()
    {
        var theme = new TrayTheme(isLight: true);
        theme.Surface.Should().Be(Color.FromArgb(0xF4, 0xF4, 0xF8));
    }

    [Fact]
    public void Surface_DarkTheme_IsCanonicalDark()
    {
        var theme = new TrayTheme(isLight: false);
        theme.Surface.Should().Be(Color.FromArgb(0x18, 0x18, 0x2D));
    }

    [Fact]
    public void SurfaceAlt_LightTheme_IsWhite()
    {
        new TrayTheme(isLight: true).SurfaceAlt.Should().Be(Color.FromArgb(0xFF, 0xFF, 0xFF));
    }

    [Fact]
    public void SurfaceStroke_LightTheme_IsNeutralBorder()
    {
        new TrayTheme(isLight: true).SurfaceStroke.Should().Be(Color.FromArgb(0xD8, 0xD8, 0xE0));
    }

    [Fact]
    public void Foreground_LightTheme_IsCanonicalText()
    {
        new TrayTheme(isLight: true).Foreground.Should().Be(Color.FromArgb(0x1A, 0x1A, 0x2E));
    }

    [Fact]
    public void ForegroundAlt_DarkTheme_HigherContrastThanOldTextMuted()
    {
        var theme = new TrayTheme(isLight: false);
        theme.ForegroundAlt.Should().Be(Color.FromArgb(0x9A, 0x95, 0xB0));
    }

    [Fact]
    public void ForegroundDim_LightTheme_IsCanonicalPlaceholder()
    {
        new TrayTheme(isLight: true).ForegroundDim.Should().Be(Color.FromArgb(0x90, 0x90, 0xA4));
    }

    [Fact]
    public void Warning_LightTheme_IsAmber()
    {
        new TrayTheme(isLight: true).Warning.Should().Be(Color.FromArgb(0xB4, 0x53, 0x09));
    }

    [Fact]
    public void AccentOn_DependsOnAccentLuminance()
    {
        // Construct with a known accent and verify AccentOn matches.
        var darkAccent = Color.FromArgb(0, 0x78, 0xD4); // Windows blue, luminance ~0.15
        var lightAccent = Color.FromArgb(0xFB, 0xBF, 0x24); // amber, luminance ~0.58

        var darkTheme = new TrayTheme(isLight: false, accent: darkAccent, isHighContrast: false);
        var lightTheme = new TrayTheme(isLight: false, accent: lightAccent, isHighContrast: false);

        darkTheme.AccentOn.Should().Be(Color.White);
        lightTheme.AccentOn.Should().Be(Color.Black);
    }

    [Fact]
    public void AccentSubtle_IsAccentBlendedOverSurface()
    {
        var accent = Color.FromArgb(0xFF, 0, 0);
        var theme = new TrayTheme(isLight: true, accent: accent, isHighContrast: false);

        // Surface is #F4F4F8 light; 24% red blend.
        theme.AccentSubtle.R.Should().Be(0xFF - 0); // hard to compute exactly; rough sanity
        theme.AccentSubtle.G.Should().BeLessThan(theme.Surface.G);
    }
```

- [ ] **Step 2: Run tests; verify they fail**

Expected: build errors on the new token names and the missing 3-arg constructor.

- [ ] **Step 3: Rewrite `TrayTheme.cs` with new tokens alongside old**

Replace `shared\WindowsTrayCore\TrayTheme.cs` with the structure below. The old token names (`Background`, `Surface`, `Text`, `TextMuted`, `Field`) remain temporarily as forwarding aliases over the new ones. They will be deleted in Task 14.

```csharp
using System.Drawing;
using Microsoft.Win32;

namespace WindowsTrayCore;

/// <summary>
/// System theme detection with Fluent-style semantic colour tokens. Subscribes
/// to live system theme and accent changes via a hidden message window.
/// </summary>
public sealed class TrayTheme : IDisposable
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static readonly TrayTheme _current = new();

    public static TrayTheme Current => _current;

    private bool _isLight;
    private Color _accent;
    private bool _isHighContrast;
    private bool _disposed;

    /// <summary>Production constructor; subscribes to system change messages.</summary>
    public TrayTheme()
    {
        _isLight = ReadRegistryIsLight();
        _accent = AccentColors.Read();
        _isHighContrast = SystemInformation.HighContrast;
        // Message-window subscription added in Task 5.
    }

    /// <summary>Testing constructor; no system subscription.</summary>
    internal TrayTheme(bool isLight, Color accent, bool isHighContrast)
    {
        _isLight = isLight;
        _accent = accent;
        _isHighContrast = isHighContrast;
    }

    /// <summary>Legacy testing constructor; defaults accent to Windows blue.</summary>
    public TrayTheme(bool isLight)
        : this(isLight, Color.FromArgb(0, 0x78, 0xD4), isHighContrast: false) { }

    public bool IsLight => _isLight;
    public bool IsHighContrast => _isHighContrast;

    public event EventHandler? Changed;

    // ── New Fluent tokens ──────────────────────────────────────────────────

    public Color Surface => _isLight
        ? Color.FromArgb(0xF4, 0xF4, 0xF8)
        : Color.FromArgb(0x18, 0x18, 0x2D);

    public Color SurfaceAlt => _isLight
        ? Color.FromArgb(0xFF, 0xFF, 0xFF)
        : Color.FromArgb(0x24, 0x24, 0x3A);

    public Color SurfaceStroke => _isLight
        ? Color.FromArgb(0xD8, 0xD8, 0xE0)
        : Color.FromArgb(0x3A, 0x3A, 0x52);

    public Color Foreground => _isLight
        ? Color.FromArgb(0x1A, 0x1A, 0x2E)
        : Color.FromArgb(0xE0, 0xDE, 0xE4);

    public Color ForegroundAlt => _isLight
        ? Color.FromArgb(0x5A, 0x5A, 0x70)
        : Color.FromArgb(0x9A, 0x95, 0xB0);

    public Color ForegroundDim => _isLight
        ? Color.FromArgb(0x90, 0x90, 0xA4)
        : Color.FromArgb(0x6E, 0x6A, 0x86);

    public Color Accent => _accent;
    public Color AccentOn => AccentColors.DeriveOn(_accent);
    public Color AccentSubtle => AccentColors.DeriveSubtle(_accent, Surface);

    public Color Warning => _isLight
        ? Color.FromArgb(0xB4, 0x53, 0x09)
        : Color.FromArgb(0xFB, 0xBF, 0x24);

    public Color Error => _isLight
        ? Color.FromArgb(0xC0, 0x29, 0x3A)
        : Color.FromArgb(0xE0, 0x55, 0x66);

    public Color Success => _isLight
        ? Color.FromArgb(0x2D, 0x8A, 0x4E)
        : Color.FromArgb(0x50, 0xC8, 0x78);

    // ── Legacy token aliases (deleted in Task 14) ──────────────────────────
    // Kept transiently so apps build during the migration commits.

    public Color Background => Surface;
    public Color Text => Foreground;
    public Color TextMuted => ForegroundAlt;
    public Color Field => SurfaceAlt;

    // ── Testing hooks ──────────────────────────────────────────────────────

    internal void SimulatePreferenceChanged(bool isLight)
    {
        if (isLight == _isLight) return;
        _isLight = isLight;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void SimulateAccentChanged(Color accent)
    {
        if (accent == _accent) return;
        _accent = accent;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void SimulateHighContrastChanged(bool on)
    {
        if (on == _isHighContrast) return;
        _isHighContrast = on;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Message-window cleanup added in Task 5.
    }

    // ── Internals ──────────────────────────────────────────────────────────

    private static bool ReadRegistryIsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey, writable: false);
            if (key?.GetValue("SystemUsesLightTheme") is int sys) return sys != 0;
            if (key?.GetValue("AppsUseLightTheme") is int app) return app != 0;
        }
        catch { }
        return false;
    }
}
```

Note the legacy block in the middle: `Background`, `Text`, `TextMuted`, `Field` (and the old `Surface` semantics) need careful handling. `Surface` is now the form background (old `Background`); the old `Surface` value moves to `SurfaceAlt`. The legacy `Background` property now forwards to `Surface` (the new role), which is the same hex as the old `Background`. The legacy `Surface` would forward to `SurfaceAlt` (preserving the old role). To keep both names valid during the transition:

Open `TrayTheme.cs` after the initial replacement and adjust the legacy aliases to map by ROLE not by NAME. The block above gets `Background => Surface` (correct), `Text => Foreground` (correct), `TextMuted => ForegroundAlt` (correct), `Field => SurfaceAlt` (correct). The old `Surface` getter needs to be removed since the new `Surface` already exists with the same hex. Verify by grepping that no consumer relies on the old `Surface` semantics that differ from the new `Surface`:

Grep: `TrayTheme\.(Current\.)?Surface` across all apps and shared library. If any call site treats it as the inner-panel color, document it; otherwise the rename is transparent.

- [ ] **Step 4: Run tests**

Run: `dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj`
Expected: All previous WindowsTrayCore tests pass + the 9 new token tests pass. The old TrayTheme tests against `Background` / `Text` / `TextMuted` / `Field` still pass via the aliases.

- [ ] **Step 5: Verify all four apps still build**

Run: `dotnet build apps\BatteryTray\BatteryTray.sln`
Run: `dotnet build apps\NetProfileSwitcher\NetProfileSwitcher.csproj`
Run: `dotnet build apps\SoundTracker\SoundTracker.sln`
Run: `dotnet build apps\ProgramHider\app\ProgramHider\ProgramHider.csproj`
Expected: all clean. Legacy aliases keep old call sites compiling.

- [ ] **Step 6: Commit**

```bash
git add shared/WindowsTrayCore/TrayTheme.cs shared/WindowsTrayCore.Tests/TrayThemeTests.cs
git commit -m "WindowsTrayCore: TrayTheme adds 12 Fluent-style semantic tokens

Surface / SurfaceAlt / SurfaceStroke / Foreground / ForegroundAlt /
ForegroundDim / Accent / AccentOn / AccentSubtle / Warning / Error /
Success. The Accent token now reflects the user's system accent
(via AccentColors.Read); AccentOn and AccentSubtle derive from it.

The legacy property names (Background, Text, TextMuted, Field) remain
as forwarding aliases temporarily so consumer call sites continue to
build during the migration. They are deleted in the final commit of
this phase.

Live system reaction (message window) and the SetOverride API come
in subsequent commits.
"
```

---

## Task 5: TrayTheme message window for live change detection

Replaces the previous `SystemEvents.UserPreferenceChanged` subscription with a hidden message window that handles both `WM_SETTINGCHANGE` and `WM_DWMCOLORIZATIONCOLORCHANGED`.

**Files:**
- Modify: `shared\WindowsTrayCore\TrayTheme.cs`
- Modify: `shared\WindowsTrayCore.Tests\TrayThemeTests.cs`

- [ ] **Step 1: Write the failing tests**

Append to `TrayThemeTests.cs`:

```csharp
    [Fact]
    public void SimulateAccentChanged_FiresChanged_AndUpdatesAccent()
    {
        var theme = new TrayTheme(isLight: true, accent: Color.Red, isHighContrast: false);
        var fired = 0;
        theme.Changed += (_, _) => fired++;

        theme.SimulateAccentChanged(Color.Blue);

        fired.Should().Be(1);
        theme.Accent.Should().Be(Color.Blue);
    }

    [Fact]
    public void SimulateAccentChanged_SameValue_DoesNotFire()
    {
        var theme = new TrayTheme(isLight: true, accent: Color.Red, isHighContrast: false);
        var fired = 0;
        theme.Changed += (_, _) => fired++;

        theme.SimulateAccentChanged(Color.Red);

        fired.Should().Be(0);
    }

    [Fact]
    public void SimulateHighContrastChanged_FiresChanged()
    {
        var theme = new TrayTheme(isLight: true, accent: Color.Red, isHighContrast: false);
        var fired = 0;
        theme.Changed += (_, _) => fired++;

        theme.SimulateHighContrastChanged(true);

        fired.Should().Be(1);
        theme.IsHighContrast.Should().BeTrue();
    }

    [Fact]
    public void AccentSubtle_RecomputesAfterAccentChange()
    {
        var theme = new TrayTheme(isLight: true, accent: Color.Red, isHighContrast: false);
        var firstSubtle = theme.AccentSubtle;

        theme.SimulateAccentChanged(Color.Blue);

        theme.AccentSubtle.Should().NotBe(firstSubtle);
    }
```

- [ ] **Step 2: Run tests; verify simulation hooks fail to compile**

Expected: `CS0117: 'TrayTheme' does not contain a definition for 'SimulateAccentChanged'` (and `SimulateHighContrastChanged`).

- [ ] **Step 3: Add the message window + simulation hooks**

Edit `shared\WindowsTrayCore\TrayTheme.cs`. Inside the class, replace the testing-hooks region and the Dispose method with:

```csharp
    // ── Testing hooks ──────────────────────────────────────────────────────

    internal void SimulatePreferenceChanged(bool isLight)
    {
        if (isLight == _isLight) return;
        _isLight = isLight;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void SimulateAccentChanged(Color accent)
    {
        if (accent == _accent) return;
        _accent = accent;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void SimulateHighContrastChanged(bool on)
    {
        if (on == _isHighContrast) return;
        _isHighContrast = on;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _messageWindow?.DestroyHandle();
    }
```

And inside the class, near the top (after the field declarations), add:

```csharp
    private readonly MessageWindow? _messageWindow;
```

In the production constructor:

```csharp
    public TrayTheme()
    {
        _isLight = ReadRegistryIsLight();
        _accent = AccentColors.Read();
        _isHighContrast = SystemInformation.HighContrast;
        _messageWindow = new MessageWindow(this);
        _messageWindow.CreateHandle(new CreateParams());
    }
```

Append the inner class to `TrayTheme.cs`:

```csharp
    private const int WM_SETTINGCHANGE = 0x001A;
    private const int WM_DWMCOLORIZATIONCOLORCHANGED = 0x0320;

    private sealed class MessageWindow : NativeWindow
    {
        private readonly TrayTheme _owner;
        public MessageWindow(TrayTheme owner) => _owner = owner;

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_SETTINGCHANGE:
                    _owner.OnSettingChange(m.LParam);
                    break;
                case WM_DWMCOLORIZATIONCOLORCHANGED:
                    _owner.OnAccentChanged();
                    break;
            }
            base.WndProc(ref m);
        }
    }

    private void OnSettingChange(IntPtr lParam)
    {
        // The broadcast that signals AppsUseLightTheme flipped has lParam
        // pointing to the literal "ImmersiveColorSet". Other WM_SETTINGCHANGE
        // broadcasts can also carry "WindowsThemeElement" or "Policy"; we
        // refresh on any of these and let the no-op short-circuit handle
        // duplicates.
        var hcChanged = SystemInformation.HighContrast != _isHighContrast;
        var newLight = ReadRegistryIsLight();

        if (hcChanged)
        {
            _isHighContrast = SystemInformation.HighContrast;
            Changed?.Invoke(this, EventArgs.Empty);
        }
        else if (newLight != _isLight)
        {
            _isLight = newLight;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnAccentChanged()
    {
        var newAccent = AccentColors.Read();
        if (newAccent == _accent) return;
        _accent = newAccent;
        Changed?.Invoke(this, EventArgs.Empty);
    }
```

Add `using System.Windows.Forms;` if not already present (`NativeWindow` and `SystemInformation` live there).

- [ ] **Step 4: Run tests**

Expected: All TrayTheme tests pass, including the 4 new ones.

- [ ] **Step 5: Commit**

```bash
git add shared/WindowsTrayCore/TrayTheme.cs shared/WindowsTrayCore.Tests/TrayThemeTests.cs
git commit -m "WindowsTrayCore: TrayTheme reacts live to system theme + accent changes

Replaces the SystemEvents.UserPreferenceChanged subscription with a
hidden message window that handles WM_SETTINGCHANGE (light/dark and
high-contrast flips) and WM_DWMCOLORIZATIONCOLORCHANGED (accent
changes). The Changed event fires on the application message-pump
thread, same contract as before but for more triggers.

Simulation hooks added for accent and high-contrast tests; the
existing SimulatePreferenceChanged hook is unchanged.
"
```

---

## Task 6: TrayTheme.SetOverride + ThemePreference plumbing

Adds the `Auto / Light / Dark` override API. Used by BatteryTray's existing combo (wired in Task 10).

**Files:**
- Modify: `shared\WindowsTrayCore\TrayTheme.cs`
- Modify: `shared\WindowsTrayCore.Tests\TrayThemeTests.cs`

- [ ] **Step 1: Write the failing tests**

Append to `TrayThemeTests.cs`:

```csharp
    [Fact]
    public void Preference_Default_IsAuto()
    {
        var theme = new TrayTheme(isLight: true);
        theme.Preference.Should().Be(ThemePreference.Auto);
    }

    [Fact]
    public void SetOverride_Light_ForcesIsLightTrue_RegardlessOfSystem()
    {
        var theme = new TrayTheme(isLight: false, accent: Color.Red, isHighContrast: false);
        theme.IsLight.Should().BeFalse();

        theme.SetOverride(ThemePreference.Light);

        theme.IsLight.Should().BeTrue();
        theme.Preference.Should().Be(ThemePreference.Light);
    }

    [Fact]
    public void SetOverride_Dark_ForcesIsLightFalse()
    {
        var theme = new TrayTheme(isLight: true, accent: Color.Red, isHighContrast: false);
        theme.SetOverride(ThemePreference.Dark);
        theme.IsLight.Should().BeFalse();
    }

    [Fact]
    public void SetOverride_Auto_RestoresSystemFollowing()
    {
        // Construct as dark; force Light; flip back to Auto.
        var theme = new TrayTheme(isLight: false, accent: Color.Red, isHighContrast: false);
        theme.SetOverride(ThemePreference.Light);
        theme.IsLight.Should().BeTrue();

        theme.SetOverride(ThemePreference.Auto);

        // Without system-event simulation, the cached _isLight stays
        // whatever the override last set; Auto means "follow system from
        // now on". The next SimulatePreferenceChanged will take effect.
        theme.Preference.Should().Be(ThemePreference.Auto);
        theme.SimulatePreferenceChanged(false);
        theme.IsLight.Should().BeFalse();
    }

    [Fact]
    public void SetOverride_FiresChanged()
    {
        var theme = new TrayTheme(isLight: true, accent: Color.Red, isHighContrast: false);
        var fired = 0;
        theme.Changed += (_, _) => fired++;

        theme.SetOverride(ThemePreference.Dark);

        fired.Should().Be(1);
    }
```

- [ ] **Step 2: Run tests; verify failure**

Expected: `Preference` and `SetOverride` do not exist on `TrayTheme`.

- [ ] **Step 3: Add the override mechanism**

Edit `TrayTheme.cs`. Add a private field:

```csharp
    private ThemePreference _preference = ThemePreference.Auto;
```

Add the public property + method:

```csharp
    public ThemePreference Preference => _preference;

    /// <summary>
    /// Overrides the system theme detection. Auto restores system-follow;
    /// Light and Dark force the corresponding palette regardless of system
    /// state. Fires <see cref="Changed"/> if the resulting palette changes
    /// or the preference itself changed.
    /// </summary>
    public void SetOverride(ThemePreference preference)
    {
        var changed = preference != _preference;
        _preference = preference;

        bool newLight = preference switch
        {
            ThemePreference.Light => true,
            ThemePreference.Dark => false,
            _ => ReadRegistryIsLight(),
        };

        if (newLight != _isLight)
        {
            _isLight = newLight;
            changed = true;
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }
```

Adjust `OnSettingChange` to respect the override; when the override is `Light` or `Dark`, ignore the system light/dark flip:

```csharp
    private void OnSettingChange(IntPtr lParam)
    {
        var hcChanged = SystemInformation.HighContrast != _isHighContrast;

        if (hcChanged)
        {
            _isHighContrast = SystemInformation.HighContrast;
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (_preference != ThemePreference.Auto) return;

        var newLight = ReadRegistryIsLight();
        if (newLight != _isLight)
        {
            _isLight = newLight;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
```

- [ ] **Step 4: Run tests**

Expected: 5 new override tests pass; all prior tests still pass.

- [ ] **Step 5: Commit**

```bash
git add shared/WindowsTrayCore/TrayTheme.cs shared/WindowsTrayCore.Tests/TrayThemeTests.cs
git commit -m "WindowsTrayCore: TrayTheme.SetOverride for per-app Light/Dark forcing

Adds a Preference property + SetOverride method that lets a consumer
force Light or Dark regardless of system theme. Auto restores
system-follow. The message-window handler now ignores system theme
flips when an override is active; high-contrast and accent changes
still propagate.

Consumer wiring (the existing unwired BatteryTray theme combo) lands
in the BatteryTray migration commit.
"
```

---

## Task 7: ThemeApplier.ApplyTitleBar via DwmSetWindowAttribute

Adds the dark title bar pass. Standalone method, not yet integrated into `Apply(Form)`.

**Files:**
- Modify: `shared\WindowsTrayCore\Native\TrayNativeMethods.cs`
- Modify: `shared\WindowsTrayCore\ThemeApplier.cs`

- [ ] **Step 1: Add the P/Invoke**

In `shared\WindowsTrayCore\Native\TrayNativeMethods.cs`, add (near the existing DWM declarations):

```csharp
[DllImport("dwmapi.dll", PreserveSig = true)]
internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

internal const int DWMWA_USE_IMMERSIVE_DARK_MODE_PRE_20H1 = 19;
internal const int DWMWA_USE_IMMERSIVE_DARK_MODE          = 20;
```

- [ ] **Step 2: Add ApplyTitleBar to ThemeApplier**

In `shared\WindowsTrayCore\ThemeApplier.cs`, add:

```csharp
    /// <summary>
    /// Applies (or removes) the Windows 10 dark title-bar tint for a form
    /// using DwmSetWindowAttribute. Tries attribute 20 (DWMWA_USE_IMMERSIVE_DARK_MODE,
    /// official since Win10 1903) first, falls back to attribute 19 (the
    /// undocumented predecessor for 1809-1902). No-op on older builds and on
    /// any HRESULT failure; the title bar stays default-themed.
    /// </summary>
    public static void ApplyTitleBar(Form form, bool dark)
    {
        if (form is null || !form.IsHandleCreated) return;

        int useDark = dark ? 1 : 0;
        var hr = Native.TrayNativeMethods.DwmSetWindowAttribute(
            form.Handle,
            Native.TrayNativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE,
            ref useDark,
            sizeof(int));

        if (hr != 0)
        {
            // Fallback path; ignored result.
            Native.TrayNativeMethods.DwmSetWindowAttribute(
                form.Handle,
                Native.TrayNativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE_PRE_20H1,
                ref useDark,
                sizeof(int));
        }
    }
```

- [ ] **Step 3: Verify build**

Run: `dotnet build shared\WindowsTrayCore\WindowsTrayCore.csproj`
Expected: clean.

- [ ] **Step 4: Commit**

```bash
git add shared/WindowsTrayCore/ThemeApplier.cs shared/WindowsTrayCore/Native/TrayNativeMethods.cs
git commit -m "WindowsTrayCore: ThemeApplier.ApplyTitleBar via DwmSetWindowAttribute

Sets the Windows 10 dark title-bar tint via the DWM attribute API.
Tries DWMWA_USE_IMMERSIVE_DARK_MODE (attribute 20, official since
Win10 1903) first, falls back to attribute 19 (the undocumented
predecessor available on Win10 1809-1902). No-op on older builds or
on any HRESULT failure; the title bar stays default-themed.

Standalone for now; integration into ThemeApplier.Apply(Form) lands
in the next commit so the title-bar pass and the control-tree walk
can be reviewed independently.
"
```

---

## Task 8: ThemeApplier.Apply(Form) calls ApplyTitleBar

Wires title-bar tinting into the existing `Apply(Form)` entry point so every consumer benefits automatically.

**Files:**
- Modify: `shared\WindowsTrayCore\ThemeApplier.cs`
- Modify: `shared\WindowsTrayCore.Tests\ThemeApplierTests.cs`

- [ ] **Step 1: Write the failing test**

Append to `ThemeApplierTests.cs`:

```csharp
    [WindowsFact]
    public void Apply_OnForm_DarkTheme_DoesNotThrow()
    {
        using var form = new Form();
        var dark = new TrayTheme(isLight: false, accent: System.Drawing.Color.Blue, isHighContrast: false);

        var act = () => ThemeApplier.Apply(form, dark);
        act.Should().NotThrow();
    }

    [WindowsFact]
    public void Apply_OnForm_LightTheme_DoesNotThrow()
    {
        using var form = new Form();
        var light = new TrayTheme(isLight: true, accent: System.Drawing.Color.Blue, isHighContrast: false);

        var act = () => ThemeApplier.Apply(form, light);
        act.Should().NotThrow();
    }
```

(The title-bar attempt's HRESULT cannot be reliably introspected in a unit test; the contract is "the call happens and never throws", which the no-throw assertion exercises.)

- [ ] **Step 2: Run tests; verify they pass on the current Apply but the title-bar call has not been wired yet**

Run: `dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj --filter FullyQualifiedName~ThemeApplierTests`
Expected: tests pass (existing Apply already handles forms cleanly).

- [ ] **Step 3: Integrate ApplyTitleBar into Apply(Form)**

In `ThemeApplier.cs`, locate the `Apply(Form form, TrayTheme? theme = null)` method (or `Apply(Control container, ...)` if there's only one). Add the title-bar call at the top, before the control-tree walk:

```csharp
    public static void Apply(Form form, TrayTheme? theme = null)
    {
        var t = theme ?? TrayTheme.Current;
        ApplyTitleBar(form, !t.IsLight);
        Apply((Control)form, t);
    }
```

If only `Apply(Control)` exists today, add a new `Apply(Form)` overload that branches as above, and have the existing `Apply(Control)` keep its old behavior.

- [ ] **Step 4: Run tests**

Expected: All ThemeApplier tests pass.

- [ ] **Step 5: Commit**

```bash
git add shared/WindowsTrayCore/ThemeApplier.cs shared/WindowsTrayCore.Tests/ThemeApplierTests.cs
git commit -m "WindowsTrayCore: ThemeApplier.Apply(Form) wires title-bar tint automatically

Every Apply(Form form, theme) call now also calls ApplyTitleBar with
the appropriate dark/light value before walking the control tree.
Consumers get the dark title bar for free; no per-form opt-in needed.

The Apply(Control) overload is unchanged; only the Form-typed entry
adds the title-bar pass.
"
```

---

## Task 9: AboutDialog + FirstRunBalloon migrate to new tokens

Shared-library types that consume `TrayTheme.Current`. Migrate first because they're internal to WindowsTrayCore and other apps depend on them visually.

**Files:**
- Modify: `shared\WindowsTrayCore\AboutDialog.cs`
- Modify: `shared\WindowsTrayCore\FirstRunBalloon.cs`

- [ ] **Step 1: Grep current references**

Run: `grep -n "TrayTheme\.Current\.\(Background\|Surface\|Text\|TextMuted\|Field\)" shared/WindowsTrayCore/AboutDialog.cs shared/WindowsTrayCore/FirstRunBalloon.cs`
Note each line. Apply the migration mapping per the spec:

| Old | New |
|---|---|
| `Background` | `Surface` |
| `Surface` (inner sunken) | `SurfaceAlt` |
| `Text` | `Foreground` |
| `TextMuted` | `ForegroundAlt` |
| `Field` | `SurfaceAlt` |

- [ ] **Step 2: Apply the migration**

For each occurrence found in step 1, rename the token reference. No semantic change; just renames.

- [ ] **Step 3: Build + test**

Run: `dotnet build shared\WindowsTrayCore\WindowsTrayCore.csproj`
Run: `dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj`
Expected: clean build, all tests pass.

- [ ] **Step 4: Commit**

```bash
git add shared/WindowsTrayCore/AboutDialog.cs shared/WindowsTrayCore/FirstRunBalloon.cs
git commit -m "WindowsTrayCore: AboutDialog + FirstRunBalloon use new Fluent tokens

Renames token references per the migration mapping (Background ->
Surface, Text -> Foreground, TextMuted -> ForegroundAlt, etc). No
behavioural change; the legacy aliases on TrayTheme forward to the
same hex values, so this is a name-only swap that lets these two
shared-library types stop depending on the legacy alias surface.
"
```

---

## Task 10: BatteryTray migration + wire ThemeCombo (subagent)

Two distinct concerns: token migration (mechanical) and wiring the existing unwired `_themeCombo` in `SettingsForm.cs` to `TrayTheme.SetOverride`.

**Files:**
- Modify: `apps\BatteryTray\BatteryTray\BatteryTrayContext.cs` (token migration)
- Modify: `apps\BatteryTray\BatteryTray\SettingsForm.cs` (token migration + theme combo wiring)

- [ ] **Step 1: Grep + apply token migration in BatteryTrayContext.cs**

Same mapping as Task 9. Mechanical.

- [ ] **Step 2: Grep + apply token migration in SettingsForm.cs**

Same.

- [ ] **Step 3: Wire the theme combo to SetOverride**

In `SettingsForm.cs`, locate the `_themeCombo` declaration (currently set up with items "Auto (follow Windows)", "Light", "Dark") and the `LoadValues` / `SaveAndClose` methods. Verify the combo is currently unwired (no `SelectedIndexChanged` handler that does anything functional). Then:

In the constructor or `BuildLayout` after the combo is created:

```csharp
    _themeCombo.SelectedIndexChanged += (_, _) =>
    {
        var preference = (ThemePreference)_themeCombo.SelectedIndex;
        TrayTheme.Current.SetOverride(preference);
    };
```

In `LoadValues`:

```csharp
    _themeCombo.SelectedIndex = (int)_settings.Theme;
```

(where `_settings.Theme` is the existing `IconTheme` enum used in `AppSettings.cs`; verify the enum order matches `ThemePreference`. If `IconTheme` is named differently, alias-cast as needed.)

In `SaveAndClose`:

```csharp
    _settings.Theme = (IconTheme)_themeCombo.SelectedIndex;
```

Verify the existing settings serialization still works after this change.

- [ ] **Step 4: Build + test**

Run: `dotnet build apps\BatteryTray\BatteryTray.sln`
Run: `dotnet test apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj`
Expected: clean build, all tests pass.

- [ ] **Step 5: Commit**

```bash
git add apps/BatteryTray/BatteryTray/BatteryTrayContext.cs apps/BatteryTray/BatteryTray/SettingsForm.cs
git commit -m "BatteryTray: migrate to Fluent tokens + wire theme combo to SetOverride

Token references migrate to the new Fluent palette (Surface,
Foreground, ForegroundAlt, etc); no behavioural change for those.

SettingsForm._themeCombo gains a SelectedIndexChanged handler that
calls TrayTheme.Current.SetOverride with the picked preference.
LoadValues restores the persisted value; SaveAndClose persists it
back into AppSettings.Theme. The combo had this UI for several
phases but was unwired until now.
"
```

---

## Task 11: NetProfileSwitcher migration (subagent)

Mechanical token migration.

**Files:**
- Modify: `apps\NetProfileSwitcher\UI\MainForm.cs`

- [ ] **Step 1: Grep + apply migration per the standard mapping**

Same approach as Task 9; grep `TrayTheme\.Current\.(Background|Surface|Text|TextMuted|Field)` in `MainForm.cs` and rename per the spec mapping.

- [ ] **Step 2: Build + test**

Run: `dotnet build apps\NetProfileSwitcher\NetProfileSwitcher.csproj`
Run: `dotnet test apps\NetProfileSwitcher.Tests\NetProfileSwitcher.Tests.csproj`
Expected: clean.

- [ ] **Step 3: Commit**

```bash
git add apps/NetProfileSwitcher/UI/MainForm.cs
git commit -m "NetProfileSwitcher: migrate to Fluent tokens

Token references migrate to the new Fluent palette per the standard
mapping. Mechanical rename; no behavioural change."
```

---

## Task 12: ProgramHider migration (subagent)

Mechanical migration; ProgramHider has the largest settings form and the most token references.

**Files:**
- Modify: `apps\ProgramHider\app\ProgramHider\ProgramHiderContext.cs`
- Modify: `apps\ProgramHider\app\ProgramHider\SettingsForm.cs`

- [ ] **Step 1: Grep + apply migration per the standard mapping** in both files.

- [ ] **Step 2: Build + test**

Run: `dotnet build apps\ProgramHider\app\ProgramHider\ProgramHider.csproj`
Run: `dotnet test apps\ProgramHider\tests\ProgramHider.Tests\ProgramHider.Tests.csproj`
Expected: clean.

- [ ] **Step 3: Commit**

```bash
git add apps/ProgramHider/app/ProgramHider/ProgramHiderContext.cs apps/ProgramHider/app/ProgramHider/SettingsForm.cs
git commit -m "ProgramHider: migrate to Fluent tokens

Token references migrate to the new Fluent palette per the standard
mapping. Mechanical rename across the tray context and the larger
settings form; no behavioural change."
```

---

## Task 13: SoundTracker migration (subagent)

Mechanical migration. `RecentActivityForm`'s list rows currently use the inner-surface color; pay attention to `Surface` vs `SurfaceAlt` semantics there.

**Files:**
- Modify: `apps\SoundTracker\SoundTracker.App\TrayApplicationContext.cs`
- Modify: `apps\SoundTracker\SoundTracker.App\SettingsForm.cs`
- Modify: `apps\SoundTracker\SoundTracker.App\RecentActivityForm.cs`

- [ ] **Step 1: Grep + apply migration per the standard mapping** in all three files.

- [ ] **Step 2: Build + test**

Run: `dotnet build apps\SoundTracker\SoundTracker.sln`
Run: `dotnet test apps\SoundTracker\SoundTracker.Tests\SoundTracker.Tests.csproj`
Optional: `dotnet run --project apps\SoundTracker\SoundTracker.SmokeTests\SoundTracker.SmokeTests.csproj`
Expected: clean.

- [ ] **Step 3: Commit**

```bash
git add apps/SoundTracker/SoundTracker.App/TrayApplicationContext.cs apps/SoundTracker/SoundTracker.App/SettingsForm.cs apps/SoundTracker/SoundTracker.App/RecentActivityForm.cs
git commit -m "SoundTracker: migrate to Fluent tokens

Token references migrate to the new Fluent palette per the standard
mapping. RecentActivityForm's list-row surface remaps from Surface to
SurfaceAlt (the old inner-surface role). Smoke tests still green.
"
```

---

## Task 14: Delete legacy TrayTheme aliases + WORKLOG (breaking commit)

The final, breaking commit. Removes the legacy property aliases (`Background`, `Text`, `TextMuted`, `Field`) from `TrayTheme`. All consumers must have migrated by this point.

**Files:**
- Modify: `shared\WindowsTrayCore\TrayTheme.cs`
- Modify: `WORKLOG.md`

- [ ] **Step 1: Verify all consumers are migrated**

Run: `grep -rn "TrayTheme\.Current\.\(Background\|Text\|TextMuted\|Field\)" shared/ apps/`
Expected: zero matches. If any remain, do NOT proceed; finish migrating those first.

Run also: `grep -rn "\.Background\|\.Text\|\.TextMuted\|\.Field" shared/WindowsTrayCore.Tests/TrayThemeTests.cs`
Expected: any matches here are stale TrayTheme tests that should be rewritten or deleted.

- [ ] **Step 2: Delete the legacy alias block from TrayTheme.cs**

Remove the section in `TrayTheme.cs` labeled `// ── Legacy token aliases (deleted in Task 14) ──`:

```csharp
    // DELETE THIS WHOLE BLOCK:
    public Color Background => Surface;
    public Color Text => Foreground;
    public Color TextMuted => ForegroundAlt;
    public Color Field => SurfaceAlt;
```

- [ ] **Step 3: Remove or rewrite any stale tests in TrayThemeTests.cs**

If `TrayThemeTests.cs` still has tests against `Background`, `Text`, `TextMuted`, or `Field`, delete those tests. Their replacements (testing `Surface`, `Foreground`, etc.) were added in Task 4.

- [ ] **Step 4: Full sweep**

Run all of:

```
dotnet build shared\WindowsTrayCore\WindowsTrayCore.csproj
dotnet build apps\BatteryTray\BatteryTray.sln
dotnet build apps\NetProfileSwitcher\NetProfileSwitcher.csproj
dotnet build apps\SoundTracker\SoundTracker.sln
dotnet build apps\ProgramHider\app\ProgramHider\ProgramHider.csproj
dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj
dotnet test shared\WindowsAppCore.Tests\WindowsAppCore.Tests.csproj
dotnet test apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj
dotnet test apps\NetProfileSwitcher.Tests\NetProfileSwitcher.Tests.csproj
dotnet test apps\SoundTracker\SoundTracker.Tests\SoundTracker.Tests.csproj
dotnet test apps\ProgramHider\tests\ProgramHider.Tests\ProgramHider.Tests.csproj
```

Expected: all clean, all green.

- [ ] **Step 5: Append WORKLOG entry**

In `WORKLOG.md`, find the most recent dated entry (Phase 28.1 polish) and insert a new entry below it, above the `## Phase Checklist` heading:

```markdown
## 2026-05-15

**Did:** Phase 29 implementation: Fluent-style theme tokens, system accent following, dark title bar.
- `WindowsTrayCore.TrayTheme` rewritten with 12 Fluent semantic tokens (Surface, SurfaceAlt, SurfaceStroke, Foreground, ForegroundAlt, ForegroundDim, Accent, AccentOn, AccentSubtle, Warning, Error, Success). Old properties (Background, Text, TextMuted, Field) deleted; one transitional commit alias period bridged the four-app migration before deletion.
- `WindowsTrayCore.AccentColors`: reads system accent via `DwmGetColorizationColor` with HKCU\Software\Microsoft\Windows\DWM\AccentColor fallback. `DeriveOn` picks black or white per WCAG luminance; `DeriveSubtle` alpha-blends accent at 24% over the surface.
- `WindowsTrayCore.ThemeApplier.ApplyTitleBar`: `DwmSetWindowAttribute` with attribute 20 (1903+) and attribute 19 fallback (1809-1902). Now called automatically by `Apply(Form, theme)` before the control-tree walk.
- `TrayTheme` reacts live: hidden message window handles WM_SETTINGCHANGE (light/dark + high contrast) and WM_DWMCOLORIZATIONCOLORCHANGED (accent). Replaces the previous `SystemEvents.UserPreferenceChanged` subscription.
- `TrayTheme.SetOverride(ThemePreference)` lets a consumer force Auto / Light / Dark. BatteryTray's existing `_themeCombo` is wired through it for the first time (the UI existed since Phase 22 but was never functional).

**Tests:** TrayTheme test suite rewritten for new tokens + override + simulation hooks. AccentColors tests cover luminance, DeriveOn, DeriveSubtle, and the no-throw Read contract. All shared and per-app test suites green.

**Committed:** see git log between Phase 28.1 and this entry.

**Next:** Phase 30 (ClipTray, or a follow-up theming phase if user feedback surfaces visible gaps).
```

- [ ] **Step 6: Final commit**

```bash
git add shared/WindowsTrayCore/TrayTheme.cs shared/WindowsTrayCore.Tests/TrayThemeTests.cs WORKLOG.md
git commit -m "WindowsTrayCore: remove legacy TrayTheme alias properties

Final commit of Phase 29. The transitional aliases Background, Text,
TextMuted, and Field (which forwarded to the new Fluent tokens during
the four-app migration) are now removed. Every consumer in shared lib
and in the four apps was migrated in Tasks 9-13.

WORKLOG entry summarises the full Phase 29 series.
"
```

---

## Self-Review Notes

**Spec coverage:**
- 12-token catalog: Tasks 4 (introduce additively) + 14 (legacy removal). Token semantics asserted in Task 4 tests.
- Accent source (DWM + registry fallback): Task 3.
- Live change detection (message window + both messages): Task 5.
- SetOverride API: Task 6.
- Dark title bar (auto-applied): Tasks 7 + 8.
- Per-app override wiring (BatteryTray): Task 10.
- All four apps + shared lib types migrated: Tasks 9-13.

**Placeholder scan:** no "TBD", "TODO", or vague steps. Every code block is concrete. App-side grep instructions appear because line numbers will have drifted and the spec follows the "grep before trusting plan-cited symbols" discipline established in Phase 27.

**Type consistency:**
- `TrayTheme` constructor signatures (`()`, `(bool)`, `(bool, Color, bool)`) used consistently across Tasks 4-6.
- `ThemePreference.Auto / Light / Dark` ordinals (0, 1, 2) match between Task 1 enum, Task 6 SetOverride logic, and Task 10 combo wiring (combo index 0 = Auto, etc.).
- Token names (`Surface`, `SurfaceAlt`, `Foreground`, etc.) identical in spec, Task 4 implementation, and migration tasks.

**Hybrid execution sketch (for the implementer):**
- Tasks 1-8 inline, sequential. They build the shared infrastructure additively.
- Tasks 10-13 can be dispatched as parallel subagents (same pattern as Phase 28's app migrations) after Task 9 lands. Each subagent works in one app's files and commits independently.
- Task 14 is the inline breaking commit; full sweep + WORKLOG.

---

## Execution

Pick a path forward when ready.
