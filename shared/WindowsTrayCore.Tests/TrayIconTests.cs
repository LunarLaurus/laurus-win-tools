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
    public void Tooltip_AssignBuilder_StoresBuiltString()
    {
        using var icon = TrayIcon.ForApp("TooltipBuilderTest");

        icon.Tooltip = new TrayTooltipBuilder()
            .AddRequired("line one")
            .AddRequired("line two");

        icon.GetTipForTesting().Should().Be("line one\nline two");
    }

    [WindowsFact]
    public void TooltipText_AssignString_StoresAsRequiredLine()
    {
        using var icon = TrayIcon.ForApp("TooltipTextTest");

        icon.TooltipText = "single-line tooltip";

        icon.GetTipForTesting().Should().Be("single-line tooltip");
    }

    [WindowsFact]
    public void Tooltip_AssignNullBuilder_StoresEmpty()
    {
        using var icon = TrayIcon.ForApp("TooltipNullTest");

        icon.Tooltip = null!;

        icon.GetTipForTesting().Should().BeEmpty();
    }

    [WindowsFact]
    public void TooltipText_AssignNullString_StoresEmpty()
    {
        using var icon = TrayIcon.ForApp("TooltipTextNullTest");

        icon.TooltipText = null!;

        icon.GetTipForTesting().Should().BeEmpty();
    }

    [WindowsFact]
    public void Tooltip_AdversarialLongInput_NeverExceedsBudget()
    {
        using var icon = TrayIcon.ForApp("TooltipBudgetTest");

        icon.Tooltip = new TrayTooltipBuilder()
            .AddRequired(new string('A', 200));

        icon.GetTipForTesting().Length.Should().BeLessOrEqualTo(TrayTooltipBuilder.MaxLength);
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
