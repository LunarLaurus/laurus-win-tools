using FluentAssertions;
using WindowsTrayCore;
using Xunit;

namespace SoundTracker.Tests;

public class RecentActivityFormTests : IDisposable
{
    public void Dispose() => TrayTheme.Current.SimulatePreferenceChanged(isLight: false);

    [WindowsFact]
    public void BackColor_DarkMode_MatchesTrayThemeBackground()
    {
        TrayTheme.Current.SimulatePreferenceChanged(isLight: false);
        using var form = new SoundTracker.App.RecentActivityForm();
        form.BackColor.Should().Be(TrayTheme.Current.Background);
    }

    [WindowsFact]
    public void BackColor_LightMode_MatchesTrayThemeBackground()
    {
        TrayTheme.Current.SimulatePreferenceChanged(isLight: true);
        using var form = new SoundTracker.App.RecentActivityForm();
        form.BackColor.Should().Be(TrayTheme.Current.Background);
    }

    [WindowsFact]
    public void BackColor_UpdatesWhenThemeChanges()
    {
        TrayTheme.Current.SimulatePreferenceChanged(isLight: false);
        using var form = new SoundTracker.App.RecentActivityForm();
        var darkBg = form.BackColor;

        TrayTheme.Current.SimulatePreferenceChanged(isLight: true);

        form.BackColor.Should().NotBe(darkBg);
        form.BackColor.Should().Be(TrayTheme.Current.Background);
    }
}
