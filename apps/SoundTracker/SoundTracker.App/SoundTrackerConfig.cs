using System.Text.Json.Serialization;
using WindowsAppCore;

namespace SoundTracker.App;

public sealed class SoundTrackerConfig
{
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public const int CurrentSchemaVersion = 1;

    public bool RunAtStartup { get; set; }
    public int StartupDelaySeconds { get; set; } = 0;
    public bool ShownFirstRunWelcome { get; set; }

    private static readonly JsonSettingsStore<SoundTrackerConfig> Store = new("SoundTracker");

    [JsonIgnore]
    public string SettingsFilePath => Store.SettingsPath;

    public static SoundTrackerConfig Load()
    {
        var c = Store.Load();
        c.SchemaVersion = CurrentSchemaVersion;
        return c;
    }

    public void Save() => Store.Save(this);
}
