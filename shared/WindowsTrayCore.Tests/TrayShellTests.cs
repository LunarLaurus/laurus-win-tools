using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class TrayShellTests
{
    private sealed class StubIconProvider : ITrayIconProvider
    {
        public System.Drawing.Icon Render(TrayTheme theme) =>
            (System.Drawing.Icon)SystemIcons.Application.Clone();
    }

    private static TrayShell CreateShell()
    {
        var theme = new TrayTheme(isLight: false);
        var menu = new ContextMenuStrip();
        var provider = new StubIconProvider();
        var notifier = new BalloonNotifier(new NotifyIcon());
        return new TrayShell(menu, provider, theme, notifier);
    }

    [WindowsFact]
    public void NotifyIcon_ExposedAndNotNull()
    {
        using var shell = CreateShell();
        shell.NotifyIcon.Should().NotBeNull();
    }

    [WindowsFact]
    public void Menu_MatchesPassedStrip()
    {
        var theme = new TrayTheme(isLight: false);
        var menu = new ContextMenuStrip();
        var provider = new StubIconProvider();
        using var notifier = new BalloonNotifier(new NotifyIcon());
        using var shell = new TrayShell(menu, provider, theme, notifier);
        shell.Menu.Should().BeSameAs(menu);
    }

    [WindowsFact]
    public void Notifier_ExposedAndNotNull()
    {
        using var shell = CreateShell();
        shell.Notifier.Should().NotBeNull();
    }

    [WindowsFact]
    public void Icons_ExposedAndNotNull()
    {
        using var shell = CreateShell();
        shell.Icons.Should().NotBeNull();
    }

    [WindowsFact]
    public void SetTooltip_ShorterThanMax_Accepted()
    {
        using var shell = CreateShell();
        shell.SetTooltip("Short");
        shell.NotifyIcon.Text.Should().Be("Short");
    }

    [WindowsFact]
    public void SetTooltip_LongerThanMax_Truncated()
    {
        using var shell = CreateShell();
        shell.SetTooltip(new string('z', 100));
        shell.NotifyIcon.Text.Length.Should().Be(TrayTooltip.MaxLength);
    }

    [WindowsFact]
    public void Dispose_DoesNotThrow()
    {
        var shell = CreateShell();
        var act = () => shell.Dispose();
        act.Should().NotThrow();
    }
}
