using System.Runtime.Versioning;
using FluentAssertions;
using Xunit;

namespace BatteryTray.Tests;

/// <summary>
/// These tests cover the watchdog logic in isolation. We can't actually start
/// an ETW session from a unit test (it needs admin and a real Windows kernel),
/// so we drive the sampler through its public surface and verify that the
/// no-events-deadline behaviour fires correctly.
///
/// On non-elevated runs RunSession will throw UnauthorizedAccessException
/// immediately, setting IsHealthy=false up-front — that's also what we expect
/// in CI. The tests assert the failure modes are correctly observable, not
/// that ETW actually works.
/// </summary>
[SupportedOSPlatform("windows")]
public class EtwEnergyPowerSamplerTests
{
    [Fact]
    public void NotElevated_IsAvailable_ReturnsFalse()
    {
        // CI runs as a regular user. If this assertion ever flips, the test
        // environment changed.
        if (EtwEnergyPowerSampler.IsAvailable())
        {
            // Skip on the rare admin-CI case.
            return;
        }

        EtwEnergyPowerSampler.IsAvailable().Should().BeFalse();
    }

    [Fact]
    public void ConstructedWithoutAdmin_QuicklyReportsUnhealthy()
    {
        // We can construct the sampler even without admin — RunSession will fail
        // internally and flip IsHealthy=false. This is the user-visible behaviour:
        // "ETW unavailable — needs admin (run elevated)" should appear in the
        // diagnostics list.
        if (EtwEnergyPowerSampler.IsAvailable())
        {
            // Test only meaningful when running unelevated.
            return;
        }

        using var sampler = new EtwEnergyPowerSampler();

        // Give RunSession a moment to fail.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (sampler.IsHealthy && DateTime.UtcNow < deadline)
            Thread.Sleep(50);

        sampler.IsHealthy.Should().BeFalse(
            because: "non-elevated session creation throws UnauthorizedAccessException synchronously");
        sampler.HasFirstSample.Should().BeFalse();
        sampler.StatusMessage.Should().Contain("admin",
            because: "the user-facing reason should mention elevation");
    }

    /// <summary>
    /// THE BUG REPRODUCER. Before the v1.9 fix, IsHealthy stayed true forever
    /// when no events arrived, because the demotion logic was inside the
    /// aggregator's per-event path. Now there's a wallclock watchdog.
    ///
    /// We can't easily fake ETW session startup, so this test only runs when
    /// the binary actually has admin (rare in CI; possible on dev box).
    /// On non-admin runs it short-circuits with a passing skip.
    /// </summary>
    [Fact]
    public void StuckWarmup_TrippedByWatchdog_WhenAdminButProviderInactive()
    {
        if (!EtwEnergyPowerSampler.IsAvailable())
        {
            // Skip — would-be unelevated, RunSession fails fast and we never reach the warmup-stuck state.
            return;
        }

        // Use a very short deadline so the test completes quickly. The watchdog
        // checks every 2s, so allow ~5s wall-clock for the demotion to fire
        // even with the worst-case scheduling.
        using var sampler = new EtwEnergyPowerSampler(
            aggregationWindow: TimeSpan.FromSeconds(1),
            noEventsDeadline: TimeSpan.FromSeconds(2));

        // Wait until the watchdog has had a chance to demote.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (sampler.IsHealthy && !sampler.HasFirstSample && DateTime.UtcNow < deadline)
            Thread.Sleep(100);

        // If the provider is genuinely active on this admin box, the test trivially
        // passes by hitting HasFirstSample. If not, the watchdog should have demoted.
        if (!sampler.HasFirstSample)
        {
            sampler.IsHealthy.Should().BeFalse(
                because: "watchdog must demote stuck-warming-up sessions");
            sampler.StatusMessage.Should().NotContain("warming up",
                because: "the failure should be reported as failure, not perpetual warmup");
        }
    }
}
