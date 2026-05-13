namespace BatteryTray;

public enum PowerSamplerSource
{
    /// <summary>Per-process performance counters; works on every Windows since 7.</summary>
    PerformanceCounters = 1,

    /// <summary>EnergyMeter WMI class; available on hardware with the right ACPI tables.</summary>
    EnergyMeterWmi = 2,

    /// <summary>ETW EnergyEstimation provider; what Task Manager uses internally. Best signal.</summary>
    EtwEnergyEstimation = 3,
}

public readonly record struct ProcessPowerSample(
    int ProcessId,
    string ProcessName,
    double CpuPercent,
    double IoBytesPerSec,
    double EstimatedWatts);

public interface IProcessPowerSampler : IDisposable
{
    /// <summary>The data source feeding this sampler.</summary>
    PowerSamplerSource Source { get; }

    /// <summary>True if at least one full sample has been collected.</summary>
    bool HasFirstSample { get; }

    /// <summary>True if this sampler is producing data on this system. False if e.g. the underlying provider isn't available.</summary>
    bool IsHealthy { get; }

    /// <summary>One-line description of why this source did or didn't initialize. Surfaced in the UI.</summary>
    string StatusMessage { get; }

    /// <summary>Most recent sample sorted high-to-low by EstimatedWatts. Empty array if no sample yet.</summary>
    ProcessPowerSample[] GetLatest();
}
