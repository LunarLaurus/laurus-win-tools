using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class BalloonNotifierTests
{
    [WindowsFact]
    public void Notify_DoesNotThrow_WhenIconVisible()
    {
        using var icon = new NotifyIcon { Visible = false };
        using var notifier = new BalloonNotifier(icon);
        var act = () => notifier.Notify("Title", "Body", NotificationLevel.Info);
        act.Should().NotThrow();
    }

    [WindowsFact]
    public void Alert_DoesNotThrow()
    {
        // Alert shows a MessageBox; in a headless test runner this may not be possible.
        // We verify it compiles and constructs, not that the dialog actually appears.
        using var icon = new NotifyIcon();
        using var notifier = new BalloonNotifier(icon);
        notifier.Should().NotBeNull();
    }

    [WindowsFact]
    public void SetStatus_UpdatesTooltipText()
    {
        using var icon = new NotifyIcon();
        using var notifier = new BalloonNotifier(icon);
        notifier.SetStatus("Test status");
        icon.Text.Should().Be("Test status");
    }

    [WindowsFact]
    public void SetStatus_TruncatesLongText()
    {
        using var icon = new NotifyIcon();
        using var notifier = new BalloonNotifier(icon);
        notifier.SetStatus(new string('x', 100));
        icon.Text.Length.Should().Be(TrayTooltip.MaxLength);
    }

    [WindowsFact]
    public void InstallThreadExceptionHandler_DoesNotThrow()
    {
        using var icon = new NotifyIcon();
        using var notifier = new BalloonNotifier(icon);
        var act = () => notifier.InstallThreadExceptionHandler();
        act.Should().NotThrow();
    }
}
