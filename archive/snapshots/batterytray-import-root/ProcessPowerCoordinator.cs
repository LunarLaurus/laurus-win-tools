namespace BatteryTray;

public sealed class ProcessPowerCoordinator : IDisposable
{
    private readonly List<IProcessPowerSampler> _samplers;
    private readonly object _gate = new();

    /// <summary>
    /// Production constructor — wires up the three real samplers, with stub
    /// entries for tiers that aren't available so the diagnostics tab can
    /// show *why* a tier isn't running.
    /// </summary>
    public ProcessPowerCoordinator() : this(BuildDefaultSamplers())
    {
    }

    /// <summary>
    /// Test constructor — accepts an explicit list of samplers so tests can
    /// substitute fakes.
    /// </summary>
    internal ProcessPowerCoordinator(IEnumerable<IProcessPowerSampler> samplers)
    {
        _samplers = samplers.ToList();
    }

    private static IEnumerable<IProcessPowerSampler> BuildDefaultSamplers()
    {
        var result = new List<IProcessPowerSampler>
        {
            new PerformanceCounterPowerSampler(),
        };

        if (OperatingSystem.IsWindowsVersionAtLeast(7))
        {
            try
            {
                if (EnergyMeterPowerSampler.IsAvailable())
                    result.Add(new EnergyMeterPowerSampler());
                else
                    result.Add(new UnavailableSampler(
                        PowerSamplerSource.EnergyMeterWmi,
                        "Hardware EnergyMeter (WMI) not present on this system. " +
                        "This typically means your hardware doesn't expose the EMI " +
                        "ACPI tables — common on most desktops and older laptops."));
            }
            catch (Exception ex)
            {
                CrashLogger.Write("EnergyMeter probe", ex);
                result.Add(new UnavailableSampler(
                    PowerSamplerSource.EnergyMeterWmi,
                    $"Probe threw {ex.GetType().Name}: {ex.Message}"));
            }

            try
            {
                if (EtwEnergyPowerSampler.IsAvailable())
                    result.Add(new EtwEnergyPowerSampler());
                else
                    result.Add(new UnavailableSampler(
                        PowerSamplerSource.EtwEnergyEstimation,
                        "ETW kernel session creation requires administrator rights. " +
                        "Enable \"Run at Windows startup (elevated)\" in Settings to grant " +
                        "BatteryTray persistent admin access for this provider."));
            }
            catch (Exception ex)
            {
                CrashLogger.Write("EtwEnergy probe", ex);
                result.Add(new UnavailableSampler(
                    PowerSamplerSource.EtwEnergyEstimation,
                    $"Probe threw {ex.GetType().Name}: {ex.Message}"));
            }
        }

        return result;
    }

    public (PowerSamplerSource Source, string Status, ProcessPowerSample[] Samples) GetCurrent()
    {
        lock (_gate)
        {
            foreach (var s in _samplers.OrderByDescending(s => (int)s.Source))
            {
                if (s.IsHealthy && s.HasFirstSample)
                    return (s.Source, s.StatusMessage, s.GetLatest());
            }

            var tip = _samplers.OrderByDescending(s => (int)s.Source).FirstOrDefault();
            if (tip is not null)
                return (tip.Source, tip.StatusMessage, Array.Empty<ProcessPowerSample>());
        }

        return (PowerSamplerSource.PerformanceCounters, "No samplers available",
                Array.Empty<ProcessPowerSample>());
    }

    public IReadOnlyList<(PowerSamplerSource Source, bool Healthy, bool HasData, string Status)> GetSamplerStates()
    {
        lock (_gate)
        {
            return _samplers
                .Select(s => (s.Source, s.IsHealthy, s.HasFirstSample, s.StatusMessage))
                .OrderByDescending(t => (int)t.Source)
                .ToList();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var s in _samplers)
            {
                try { s.Dispose(); } catch (Exception ex) { CrashLogger.Write("Sampler.Dispose", ex); }
            }
            _samplers.Clear();
        }
    }

    /// <summary>Internal so tests can construct it directly.</summary>
    internal sealed class UnavailableSampler : IProcessPowerSampler
    {
        public UnavailableSampler(PowerSamplerSource source, string reason)
        {
            Source = source;
            StatusMessage = reason;
        }

        public PowerSamplerSource Source { get; }
        public bool HasFirstSample => false;
        public bool IsHealthy => false;
        public string StatusMessage { get; }
        public ProcessPowerSample[] GetLatest() => Array.Empty<ProcessPowerSample>();
        public void Dispose() { }
    }
}
