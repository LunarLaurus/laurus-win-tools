using System.Drawing;
using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class TrayIconManagerTests
{
    // Minimal provider that records render calls and returns a new Icon each time.
    private sealed class CountingProvider : ITrayIconProvider
    {
        public int RenderCount;
        public Icon Render(TrayTheme theme)
        {
            RenderCount++;
            // SystemIcons are shared/not-owned so we clone to give the manager something to dispose.
            return (Icon)SystemIcons.Application.Clone();
        }
    }

    [WindowsFact]
    public void ForceRefresh_CallsProviderAndUpdatesIcon()
    {
        var provider = new CountingProvider();
        var theme = new TrayTheme(isLight: false);
        using var icon = new NotifyIcon();
        using var mgr = new TrayIconManager(icon, provider, theme);

        mgr.ForceRefresh();

        provider.RenderCount.Should().Be(1);
        icon.Icon.Should().NotBeNull();
    }

    [WindowsFact]
    public void ForceRefresh_DisposesOldIcon()
    {
        var provider = new CountingProvider();
        var theme = new TrayTheme(isLight: false);
        using var icon = new NotifyIcon();
        using var mgr = new TrayIconManager(icon, provider, theme);

        mgr.ForceRefresh();
        mgr.ForceRefresh();   // second call — old icon should be disposed

        provider.RenderCount.Should().Be(2);
    }

    [WindowsFact]
    public void ThemeChange_TriggersForceRefresh()
    {
        var provider = new CountingProvider();
        var theme = new TrayTheme(isLight: false);
        using var icon = new NotifyIcon();
        using var mgr = new TrayIconManager(icon, provider, theme);

        theme.SimulatePreferenceChanged(isLight: true);

        provider.RenderCount.Should().Be(1);
    }

    [WindowsFact]
    public void Dispose_UnsubscribesFromTheme_NoMoreRenders()
    {
        var provider = new CountingProvider();
        var theme = new TrayTheme(isLight: false);
        using var icon = new NotifyIcon();
        var mgr = new TrayIconManager(icon, provider, theme);
        mgr.Dispose();

        theme.SimulatePreferenceChanged(isLight: true);

        provider.RenderCount.Should().Be(0);
    }
}
