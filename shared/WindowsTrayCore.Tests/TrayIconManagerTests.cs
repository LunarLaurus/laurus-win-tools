using System.Drawing;
using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class TrayIconManagerTests
{
    // Minimal provider that records render calls and returns a new Icon each time.
    // Uses the default HasChanged = true so every RequestRefresh triggers a render.
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

    // Provider that auto-resets HasChanged to false after each Render(), matching real provider behaviour.
    private sealed class DirtyFlagProvider : ITrayIconProvider
    {
        private bool _dirty = true;
        public bool HasChanged => _dirty;
        public int RenderCount;
        public void MarkDirty() => _dirty = true;
        public Icon Render(TrayTheme theme)
        {
            RenderCount++;
            _dirty = false;
            return (Icon)SystemIcons.Application.Clone();
        }
    }

    // Provider whose HasChanged is always false — simulates a provider that has been rendered
    // and has seen no new state changes since.
    private sealed class AlwaysCleanProvider : ITrayIconProvider
    {
        public bool HasChanged => false;
        public int RenderCount;
        public Icon Render(TrayTheme theme)
        {
            RenderCount++;
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

    // ── Dirty-flag: RequestRefresh honours HasChanged ───────────────────────

    [WindowsFact]
    public void RequestRefresh_WhenProviderDirty_CallsRender()
    {
        var provider = new DirtyFlagProvider();  // starts dirty
        var theme = new TrayTheme(isLight: false);
        using var icon = new NotifyIcon();
        using var mgr = new TrayIconManager(icon, provider, theme);

        mgr.RequestRefresh();

        provider.RenderCount.Should().Be(1);
    }

    [WindowsFact]
    public void RequestRefresh_WhenProviderClean_SkipsRender()
    {
        var provider = new AlwaysCleanProvider();
        var theme = new TrayTheme(isLight: false);
        using var icon = new NotifyIcon();
        using var mgr = new TrayIconManager(icon, provider, theme);

        mgr.RequestRefresh();

        provider.RenderCount.Should().Be(0);
    }

    [WindowsFact]
    public void RequestRefresh_AfterRender_SubsequentCallSkipped()
    {
        var provider = new DirtyFlagProvider();  // starts dirty; Render resets to clean
        var theme = new TrayTheme(isLight: false);
        using var icon = new NotifyIcon();
        using var mgr = new TrayIconManager(icon, provider, theme);

        mgr.RequestRefresh();   // dirty → renders, resets to clean
        mgr.RequestRefresh();   // clean → skipped

        provider.RenderCount.Should().Be(1);
    }

    [WindowsFact]
    public void RequestRefresh_AfterMarkDirty_RendersAgain()
    {
        var provider = new DirtyFlagProvider();
        var theme = new TrayTheme(isLight: false);
        using var icon = new NotifyIcon();
        using var mgr = new TrayIconManager(icon, provider, theme);

        mgr.RequestRefresh();   // renders, resets to clean
        provider.MarkDirty();
        mgr.RequestRefresh();   // dirty again → renders

        provider.RenderCount.Should().Be(2);
    }

    [WindowsFact]
    public void ForceRefresh_IgnoresDirtyFlag_AlwaysRenders()
    {
        var provider = new AlwaysCleanProvider();  // HasChanged always false
        var theme = new TrayTheme(isLight: false);
        using var icon = new NotifyIcon();
        using var mgr = new TrayIconManager(icon, provider, theme);

        mgr.ForceRefresh();

        provider.RenderCount.Should().Be(1);
    }

    [WindowsFact]
    public void ThemeChange_BypassesDirtyFlag_AlwaysRenders()
    {
        // Theme change uses ForceRefresh internally — dirty-flag must not block it.
        var provider = new AlwaysCleanProvider();
        var theme = new TrayTheme(isLight: false);
        using var icon = new NotifyIcon();
        using var mgr = new TrayIconManager(icon, provider, theme);

        theme.SimulatePreferenceChanged(isLight: true);

        provider.RenderCount.Should().Be(1);
    }
}
