using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class TrayIconMetricsTests
{
    [WindowsFact]
    public void DisplayedSize_IsPositive()
    {
        TrayIconMetrics.DisplayedSize().Should().BeGreaterThan(0);
    }

    [WindowsFact]
    public void DisplayedSize_IsSmallIconRange()
    {
        // Windows small tray icons live in the 16-48px band depending on DPI.
        var size = TrayIconMetrics.DisplayedSize();
        size.Should().BeInRange(16, 48);
    }

    [WindowsFact]
    public void RecommendedRenderSize_IsAtLeastDoubleDisplayed()
    {
        TrayIconMetrics.RecommendedRenderSize().Should().Be(TrayIconMetrics.DisplayedSize() * 2);
    }
}
