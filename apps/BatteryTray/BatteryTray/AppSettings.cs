using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

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

    [JsonIgnore]
    public string SettingsFilePath => GetSettingsPath();

    private static string GetSettingsPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BatteryTray");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "settings.json");
    }

    public static AppSettings Load()
    {
        var path = GetSettingsPath();
        try
        {
            if (!File.Exists(path))
            {
                var fresh = new AppSettings();
                try { fresh.Save(); } catch { }
                return fresh;
            }

            var json = File.ReadAllText(path);
            int existingVersion = ReadSchemaVersion(json);
            var migratedJson = Migrate(json, existingVersion);

            var loaded = JsonSerializer.Deserialize<AppSettings>(migratedJson);
            if (loaded != null)
            {
                if (loaded.SchemaVersion != CurrentSchemaVersion)
                {
                    loaded.SchemaVersion = CurrentSchemaVersion;
                    try { loaded.Save(); } catch { }
                }
                return loaded;
            }
        }
        catch (Exception ex)
        {
            CrashLogger.Write("Settings.Load", ex);
        }

        try
        {
            if (File.Exists(path))
                File.Move(path, path + ".broken-" + DateTime.Now.ToString("yyyyMMddHHmmss"), overwrite: false);
        }
        catch { }

        var defaults = new AppSettings();
        try { defaults.Save(); } catch { }
        return defaults;
    }

    public void Save()
    {
        SchemaVersion = CurrentSchemaVersion;
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        var path = GetSettingsPath();
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }

    private static int ReadSchemaVersion(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            if (node?["SchemaVersion"] is JsonNode v) return (int?)v ?? 1;
        }
        catch { }
        return 1;
    }

    private static string Migrate(string json, int from)
    {
        if (from >= CurrentSchemaVersion) return json;

        try
        {
            var node = JsonNode.Parse(json) as JsonObject;
            if (node is null) return json;

            // v1 → v2: bump default polling interval if it was the old default of 5.
            if (from < 2)
            {
                if (node["UpdateIntervalSeconds"] is JsonNode interval && (int?)interval == 5)
                {
                    node["UpdateIntervalSeconds"] = 30;
                }
            }

            // v2 → v3: drop the dead Battery Saver fields. The deserializer would just
            // ignore unknown JSON keys against the v3 schema, but explicit removal keeps
            // the saved file clean and prevents confusion if someone hand-edits it.
            if (from < 3)
            {
                node.Remove("AutoEnableBatterySaver");
                node.Remove("BatterySaverThreshold");
            }

            return node.ToJsonString();
        }
        catch (Exception ex)
        {
            CrashLogger.Write("Settings.Migrate", ex);
            return json;
        }
    }
}
