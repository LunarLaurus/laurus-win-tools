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
    public void DarkPalette_BackgroundMatchesSpec()
    {
        var theme = new TrayTheme(isLight: false);
        theme.Background.Should().Be(Color.FromArgb(0x18, 0x18, 0x2d));
    }

    [Fact]
    public void LightPalette_BackgroundMatchesSpec()
    {
        var theme = new TrayTheme(isLight: true);
        theme.Background.Should().Be(Color.FromArgb(0xf4, 0xf4, 0xf8));
    }

    [Fact]
    public void DarkPalette_AllColorsNonDefault()
    {
        var theme = new TrayTheme(isLight: false);
        theme.Background.Should().NotBe(Color.Empty);
        theme.Surface.Should().NotBe(Color.Empty);
        theme.Text.Should().NotBe(Color.Empty);
        theme.TextMuted.Should().NotBe(Color.Empty);
        theme.Accent.Should().NotBe(Color.Empty);
        theme.Success.Should().NotBe(Color.Empty);
        theme.Error.Should().NotBe(Color.Empty);
        theme.Field.Should().NotBe(Color.Empty);
    }

    [Fact]
    public void LightPalette_DiffersFromDark()
    {
        var dark  = new TrayTheme(isLight: false);
        var light = new TrayTheme(isLight: true);
        dark.Background.Should().NotBe(light.Background);
        dark.Surface.Should().NotBe(light.Surface);
        dark.Text.Should().NotBe(light.Text);
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
}
