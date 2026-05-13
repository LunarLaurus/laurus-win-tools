using FluentAssertions;
using Xunit;

namespace BatteryTray.Tests;

public class PerformanceCounterPowerSamplerTests
{
    [Fact]
    public void ComputeOne_ZeroElapsed_ReturnsNull()
    {
        var s = PerformanceCounterPowerSampler.ComputeOne(
            pid: 1, name: "test", cpuMs: 100, ioBytesDelta: 0,
            elapsed: TimeSpan.Zero, logicalCores: 4);

        s.Should().BeNull();
    }

    [Fact]
    public void ComputeOne_FullCpuOnAllCores_Returns100Percent()
    {
        // 1 second window, 4 cores, 4000ms of cumulative CPU = 100%
        var s = PerformanceCounterPowerSampler.ComputeOne(
            pid: 1, name: "burner", cpuMs: 4000, ioBytesDelta: 0,
            elapsed: TimeSpan.FromSeconds(1), logicalCores: 4);

        s.Should().NotBeNull();
        s!.Value.CpuPercent.Should().BeApproximately(100.0, precision: 0.01);
    }

    [Fact]
    public void ComputeOne_HalfCpuOnOneCore_AppearsAsExpectedShare()
    {
        // 1 second, 4 cores, 500ms CPU = 12.5% (one core at half)
        var s = PerformanceCounterPowerSampler.ComputeOne(
            pid: 1, name: "test", cpuMs: 500, ioBytesDelta: 0,
            elapsed: TimeSpan.FromSeconds(1), logicalCores: 4);

        s.Should().NotBeNull();
        s!.Value.CpuPercent.Should().BeApproximately(12.5, precision: 0.01);
    }

    [Fact]
    public void ComputeOne_OverbudgetCpu_GetsClamped()
    {
        // 1 second elapsed but 5 seconds of CPU reported (driver glitch on
        // some Win11 builds). Don't return 500%, clamp to 100.
        var s = PerformanceCounterPowerSampler.ComputeOne(
            pid: 1, name: "broken", cpuMs: 5000, ioBytesDelta: 0,
            elapsed: TimeSpan.FromSeconds(1), logicalCores: 1);

        s.Should().NotBeNull();
        s!.Value.CpuPercent.Should().BeLessOrEqualTo(100);
    }

    [Fact]
    public void ComputeOne_NegativeIoBytes_TreatedAsZero()
    {
        // Process IO counter should be monotonic, but if a process recycles its
        // pid we might see a negative delta. Don't produce nonsense rates.
        var s = PerformanceCounterPowerSampler.ComputeOne(
            pid: 1, name: "test", cpuMs: 1000, ioBytesDelta: -1_000_000,
            elapsed: TimeSpan.FromSeconds(1), logicalCores: 4);

        s.Should().NotBeNull();
        s!.Value.IoBytesPerSec.Should().Be(0);
    }

    [Fact]
    public void ComputeOne_BelowNoiseFloor_ReturnsNull()
    {
        // ~0% CPU and 0 IO — the System Idle Process equivalent. Filter out.
        var s = PerformanceCounterPowerSampler.ComputeOne(
            pid: 0, name: "Idle", cpuMs: 1, ioBytesDelta: 0,
            elapsed: TimeSpan.FromSeconds(1), logicalCores: 4);

        s.Should().BeNull();
    }

    [Fact]
    public void ComputeOne_LogicalCoresZero_DoesNotDivideByZero()
    {
        // Defensive: logicalCores=0 would crash with /0; should be clamped to 1.
        var act = () => PerformanceCounterPowerSampler.ComputeOne(
            pid: 1, name: "test", cpuMs: 100, ioBytesDelta: 0,
            elapsed: TimeSpan.FromSeconds(1), logicalCores: 0);

        act.Should().NotThrow();
    }
}
