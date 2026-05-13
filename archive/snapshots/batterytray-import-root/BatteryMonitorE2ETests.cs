using FluentAssertions;
using Xunit;

namespace BatteryTray.E2ETests;

/// <summary>
/// Hits the real Windows BatteryMonitor + WinRT APIs. Runs only on Windows;
/// on dev boxes without batteries (desktops, VMs), most assertions accept the
/// "no battery" reading as valid output. The point isn't to assert specific
/// numbers — it's to confirm the API surface doesn't throw when exercised on
/// real hardware.
/// </summary>
public class BatteryMonitorE2ETests
{
    [WindowsFact]
    public void Read_DoesNotThrow_OnRealHardware()
    {
        var monitor = new BatteryMonitor();
        var act = () => monitor.Read();
        act.Should().NotThrow();
    }

    [WindowsFact]
    public void Read_ReturnsConsistentState()
    {
        // If a battery is present, percent should be 0..100 and the flags
        // should make sense relative to each other.
        var monitor = new BatteryMonitor();
        var state = monitor.Read();

        state.Percent.Should().BeInRange(0, 100);

        if (state.IsCharging)
        {
            state.HasBattery.Should().BeTrue(because: "can't charge without a battery");
            state.IsOnAcPower.Should().BeTrue(because: "can't charge without AC");
            state.Percent.Should().BeLessThan(100,
                because: "BatteryMonitor.IsCharging should be false at 100%");
        }

        if (!state.HasBattery)
        {
            state.IsCharging.Should().BeFalse();
            state.SecondsRemaining.Should().BeNull();
        }
    }

    [WindowsFact]
    public void Read_TwiceInQuickSuccession_UsesCacheAndDoesNotDrift()
    {
        var monitor = new BatteryMonitor();

        var a = monitor.Read();
        var b = monitor.Read();

        // Within a 1s cache window, the WinRT-derived fields should be identical
        // (we cache the WinRT report). PowerStatus may briefly differ if a tick
        // landed between, so we only assert on the cached fields.
        b.ChargeRateMilliwatts.Should().Be(a.ChargeRateMilliwatts);
        b.RemainingMilliwattHours.Should().Be(a.RemainingMilliwattHours);
        b.FullChargeMilliwattHours.Should().Be(a.FullChargeMilliwattHours);
    }

    [WindowsFact]
    public void InvalidateCache_ForcesFreshWinRtRead()
    {
        var monitor = new BatteryMonitor();
        var a = monitor.Read();
        monitor.InvalidateCache();
        var b = monitor.Read();

        // We can't assert values changed (battery state is stable in a test),
        // but we CAN assert the call didn't throw — and that the post-invalidate
        // state is still consistent.
        b.Percent.Should().BeInRange(0, 100);
    }
}
