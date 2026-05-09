using System.Text.Json;
using NetProfileSwitcher.Models;

namespace NetProfileSwitcher.Services;

public static class ConfigStore
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "NetProfileSwitcher");
    private static readonly string FilePath = Path.Combine(Dir, "config.json");

    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath), Opts)
                       ?? DefaultConfig();
        }
        catch { }
        return DefaultConfig();
    }

    public static void Save(AppConfig cfg)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(cfg, Opts));
    }

    private static AppConfig DefaultConfig() => new()
    {
        Profiles = new List<NetworkProfile>
        {
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
        },
    };
}
