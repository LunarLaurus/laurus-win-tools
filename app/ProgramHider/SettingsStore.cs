using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProgramHider;

internal sealed class SettingsStore
{
    private const string SettingsPathOverrideEnvironmentVariable = "PROGRAM_HIDER_SETTINGS_PATH";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public SettingsStore()
    {
        var overriddenPath = Environment.GetEnvironmentVariable(SettingsPathOverrideEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overriddenPath))
        {
            SettingsPath = Path.GetFullPath(overriddenPath);
            return;
        }

        var appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        SettingsPath = Path.Combine(appDataDirectory, "ProgramHider", "settings.json");
    }

    public string SettingsPath { get; }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return CreateDefaultSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? CreateDefaultSettings();
            settings.Normalize();
            return settings;
        }
        catch
        {
            return CreateDefaultSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        settings.Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    private static AppSettings CreateDefaultSettings()
    {
        var settings = new AppSettings();
        settings.Normalize();
        return settings;
    }
}
