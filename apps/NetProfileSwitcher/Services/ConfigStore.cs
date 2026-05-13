using System.Text.Json;
using NetProfileSwitcher.Models;
using WindowsAppCore;

namespace NetProfileSwitcher.Services;

public static class ConfigStore
{
    private static readonly JsonSettingsStore<AppConfig> Store = new(
        "NetProfileSwitcher",
        normalize: cfg => { cfg.Profiles ??= new(); return cfg; },
        options: new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    public static AppConfig Load()
    {
        // One-time migration: old file was "config.json"; new convention is "settings.json"
        MigrateConfigFileName();
        var cfg = Store.Load();
        if (cfg.Profiles.Count == 0)
            cfg.Profiles.AddRange(DefaultProfiles());
        return cfg;
    }

    private static void MigrateConfigFileName()
    {
        var dir = AppPaths.SettingsDir("NetProfileSwitcher");
        var oldPath = Path.Combine(dir, "config.json");
        var newPath = Path.Combine(dir, "settings.json");
        if (!File.Exists(newPath) && File.Exists(oldPath))
            try { File.Move(oldPath, newPath); } catch { }
    }

    public static bool Save(AppConfig cfg)
    {
        try { Store.Save(cfg); return true; }
        catch { return false; }
    }

    private static IEnumerable<NetworkProfile> DefaultProfiles() =>
    [
        new() {
            Name = "Home", UseDhcp = false,
            Ip = "192.168.1.100", Subnet = "255.255.255.0", Gateway = "192.168.1.1",
            Dns1 = "1.1.1.1", Dns2 = "1.0.0.1",
        },
        new() {
            Name = "Work", UseDhcp = false,
            Ip = "10.0.0.50", Subnet = "255.255.255.0", Gateway = "10.0.0.1",
            Dns1 = "10.0.0.2", Dns2 = "8.8.8.8",
        },
        new() {
            Name = "Travel (DHCP)", UseDhcp = true,
            Dns1 = "8.8.8.8", Dns2 = "8.8.4.4",
        },
    ];
}
