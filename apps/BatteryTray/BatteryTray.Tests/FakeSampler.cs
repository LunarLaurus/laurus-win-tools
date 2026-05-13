namespace BatteryTray.Tests;

/// <summary>
/// Test double for IProcessPowerSampler. Lets tests script every transition:
/// promote/demote, has-data flips, status messages, etc.
/// </summary>
internal sealed class FakeSampler : IProcessPowerSampler
{
    public PowerSamplerSource Source { get; }

    public bool HasFirstSample { get; set; }
    public bool IsHealthy { get; set; }
    public string StatusMessage { get; set; }
    private ProcessPowerSample[] _samples = Array.Empty<ProcessPowerSample>();
    public bool DisposeCalled { get; private set; }

    public FakeSampler(PowerSamplerSource source, bool healthy = true, bool hasData = false, string status = "")
    {
        Source = source;
        IsHealthy = healthy;
        HasFirstSample = hasData;
        StatusMessage = status;
    }

    public void SetSamples(params ProcessPowerSample[] samples)
    {
        _samples = samples;
        HasFirstSample = samples.Length > 0;
    }

    public ProcessPowerSample[] GetLatest() => _samples;
    public void Dispose() { DisposeCalled = true; }
}
