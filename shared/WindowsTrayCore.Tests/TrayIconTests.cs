using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class TrayIconTests
{
    [WindowsFact]
    public void Construct_FromGuid_ExposesIt()
    {
        var g = Guid.Parse("7c15d17a-28f2-43a6-9463-82f9c3499a3c");
        using var icon = new TrayIcon(g);
        icon.Guid.Should().Be(g);
    }

    [WindowsFact]
    public void ForApp_DerivesGuidFromAppId()
    {
        using var a = TrayIcon.ForApp("MyApp");
        using var b = TrayIcon.ForApp("MyApp");
        a.Guid.Should().Be(b.Guid);
    }

    [WindowsFact]
    public void Text_TruncatesPast127Chars()
    {
        using var icon = TrayIcon.ForApp("TruncTest");
        icon.Text = new string('A', 200);
        icon.Text.Length.Should().Be(127);
    }

    [WindowsFact]
    public void Visible_RoundTrips()
    {
        // Construction without setting Visible -> false. Toggle then unset.
        using var icon = TrayIcon.ForApp("VisibleTest");
        icon.Visible.Should().BeFalse();
        // We can't reliably verify the actual tray registration in a headless
        // test environment, but the property should at least track its set value.
        icon.Visible = true;
        icon.Visible.Should().BeTrue();
        icon.Visible = false;
        icon.Visible.Should().BeFalse();
    }

    [WindowsFact]
    public void Dispose_IsIdempotent()
    {
        var icon = TrayIcon.ForApp("DisposeTest");
        icon.Dispose();
        icon.Dispose(); // second call must not throw
    }
}
