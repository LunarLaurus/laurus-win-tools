namespace BatteryTray;

/// <summary>
/// The five actions Windows supports for power-button and lid-close events.
/// Integer values match the indices powercfg uses for /setacvalueindex and
/// /setdcvalueindex, so the enum value can be cast to int and emitted directly.
/// </summary>
public enum HardwareAction
{
    DoNothing      = 0,
    Sleep          = 1,
    Hibernate      = 2,
    ShutDown       = 3,
    TurnOffDisplay = 4,
}
