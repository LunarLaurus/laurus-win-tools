using FluentAssertions;
using Xunit;

namespace BatteryTray.E2ETests;

public class ProcessPowerCoordinatorE2ETests
{
    [WindowsFact]
    public void DefaultCtor_AlwaysReportsAllThreeTiers()
    {
        using var coord = new ProcessPowerCoordinator();
        var states = coord.GetSamplerStates();

        states.Should().HaveCount(3,
            because: "diagnostics view must always show three tiers — even unavailable ones");

        states.Select(s => s.Source).Should().Contain(new[]
        {
            PowerSamplerSource.PerformanceCounters,
            PowerSamplerSource.EnergyMeterWmi,
            PowerSamplerSource.EtwEnergyEstimation,
        });
    }

    [WindowsFact]
    public void DefaultCtor_Tier1_AlwaysHealthy()
    {
        using var coord = new ProcessPowerCoordinator();
        var states = coord.GetSamplerStates();
        var t1 = states.Single(s => s.Source == PowerSamplerSource.PerformanceCounters);

        t1.Healthy.Should().BeTrue(because: "perf counters work on every Windows since 7");
    }

    [WindowsFact]
    public async Task Tier1_ProducesData_WithinReasonableTime()
    {
        // Tier 1 samples in 2s windows; first sample available after ~2s.
        using var coord = new ProcessPowerCoordinator();

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var states = coord.GetSamplerStates();
            var t1 = states.Single(s => s.Source == PowerSamplerSource.PerformanceCounters);
            if (t1.HasData) return;
            await Task.Delay(500);
        }

        Assert.Fail("Tier 1 didn't produce a sample within 15 seconds — sampler thread isn't running.");
    }
}
