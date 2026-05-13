using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class FirstRunBalloonTests
{
    [WindowsFact]
    public void ShowIfNeeded_AlreadyShown_DoesNotInvokeMarkShown()
    {
        using var icon = TrayIcon.ForApp("FirstRunBalloonTest1");
        icon.Visible = true;
        bool markCalled = false;
        FirstRunBalloon.ShowIfNeeded(icon, shownAlready: true,
            markShown: () => markCalled = true, "App", "msg");
        markCalled.Should().BeFalse();
    }

    [WindowsFact]
    public void ShowIfNeeded_IconHidden_DoesNotInvokeMarkShown()
    {
        using var icon = TrayIcon.ForApp("FirstRunBalloonTest2");
        // Visible defaults to false.
        bool markCalled = false;
        FirstRunBalloon.ShowIfNeeded(icon, shownAlready: false,
            markShown: () => markCalled = true, "App", "msg");
        markCalled.Should().BeFalse();
    }

    [WindowsFact]
    public void ShowIfNeeded_FirstRunVisibleIcon_InvokesMarkShownExactlyOnce()
    {
        using var icon = TrayIcon.ForApp("FirstRunBalloonTest3");
        icon.Visible = true;
        int markCount = 0;
        FirstRunBalloon.ShowIfNeeded(icon, shownAlready: false,
            markShown: () => markCount++, "App", "msg");
        markCount.Should().Be(1);
    }
}
