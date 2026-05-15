namespace BatteryTray;

/// <summary>
/// User-configured policy for hardware power events. Persisted into AppSettings.
///
/// When <see cref="DifferOnBattery"/> is false, the two "OnBattery" fields are
/// ignored at apply time and the AC values are written to both AC and DC
/// powercfg indices. When true, the OnBattery fields populate the DC indices.
/// </summary>
public sealed class HardwareActionPolicy
{
    public HardwareAction PowerButton { get; set; } = HardwareAction.Sleep;
    public HardwareAction LidClose    { get; set; } = HardwareAction.Sleep;

    public bool DifferOnBattery { get; set; }
    public HardwareAction PowerButtonOnBattery { get; set; } = HardwareAction.Sleep;
    public HardwareAction LidCloseOnBattery    { get; set; } = HardwareAction.Hibernate;
}
