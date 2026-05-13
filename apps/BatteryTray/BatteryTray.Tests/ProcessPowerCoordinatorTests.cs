using FluentAssertions;
using Xunit;

namespace BatteryTray.Tests;

public class ProcessPowerCoordinatorTests
{
    [Fact]
    public void GetCurrent_PicksHighestTierThatHasData()
    {
        var t1 = new FakeSampler(PowerSamplerSource.PerformanceCounters, healthy: true, hasData: true,
                                 status: "perf counters live");
        var t2 = new FakeSampler(PowerSamplerSource.EnergyMeterWmi, healthy: true, hasData: true,
                                 status: "energy meter live");
        var t3 = new FakeSampler(PowerSamplerSource.EtwEnergyEstimation, healthy: true, hasData: true,
                                 status: "etw live");

        using var coord = new ProcessPowerCoordinator(new[] { (IProcessPowerSampler)t1, t2, t3 });

        var (source, status, _) = coord.GetCurrent();
        source.Should().Be(PowerSamplerSource.EtwEnergyEstimation);
        status.Should().Be("etw live");
    }

    [Fact]
    public void GetCurrent_FallsBackWhenHigherTierIsUnhealthy()
    {
        var t1 = new FakeSampler(PowerSamplerSource.PerformanceCounters, healthy: true, hasData: true);
        var t3 = new FakeSampler(PowerSamplerSource.EtwEnergyEstimation, healthy: false, hasData: false);

        using var coord = new ProcessPowerCoordinator(new[] { (IProcessPowerSampler)t1, t3 });

        var (source, _, _) = coord.GetCurrent();
        source.Should().Be(PowerSamplerSource.PerformanceCounters);
    }

    /// <summary>
    /// THE BUG REPRODUCER. Pre-fix, ETW would set IsHealthy=true at session start
    /// and never demote to false even if no events ever flowed. The coordinator
    /// would keep picking ETW as the active source despite it producing zero data,
    /// because the predicate was just `IsHealthy && HasFirstSample`.
    ///
    /// Behavioural fix: a sampler that's been "healthy but not producing" for
    /// longer than its warmup budget should self-demote IsHealthy to false,
    /// letting the coordinator promote a lower tier.
    /// </summary>
    [Fact]
    public void GetCurrent_PrefersLowerTierWithDataOverHigherTierWithout()
    {
        // ETW says it's healthy but has never produced a sample (the failure mode).
        var t3 = new FakeSampler(PowerSamplerSource.EtwEnergyEstimation, healthy: true, hasData: false,
                                 status: "etw warming up");
        // Tier 1 is healthy and has real data.
        var t1 = new FakeSampler(PowerSamplerSource.PerformanceCounters, healthy: true, hasData: true,
                                 status: "counters live");
        t1.SetSamples(new ProcessPowerSample(123, "chrome", 12.5, 0, 3.2));

        using var coord = new ProcessPowerCoordinator(new[] { (IProcessPowerSampler)t1, t3 });

        var (source, _, samples) = coord.GetCurrent();

        // Pre-fix this picks Tier 3 and returns empty samples — broken UX.
        // Post-fix it picks Tier 1 because Tier 3 has !HasFirstSample.
        source.Should().Be(PowerSamplerSource.PerformanceCounters,
            because: "ETW with no events should not be picked over a working lower tier");
        samples.Should().HaveCount(1);
    }

    [Fact]
    public void GetCurrent_NoneHealthy_StillReturnsHighestTierStatus()
    {
        // All tiers unhealthy — diagnostics should still surface the *highest* tier's
        // failure reason since that's the most useful signal for the user.
        var t1 = new FakeSampler(PowerSamplerSource.PerformanceCounters, healthy: false, hasData: false, status: "t1 dead");
        var t2 = new FakeSampler(PowerSamplerSource.EnergyMeterWmi,      healthy: false, hasData: false, status: "t2 dead");
        var t3 = new FakeSampler(PowerSamplerSource.EtwEnergyEstimation, healthy: false, hasData: false, status: "t3 dead");

        using var coord = new ProcessPowerCoordinator(new[] { (IProcessPowerSampler)t1, t2, t3 });

        var (source, status, samples) = coord.GetCurrent();
        source.Should().Be(PowerSamplerSource.EtwEnergyEstimation);
        status.Should().Be("t3 dead");
        samples.Should().BeEmpty();
    }

    [Fact]
    public void GetSamplerStates_ListsAllSamplersHighTierFirst()
    {
        var t1 = new FakeSampler(PowerSamplerSource.PerformanceCounters, healthy: true, hasData: true);
        var t2 = new FakeSampler(PowerSamplerSource.EnergyMeterWmi,      healthy: false, hasData: false);
        var t3 = new FakeSampler(PowerSamplerSource.EtwEnergyEstimation, healthy: true, hasData: false);

        using var coord = new ProcessPowerCoordinator(new[] { (IProcessPowerSampler)t1, t2, t3 });

        var states = coord.GetSamplerStates();

        states.Select(s => s.Source).Should().BeInDescendingOrder();
        states[0].Source.Should().Be(PowerSamplerSource.EtwEnergyEstimation);
        states[1].Source.Should().Be(PowerSamplerSource.EnergyMeterWmi);
        states[2].Source.Should().Be(PowerSamplerSource.PerformanceCounters);
    }

    [Fact]
    public void Dispose_DisposesAllSamplers()
    {
        var t1 = new FakeSampler(PowerSamplerSource.PerformanceCounters);
        var t2 = new FakeSampler(PowerSamplerSource.EnergyMeterWmi);
        var t3 = new FakeSampler(PowerSamplerSource.EtwEnergyEstimation);

        var coord = new ProcessPowerCoordinator(new[] { (IProcessPowerSampler)t1, t2, t3 });
        coord.Dispose();

        t1.DisposeCalled.Should().BeTrue();
        t2.DisposeCalled.Should().BeTrue();
        t3.DisposeCalled.Should().BeTrue();
    }

    [Fact]
    public void UnavailableSampler_NeverPickedAsActive()
    {
        // Simulating the v1.7 case: ETW probe failed, Unavailable stub registered.
        var t1 = new FakeSampler(PowerSamplerSource.PerformanceCounters, healthy: true, hasData: true);
        var unavailableEtw = new ProcessPowerCoordinator.UnavailableSampler(
            PowerSamplerSource.EtwEnergyEstimation, "needs admin");

        using var coord = new ProcessPowerCoordinator(new IProcessPowerSampler[] { t1, unavailableEtw });

        var (source, _, _) = coord.GetCurrent();
        source.Should().Be(PowerSamplerSource.PerformanceCounters);
    }
}
