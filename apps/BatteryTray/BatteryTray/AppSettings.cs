using System.Text.Json.Serialization;
using WindowsAppCore;

namespace BatteryTray;

public enum IconStyle { Numeric = 0, Bar = 1, Both = 2 }
public enum IconTheme { Auto = 0, Light = 1, Dark = 2 }

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public const int CurrentSchemaVersion = 3;

    // ---- Polling / thresholds ----
    public int UpdateIntervalSeconds { get; set; } = 30;
    public int LowBatteryThreshold { get; set; } = 20;
    public int CriticalBatteryThreshold { get; set; } = 10;

    // ---- Notifications ----
    public bool NotifyOnLow { get; set; } = true;
    public bool NotifyOnCritical { get; set; } = true;
    public bool NotifyOnFullyCharged { get; set; } = true;
    public bool NotifyOnChargeLimitReached { get; set; } = true;
    public int  ChargeLimitMinutesAtFull { get; set; } = 120;

    /// <summary>
    /// (v3) When Battery Saver is on, suppress the "low battery" notification —
    /// the user clearly already knows they're low on charge. Critical alerts
    /// still fire so we don't lose safety value.
    /// </summary>
    public bool SuppressLowAlertWhenBatterySaverActive { get; set; } = true;

    // ---- Appearance ----
    public IconStyle Style { get; set; } = IconStyle.Numeric;
    public IconTheme Theme { get; set; } = IconTheme.Auto;
    public bool SmoothColorTransitions { get; set; } = true;
    public bool ShowTimeRemainingInTooltip { get; set; } = true;
    public bool DpiAwareIcon { get; set; } = true;

    /// <summary>
    /// (v3) When Battery Saver is active, show a small leaf glyph overlaying the
    /// icon so the user can tell at a glance the OS is in low-power mode.
    /// </summary>
    public bool ShowBatterySaverIndicator { get; set; } = true;

    // ---- System ----
    public bool RunAtStartup { get; set; }
    public bool ShownFirstRunWelcome { get; set; }

    // ---- Power plan auto-switch ----
    public bool   PowerPlanAutoSwitchEnabled { get; set; }
    public string? PowerPlanOnAcGuid { get; set; }
    public string? PowerPlanOnBatteryGuid { get; set; }
    // NOTE: AutoEnableBatterySaver and BatterySaverThreshold removed in v3.
    // Microsoft doesn't expose a way to programmatically enable Battery Saver from
    // user-mode (deliberately — it's user-controlled by design), so the v2 fields
    // never did anything. v3 migration drops them silently.

    // ---- Colors ----
    public string ColorCharging { get; set; } = "#1E88E5";
    public string ColorNormal   { get; set; } = "#43A047";
    public string ColorLow      { get; set; } = "#FB8C00";
    public string ColorCritical { get; set; } = "#E53935";
    public string ColorText     { get; set; } = "#FFFFFF";

    private static readonly JsonSettingsStore<AppSettings> Store = new(
        "BatteryTray",
        migrations: new ISettingsMigration[]
        {
            new AppSettingsMigrationV1ToV2(),
            new AppSettingsMigrationV2ToV3(),
        });

    [JsonIgnore]
    public string SettingsFilePath => Store.SettingsPath;

    public static AppSettings Load()
    {
        var s = Store.Load();
        s.SchemaVersion = CurrentSchemaVersion;
        return s;
    }

    public void Save()
    {
        SchemaVersion = CurrentSchemaVersion;
        Store.Save(this);
    }
}
