using System.Drawing;
using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class TrayThemeTests
{
    [WindowsFact]
    public void Current_IsNotNull()
    {
        TrayTheme.Current.Should().NotBeNull();
    }

    [WindowsFact]
    public void Current_ReturnsSameInstance()
    {
        TrayTheme.Current.Should().BeSameAs(TrayTheme.Current);
    }

    [Fact]
    public void DarkPalette_AllTokensNonDefault()
    {
        var theme = new TrayTheme(isLight: false);
        theme.Surface.Should().NotBe(Color.Empty);
        theme.SurfaceAlt.Should().NotBe(Color.Empty);
        theme.SurfaceStroke.Should().NotBe(Color.Empty);
        theme.Foreground.Should().NotBe(Color.Empty);
        theme.ForegroundAlt.Should().NotBe(Color.Empty);
        theme.ForegroundDim.Should().NotBe(Color.Empty);
        theme.Accent.Should().NotBe(Color.Empty);
        theme.AccentOn.Should().NotBe(Color.Empty);
        theme.Warning.Should().NotBe(Color.Empty);
        theme.Error.Should().NotBe(Color.Empty);
        theme.Success.Should().NotBe(Color.Empty);
    }

    [Fact]
    public void LightPalette_DiffersFromDark()
    {
        var dark  = new TrayTheme(isLight: false);
        var light = new TrayTheme(isLight: true);
        dark.Surface.Should().NotBe(light.Surface);
        dark.SurfaceAlt.Should().NotBe(light.SurfaceAlt);
        dark.Foreground.Should().NotBe(light.Foreground);
    }

    [Fact]
    public void IsLight_ReflectsConstructorArg()
    {
        new TrayTheme(isLight: false).IsLight.Should().BeFalse();
        new TrayTheme(isLight: true).IsLight.Should().BeTrue();
    }

    [WindowsFact]
    public void Changed_FiredWhenIsLightFlips()
    {
        var theme = new TrayTheme(isLight: false);
        var fired = false;
        theme.Changed += (_, _) => fired = true;
        theme.SimulatePreferenceChanged(isLight: true);
        fired.Should().BeTrue();
    }

    [WindowsFact]
    public void Changed_NotFiredWhenIsLightUnchanged()
    {
        var theme = new TrayTheme(isLight: false);
        var fired = false;
        theme.Changed += (_, _) => fired = true;
        theme.SimulatePreferenceChanged(isLight: false);
        fired.Should().BeFalse();
    }

    // ── New Fluent token tests ─────────────────────────────────────────────

    [Fact]
    public void Surface_LightTheme_IsCanonicalLight()
    {
        new TrayTheme(isLight: true).Surface.Should().Be(Color.FromArgb(0xF4, 0xF4, 0xF8));
    }

    [Fact]
    public void Surface_DarkTheme_IsCanonicalDark()
    {
        new TrayTheme(isLight: false).Surface.Should().Be(Color.FromArgb(0x18, 0x18, 0x2D));
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
    public void ForegroundAlt_DarkTheme_HasHigherContrastThanOldTextMuted()
    {
        new TrayTheme(isLight: false).ForegroundAlt.Should().Be(Color.FromArgb(0x9A, 0x95, 0xB0));
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
        var darkAccent = Color.FromArgb(0, 0x78, 0xD4);
        var lightAccent = Color.FromArgb(0xFB, 0xBF, 0x24);

        var darkTheme = new TrayTheme(isLight: false, accent: darkAccent, isHighContrast: false);
        var lightTheme = new TrayTheme(isLight: false, accent: lightAccent, isHighContrast: false);

        darkTheme.AccentOn.Should().Be(Color.FromArgb(0xFF, 0xFF, 0xFF));
        lightTheme.AccentOn.Should().Be(Color.FromArgb(0, 0, 0));
    }

    [Fact]
    public void AccentSubtle_IsAccentBlendedOverSurface()
    {
        var accent = Color.FromArgb(0xFF, 0, 0);
        var theme = new TrayTheme(isLight: true, accent: accent, isHighContrast: false);

        theme.AccentSubtle.G.Should().BeLessThan(theme.Surface.G);
    }

    // ── Live change simulation tests ───────────────────────────────────────

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

    // ── SetOverride tests ──────────────────────────────────────────────────

    [Fact]
    public void Preference_Default_IsAuto()
    {
        new TrayTheme(isLight: true).Preference.Should().Be(ThemePreference.Auto);
    }

    [Fact]
    public void SetOverride_Light_ForcesIsLightTrue()
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
    public void SetOverride_FiresChanged_WhenPaletteOrPreferenceChanges()
    {
        var theme = new TrayTheme(isLight: true, accent: Color.Red, isHighContrast: false);
        var fired = 0;
        theme.Changed += (_, _) => fired++;

        theme.SetOverride(ThemePreference.Dark);

        fired.Should().Be(1);
    }

    [Fact]
    public void SetOverride_Auto_RestoresSystemFollowing()
    {
        // Construct as dark, force Light, switch back to Auto, then
        // simulate a system flip to dark and verify Auto picks it up.
        var theme = new TrayTheme(isLight: false, accent: Color.Red, isHighContrast: false);
        theme.SetOverride(ThemePreference.Light);
        theme.IsLight.Should().BeTrue();

        theme.SetOverride(ThemePreference.Auto);
        theme.Preference.Should().Be(ThemePreference.Auto);

        // After Auto, SimulatePreferenceChanged should take effect again.
        theme.SimulatePreferenceChanged(false);
        theme.IsLight.Should().BeFalse();
    }
}
