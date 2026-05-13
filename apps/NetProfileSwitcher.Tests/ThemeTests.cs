using System.Drawing;
using FluentAssertions;
using WindowsTrayCore;
using Xunit;
using NetProfileSwitcher.UI;

namespace NetProfileSwitcher.Tests;

public class ThemeTests : IDisposable
{
    public void Dispose() => TrayTheme.Current.SimulatePreferenceChanged(isLight: false);

    [WindowsFact]
    public void Bg_DelegatesToTrayThemeCurrent()
    {
        TrayTheme.Current.SimulatePreferenceChanged(isLight: false);
        Theme.Bg.Should().Be(TrayTheme.Current.Background);
    }

    [WindowsFact]
    public void Bg_ChangesWithTheme()
    {
        TrayTheme.Current.SimulatePreferenceChanged(isLight: false);
        var dark = Theme.Bg;
        TrayTheme.Current.SimulatePreferenceChanged(isLight: true);
        Theme.Bg.Should().NotBe(dark);
    }

    [WindowsFact]
    public void Surface2_DarkMode_ReturnsExpectedColor()
    {
        TrayTheme.Current.SimulatePreferenceChanged(isLight: false);
        Theme.Surface2.Should().Be(Color.FromArgb(48, 48, 68));
    }

    [WindowsFact]
    public void Surface2_LightMode_ReturnsExpectedColor()
    {
        TrayTheme.Current.SimulatePreferenceChanged(isLight: true);
        Theme.Surface2.Should().Be(Color.FromArgb(225, 225, 240));
    }

    [WindowsFact]
    public void AccentDim_DarkMode_ReturnsExpectedColor()
    {
        TrayTheme.Current.SimulatePreferenceChanged(isLight: false);
        Theme.AccentDim.Should().Be(Color.FromArgb(90, 80, 180));
    }

    [WindowsFact]
    public void AccentDim_LightMode_ReturnsExpectedColor()
    {
        TrayTheme.Current.SimulatePreferenceChanged(isLight: true);
        Theme.AccentDim.Should().Be(Color.FromArgb(74, 63, 178));
    }
}
