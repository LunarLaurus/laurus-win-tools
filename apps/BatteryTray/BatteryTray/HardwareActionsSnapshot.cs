namespace BatteryTray;

/// <summary>
/// Live read-back from powercfg of the current power-button and lid-close
/// actions on the active scheme. Used to populate the Settings dialog and
/// to detect drift between BatteryTray's persisted policy and Windows state.
/// </summary>
public readonly record struct HardwareActionsSnapshot(
    HardwareAction PowerButtonAc,
    HardwareAction PowerButtonDc,
    HardwareAction LidCloseAc,
    HardwareAction LidCloseDc);
