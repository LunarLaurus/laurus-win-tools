using FluentAssertions;
using Xunit;

namespace BatteryTray.Tests;

public class RateHistoryTests
{
    [Fact]
    public void Empty_HasNoData()
    {
        var h = new RateHistory();
        h.HasData.Should().BeFalse();
        h.Snapshot().Should().BeEmpty();
    }

    [Fact]
    public void Record_NullRate_IsIgnored()
    {
        var h = new RateHistory();
        h.Record(null, 50);
        h.Snapshot().Should().BeEmpty();
    }

    [Fact]
    public void Record_StoresSampleAndPercent()
    {
        var h = new RateHistory();
        h.Record(-1500, 87);

        var samples = h.Snapshot();
        samples.Should().HaveCount(1);
        samples[0].RateMilliwatts.Should().Be(-1500);
        samples[0].Percent.Should().Be(87);
    }

    [Fact]
    public void Record_MultipleSamples_PreservesOrder()
    {
        var h = new RateHistory();
        h.Record(-1000, 90);
        h.Record(-1100, 89);
        h.Record(-1200, 88);

        var snap = h.Snapshot();
        snap.Should().HaveCount(3);
        snap[0].RateMilliwatts.Should().Be(-1000);
        snap[1].RateMilliwatts.Should().Be(-1100);
        snap[2].RateMilliwatts.Should().Be(-1200);
    }

    [Fact]
    public void Clear_RemovesAllSamples()
    {
        var h = new RateHistory();
        h.Record(-1000, 90);
        h.Record(-1100, 89);
        h.Clear();

        h.HasData.Should().BeFalse();
        h.Snapshot().Should().BeEmpty();
    }

    [Fact]
    public void HasData_RequiresAtLeastTwoSamples()
    {
        // Sparkline needs ≥2 points to draw a line; HasData reflects that.
        var h = new RateHistory();
        h.Record(-1000, 90);
        h.HasData.Should().BeFalse();

        h.Record(-1100, 89);
        h.HasData.Should().BeTrue();
    }
}
