# WindowsTrayCore: Fluent-style Theme Tokens, System Accent Following, Dark Title Bar

**Status:** Design approved 2026-05-15. Implementation pending.
**Scope:** `shared\WindowsTrayCore\` (`TrayTheme`, `ThemeApplier`, new helpers) plus every call site of the existing `TrayTheme` properties across the four consumer apps and shared library types (`AboutDialog`, settings forms).

## Summary

Restructures `TrayTheme` from its current 8 hand-picked color properties into a 12-token Fluent-style semantic palette, replaces the hardcoded purple accent with the user's actual system accent color (read via `DwmGetColorizationColor` with a registry fallback), wires up live reaction to runtime theme and accent changes via a hidden message window listening for `WM_SETTINGCHANGE` and `WM_DWMCOLORIZATIONCOLORCHANGED`, and teaches `ThemeApplier` to apply Windows 10 dark title bars to every form it walks.

End state: hovering between Settings > Personalization > Colors and any open app form produces a visible, instant update across both the form chrome (title bar) and content (palette).

## Context

`TrayTheme.cs` today is a singleton that reads `SystemUsesLightTheme` / `AppsUseLightTheme` from the registry and exposes 8 properties (`Background`, `Surface`, `Text`, `TextMuted`, `Accent`, `Success`, `Error`, `Field`). Reaction to theme changes happens through `SystemEvents.UserPreferenceChanged`, which catches the light/dark flip but not accent changes. The `Accent` token is a hardcoded purple (`#5A4FD4` light, `#7C6FF7` dark) that ignores whatever the user has picked. Title bars on every form stay light-themed in dark mode because nothing calls `DwmSetWindowAttribute`. `ThemeApplier` recursively walks a form's `Controls` tree and sets `BackColor` / `ForeColor` per a small case table.

Four apps consume `TrayTheme.Current` plus `ThemeApplier.Apply(form)`. Settings forms, the shared `AboutDialog`, and `FirstRunBalloon` all reach into the same properties. The token names are inconsistent enough that some call sites use `SystemColors.GrayText` (BatteryTray drift hint) rather than `TrayTheme.Current.TextMuted` because the latter does not visually look right in some contexts. That divergence is the symptom Phase 29 fixes.

## Locked Decisions

| Axis | Decision |
|---|---|
| Token vocabulary | 12 Fluent-style semantic tokens (see Token Catalog below) |
| Accent source | `DwmGetColorizationColor` primary; `HKCU\Software\Microsoft\Windows\DWM\AccentColor` fallback; fixed defaults if both unavailable |
| Live change detection | Hidden message window inside `TrayTheme` handling `WM_SETTINGCHANGE` and `WM_DWMCOLORIZATIONCOLORCHANGED`. Replaces the existing `SystemEvents.UserPreferenceChanged` subscription. |
| Title bar dark mode | Applied automatically by `ThemeApplier` walking the control tree, calling `DwmSetWindowAttribute` with attribute 20 (DWMWA_USE_IMMERSIVE_DARK_MODE). Falls back to attribute 19 on Windows 10 1809-1902. No-op on older builds. |
| Migration | Old `TrayTheme` properties (`Background`, `Surface`, `Text`, `TextMuted`, `Accent`, `Success`, `Error`, `Field`) deleted outright. Every call site migrates to new tokens in the same phase. |
| `AccentOn` derivation | WCAG relative-luminance check on `Accent`; pick black (`#000000`) if luminance > 0.55, else white (`#FFFFFF`). |
| `AccentSubtle` derivation | Precomputed alpha blend: Accent at 24% over Surface. Recomputed whenever Accent or Surface changes. |
| High-contrast mode | Detect (expose `IsHighContrast`) but do not actively repaint. Deferred for a future phase. |
| Per-app theme override | `TrayTheme.SetOverride(ThemePreference?)` accepts `Auto` / `Light` / `Dark`. BatteryTray's existing combo wires into it. Other apps gain no override UI in Phase 29. |
| Platform | Windows 10 build 1809+ for title bar dark mode (older builds silently skip the call). All other features work on every supported Windows 10 build. |

## Architecture

### New types

```csharp
// shared/WindowsTrayCore/ThemePreference.cs
namespace WindowsTrayCore;

public enum ThemePreference
{
    /// <summary>Follow the system AppsUseLightTheme flag (default).</summary>
    Auto,
    Light,
    Dark,
}
```

```csharp
// shared/WindowsTrayCore/AccentColors.cs (internal)
namespace WindowsTrayCore;

internal static class AccentColors
{
    /// <summary>
    /// Reads the current Windows accent color via DwmGetColorizationColor.
    /// Falls back to HKCU\Software\Microsoft\Windows\DWM\AccentColor, then
    /// to a hardcoded blue (#0078D4) if both fail.
    /// </summary>
    public static Color Read();

    /// <summary>
    /// Derives the foreground color (white or black) that yields adequate
    /// contrast against the supplied accent. Threshold: WCAG relative
    /// luminance > 0.55 => black, else white.
    /// </summary>
    public static Color DeriveOn(Color accent);

    /// <summary>Alpha-blends a 24%-opacity accent over the surface.</summary>
    public static Color DeriveSubtle(Color accent, Color surface);

    /// <summary>WCAG 2.1 relative luminance, used for AccentOn selection.</summary>
    internal static double Luminance(Color c);
}
```

### Modified types

`shared\WindowsTrayCore\TrayTheme.cs` is substantially rewritten. The shape:

```csharp
public sealed class TrayTheme : IDisposable
{
    // Singleton
    public static TrayTheme Current { get; }

    // Constructors
    public TrayTheme();                           // production: live subscription
    internal TrayTheme(bool isLight, Color accent, bool isHighContrast); // tests

    // State
    public bool IsLight { get; }
    public bool IsHighContrast { get; }
    public ThemePreference Preference { get; }    // Auto / Light / Dark
    public void SetOverride(ThemePreference preference);

    // Tokens (12)
    public Color Surface { get; }
    public Color SurfaceAlt { get; }
    public Color SurfaceStroke { get; }
    public Color Foreground { get; }
    public Color ForegroundAlt { get; }
    public Color ForegroundDim { get; }
    public Color Accent { get; }
    public Color AccentOn { get; }
    public Color AccentSubtle { get; }
    public Color Warning { get; }
    public Color Error { get; }
    public Color Success { get; }

    // Events
    public event EventHandler? Changed;

    // Testing hook
    internal void SimulatePreferenceChanged(bool isLight);
    internal void SimulateAccentChanged(Color accent);
    internal void SimulateHighContrastChanged(bool on);

    // Dispose unsubscribes the message window
    public void Dispose();
}
```

`shared\WindowsTrayCore\ThemeApplier.cs` gains a title-bar pass:

```csharp
public static void Apply(Form form, TrayTheme? theme = null);
public static void Apply(Control container, TrayTheme? theme = null);

// New (internal): apply dark title bar via DwmSetWindowAttribute
internal static void ApplyTitleBar(Form form, bool dark);
```

`Apply(Form ...)` calls `ApplyTitleBar` once on the form before walking the control tree. The control-tree walk uses the new tokens (no old property references survive).

### Deleted properties

From `TrayTheme.cs`:

- `Background` (replaced by `Surface`)
- `Surface` (replaced by `SurfaceAlt`; the role shifts since old `Surface` was the inner panel surface)
- `Text` (replaced by `Foreground`)
- `TextMuted` (replaced by `ForegroundAlt`)
- `Accent` (kept name, behavior changes; no longer hardcoded purple)
- `Success` / `Error` (kept names, values retuned for Fluent palette)
- `Field` (folded into `SurfaceAlt`; if a real divergence appears post-migration, split back out)

The `Accent` rename is technically a same-name behavior change rather than a deletion. The old hardcoded purple is gone; the new `Accent` returns the system color. Callers that depended on the literal purple need to either accept the new value or pin their own color outside `TrayTheme`.

## Token Catalog

All values are Fluent-tuned for WCAG AA contrast against the surface tokens they sit on. Light values listed first, dark second. The accent and accent-derived tokens vary at runtime.

| Token | Light | Dark | Purpose |
|---|---|---|---|
| `Surface` | `#F4F4F8` | `#18182D` | Form outer background (was: `Background`) |
| `SurfaceAlt` | `#FFFFFF` | `#24243A` | Sunken inner panels, list rows, text fields (was: `Surface` + `Field`) |
| `SurfaceStroke` | `#D8D8E0` | `#3A3A52` | Borders, separators, group-box outlines |
| `Foreground` | `#1A1A2E` | `#E0DEE4` | Primary text (was: `Text`) |
| `ForegroundAlt` | `#5A5A70` | `#9A95B0` | Secondary text: hints, captions, drift indicators (was: `TextMuted`, retuned for higher contrast) |
| `ForegroundDim` | `#9090A4` | `#6E6A86` | Disabled controls, placeholder text |
| `Accent` | system | system | User's Windows accent (`DwmGetColorizationColor`) |
| `AccentOn` | derived | derived | Black or white for text on top of `Accent` |
| `AccentSubtle` | derived | derived | `Accent` at 24% blend over `Surface` for hover/focus |
| `Warning` | `#B45309` | `#FBBF24` | Drift hints, advisory yellows |
| `Error` | `#C0293A` | `#E05566` | Failures, invalid input |
| `Success` | `#2D8A4E` | `#50C878` | Confirmation, healthy state |

### `Accent` defaults when system source unavailable

If `DwmGetColorizationColor` fails AND the registry fallback returns nothing, `Accent` defaults to Windows' canonical blue: `#0078D4` (the same blue Windows uses when "Show accent color on title bars" is off).

### Migration mapping (old to new)

This table is the contract every consumer call site follows:

| Old | New | Notes |
|---|---|---|
| `Background` | `Surface` | Direct rename |
| `Surface` (was inner) | `SurfaceAlt` | Role shift: old "inner sunken" is the new alt-surface |
| `Text` | `Foreground` | Direct rename |
| `TextMuted` | `ForegroundAlt` | Retuned hex values for higher contrast in dark mode |
| `Accent` | `Accent` | Name unchanged; value is now system-following |
| `Success` | `Success` | Name unchanged; values retuned |
| `Error` | `Error` | Name unchanged; values retuned |
| `Field` | `SurfaceAlt` | Folded together; if controls visibly diverge post-migration, split back into a dedicated `FieldSurface` token |
| (none) | `SurfaceStroke` | New: replaces ad-hoc border colors and `SystemColors.ControlDark` references |
| (none) | `ForegroundDim` | New: replaces `SystemColors.GrayText` for placeholder / disabled text |
| (none) | `AccentOn` | New: contrast-derived text color on Accent |
| (none) | `AccentSubtle` | New: hover / focus rings |
| (none) | `Warning` | New: amber for drift hints (replaces ad-hoc `SystemColors.GrayText` use in BatteryTray drift label) |

## System integration

### Accent reading

`AccentColors.Read()` tries in order:

1. `DwmGetColorizationColor(out uint color, out bool opaque)`. Returns `color` as `0xAARRGGBB`. Strip alpha, use RGB. Documented Windows API, available since Vista. Returns the live colorization color (which tracks accent on systems where "Show accent color on title bars" is enabled).
2. Registry `HKCU\Software\Microsoft\Windows\DWM\AccentColor` (REG_DWORD, BGR-packed). Available on every modern Windows. Used when DWM API fails or returns transparent.
3. Hardcoded fallback `#0078D4` (Windows blue).

The DWM API is preferred because it stays correct when the user disables "Show accent color on title bars and window borders" (in that case the registry stores the chosen accent but the title bar shows white; we want to follow the chosen accent regardless).

### Live change detection

`TrayTheme` constructs a hidden `NativeWindow` (`ThemeMessageWindow`) at startup. The window subscribes to two messages:

- `WM_SETTINGCHANGE` (`0x001A`): fires on theme flip (light/dark), high-contrast toggle, and other Settings changes. The `wParam` indicates the change category. TrayTheme refreshes if `wParam` is `SPI_SETHIGHCONTRAST` (0x43) or if `lParam` is the string "ImmersiveColorSet" (broadcast when AppsUseLightTheme flips).
- `WM_DWMCOLORIZATIONCOLORCHANGED` (`0x0320`): fires when the user changes the accent color.

On either message, `TrayTheme` re-reads the relevant state (light/dark from registry; accent from DWM; high-contrast from `SystemParameters.HighContrast`), updates its cached values, and fires `Changed` on the message-pump thread.

The hidden window uses the same `NativeWindow` pattern as `TrayIcon.MessageWindow`. Lifetime is the singleton's lifetime; `Dispose` destroys the handle.

### Title bar dark mode

`ThemeApplier.ApplyTitleBar(Form form, bool dark)`:

```csharp
// Pseudocode
[DllImport("dwmapi.dll", PreserveSig = true)]
static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

const int DWMWA_USE_IMMERSIVE_DARK_MODE_PRE_20H1 = 19;
const int DWMWA_USE_IMMERSIVE_DARK_MODE          = 20;

int useDark = dark ? 1 : 0;
// Try attribute 20 first (Win10 1903+, Win11)
var hr = DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
if (hr != 0)
{
    // Fall back to the undocumented predecessor (Win10 1809-1902)
    DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_PRE_20H1, ref useDark, sizeof(int));
}
```

Failure is silent. On Win10 builds older than 1809 both calls return non-zero and the title bar stays light; that is acceptable behavior for an outdated host.

`ThemeApplier.Apply(Form form, ...)` calls `ApplyTitleBar(form, !theme.IsLight)` once before walking the control tree. The walk handles all visible repaints; the title bar handles the chrome the walk cannot reach.

Forms constructed after the first `Apply` call (e.g. dialogs opened from a settings form) get themed when the consumer calls `ThemeApplier.Apply` on them. There is no global form-creation hook; that scope is out of Phase 29.

## Failure modes

| Mode | Detection | Behavior |
|---|---|---|
| `DwmGetColorizationColor` returns non-zero HRESULT | inline check after P/Invoke | Fall through to registry; then fallback default |
| Registry `AccentColor` value missing or wrong type | `RegistryKey.GetValue` returns `null` or non-int | Fall through to fallback default |
| `WM_DWMCOLORIZATIONCOLORCHANGED` fires but `DwmGetColorizationColor` then fails | same as above | Keep previous Accent value; no fire of Changed |
| `DwmSetWindowAttribute` returns non-zero for both attributes 20 and 19 | inline check | No-op; title bar stays default-themed |
| `WM_SETTINGCHANGE` fires with unrelated `wParam` | filter in WndProc | Ignored |
| Theme override `SetOverride(Auto)` after a Light or Dark forced state | property change | Re-reads system theme, fires Changed |

`TrayTheme` swallows P/Invoke exceptions to keep the singleton stable; first-use after a failure returns the cached default. No exception ever propagates out of `Read()` or the message handler.

## Per-app changes

### BatteryTray

- `BatteryTrayContext.cs`: any `TrayTheme.Current.Background` / `Surface` / `Text` reference updates per the migration mapping.
- `SettingsForm.cs`: the existing `_themeCombo` ("Auto / Light / Dark") gains a `SelectedIndexChanged` handler that calls `TrayTheme.Current.SetOverride(ThemePreference.Auto | Light | Dark)`. Persisted as `AppSettings.Theme` (already exists today; currently unwired).
- The drift-hint label in the Hardware actions tab (Phase 27) switches from `SystemColors.GrayText` to `TrayTheme.Current.Warning` if amber is preferred for "drift" semantics; otherwise `ForegroundAlt`.

### NetProfileSwitcher

- `MainForm.cs`: token references migrate per mapping. The status panel's hardcoded greys (lines around theme-application code) switch to `ForegroundAlt`.

### ProgramHider

- `ProgramHiderContext.cs`, `SettingsForm.cs`: token references migrate per mapping. ProgramHider's themed settings form (Phase 20) consumes a lot of `Text` / `Background` references; mostly mechanical.

### SoundTracker

- `TrayApplicationContext.cs`, `SettingsForm.cs`, `RecentActivityForm.cs`: token references migrate per mapping. `RecentActivityForm` uses `TrayTheme.Current.Surface` for its list rows; that becomes `SurfaceAlt`.

### Shared library types

- `AboutDialog.cs`: migrates per mapping. Link colors continue to use `Accent` (which now follows system).
- `FirstRunBalloon.cs`: uses `TrayTheme.Current` for theming the balloon body. Migrates per mapping.
- `ThemeApplier.cs`: rewritten internally for new tokens + title bar. Public API surface (`Apply(Form)`, `Apply(Control)`) preserved.

## Testing

### Unit (`shared\WindowsTrayCore.Tests\`)

`TrayThemeTests.cs` (existing file, rewritten):

- `Construct_DefaultsToSystem_LightOrDark_BasedOnRegistry`
- `Tokens_LightTheme_MatchSpecifiedHex` (one assertion per token)
- `Tokens_DarkTheme_MatchSpecifiedHex` (one assertion per token)
- `AccentOn_OnLightAccent_IsBlack`
- `AccentOn_OnDarkAccent_IsWhite`
- `AccentSubtle_BlendsAt24Percent`
- `SetOverride_Light_OverridesSystemDark`
- `SetOverride_Auto_RestoresSystemFollowing`
- `SimulatePreferenceChanged_FiresChanged`
- `SimulateAccentChanged_FiresChanged`
- `SimulateAccentChanged_RecomputesAccentSubtle`

`AccentColorsTests.cs` (new):

- `DeriveOn_LightAccent_ReturnsBlack` (parameterized over yellow, light blue, white)
- `DeriveOn_DarkAccent_ReturnsWhite` (parameterized over navy, maroon, black)
- `DeriveSubtle_HasExpectedAlphaBlend` (compute by hand for one known case)
- `Luminance_KnownValues` (parameterized; pin WCAG formula correctness)

`ThemeApplierTests.cs` (existing file, augmented):

- `Apply_LightTheme_SetsControlColorsFromTokens` (existing; rewritten for new tokens)
- `Apply_DarkTheme_SetsControlColorsFromTokens` (existing; rewritten)
- `Apply_OnForm_CallsApplyTitleBar` (new; verifies the title-bar attempt happens, via a test seam)

### E2E / smoke

No new E2E coverage. The existing BatteryTray E2E suite confirms no regression. SoundTracker smoke tests confirm tray composition continues to work.

### Manual smoke (post-implementation)

1. Open Settings > Personalization > Colors.
2. Toggle "Choose your mode" between Light and Dark. Every open SettingsForm and AboutDialog should reflect the new theme within a frame.
3. Pick a different accent (e.g. Olive). All four app tray title bars, About dialog accent links, and BatteryTray's hardware-actions Save button hover ring should reflect the new accent.
4. Turn "Show accent color on title bars and window borders" off. Title bars stay default-Windows but accent inside the apps still follows the chosen color.
5. Toggle High Contrast mode (Left Alt + Left Shift + Print Screen). The apps should not crash; visual fidelity is best-effort.

## File manifest

### New files

| Path | Purpose |
|---|---|
| `shared\WindowsTrayCore\ThemePreference.cs` | The `Auto / Light / Dark` enum |
| `shared\WindowsTrayCore\AccentColors.cs` | DWM + registry accent reader, derivation helpers |
| `shared\WindowsTrayCore.Tests\AccentColorsTests.cs` | Unit tests for the helper |

### Modified files

| Path | Change |
|---|---|
| `shared\WindowsTrayCore\TrayTheme.cs` | Substantial rewrite: 12 tokens, hidden message window, accent + high-contrast detection, `SetOverride` |
| `shared\WindowsTrayCore\ThemeApplier.cs` | New tokens; new `ApplyTitleBar` pass on every Form |
| `shared\WindowsTrayCore.Tests\TrayThemeTests.cs` | Rewritten for new tokens + override + simulation hooks |
| `shared\WindowsTrayCore.Tests\ThemeApplierTests.cs` | Rewritten for new tokens; new title-bar test |
| `shared\WindowsTrayCore\AboutDialog.cs` | Token migration |
| `shared\WindowsTrayCore\FirstRunBalloon.cs` | Token migration |
| `apps\BatteryTray\BatteryTray\BatteryTrayContext.cs` | Token migration |
| `apps\BatteryTray\BatteryTray\SettingsForm.cs` | Token migration + wire theme combo to `SetOverride` |
| `apps\NetProfileSwitcher\UI\MainForm.cs` | Token migration |
| `apps\ProgramHider\app\ProgramHider\ProgramHiderContext.cs` | Token migration |
| `apps\ProgramHider\app\ProgramHider\SettingsForm.cs` | Token migration |
| `apps\SoundTracker\SoundTracker.App\TrayApplicationContext.cs` | Token migration |
| `apps\SoundTracker\SoundTracker.App\SettingsForm.cs` | Token migration |
| `apps\SoundTracker\SoundTracker.App\RecentActivityForm.cs` | Token migration |
| `WORKLOG.md` | New entry on the final commit |

### Unchanged

- `WindowsAppCore`: no shared-library changes outside `WindowsTrayCore`.
- Native interop: no new P/Invoke beyond `dwmapi.dll` (`DwmGetColorizationColor`, `DwmSetWindowAttribute`).
- `install.ps1`, `.github\workflows\*`: no changes.
- `Directory.Build.props`: no changes.

## Open caveats

1. **Win10 build coverage for dark title bar.** Builds older than 1809 silently skip the title-bar tint. Acceptable on a development target running 21H2; flag if a deployment surface predates 1809.
2. **WinForms native control painting under dark theme.** Buttons, ComboBox dropdown arrows, and the scrollbar inside ListView still render via the OS theme service, which does not know about `Accent` or `Surface`. `ThemeApplier` sets `BackColor` and `ForeColor` correctly but the hot/hover states on buttons may still render light-mode chrome over a dark background. Phase 29 accepts this; full owner-draw control theming is a separate phase.
3. **Per-app override scope.** Only BatteryTray gains an active override in Phase 29 (it had the unwired UI already). NetProfileSwitcher, ProgramHider, and SoundTracker continue to passively follow the system. If user demand surfaces for unified override UIs across all four, that is a per-app feature commit, not infrastructure.
4. **Accent color contrast on `Warning` / `Error` / `Success`.** Fixed palette values target WCAG AA against `Surface` / `SurfaceAlt`. If a future custom accent washes them out (e.g. an amber accent against the `Warning` token), the visible distinction may erode. The fixed palette is a conscious trade against compounding accent-tinted variants of every status token.
5. **High-contrast mode passthrough.** Phase 29 detects high-contrast but lets Windows draw the controls as-is. The result is functional but visually inconsistent with the rest of the theme. Full HC theming, including custom palettes and `SystemColors` integration, is a follow-up phase if accessibility audits demand it.
