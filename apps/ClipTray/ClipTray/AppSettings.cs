using System.Text.Json.Serialization;
using System.Windows.Forms;
using WindowsAppCore;
using WindowsTrayCore;

namespace ClipTray;

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public const int CurrentSchemaVersion = 1;

    // General
    public HotkeyModifiers PickerHotkeyModifiers { get; set; } = HotkeyModifiers.Control | HotkeyModifiers.Shift;
    public Keys PickerHotkeyKey { get; set; } = Keys.V;
    public int TextHistoryCap { get; set; } = 50;
    public int ImageHistoryCap { get; set; } = 10;
    public int DiskQuotaMb { get; set; } = 100;

    // Privacy
    public HotkeyModifiers PauseHotkeyModifiers { get; set; } = HotkeyModifiers.None;
    public Keys PauseHotkeyKey { get; set; } = Keys.None;
    public bool PauseCapture { get; set; }
    public bool PauseOnLockScreen { get; set; } = true;
    public bool PasswordHeuristicEnabled { get; set; } = true;
    public int PasswordHeuristicMinLength { get; set; } = 8;
    public int PasswordHeuristicMaxLength { get; set; } = 64;
    public List<string> ForegroundBlocklist { get; set; } = new()
    {
        "keepass", "keepass2", "keepassxc",
        "1password", "bitwarden", "lastpass", "dashlane",
    };

    // System
    public bool RunAtStartup { get; set; }
    public int StartupDelaySeconds { get; set; }
    public bool ShownFirstRunWelcome { get; set; }

    // Picker geometry
    public int PickerWidth { get; set; } = 400;
    public int PickerHeight { get; set; } = 360;

    private static readonly JsonSettingsStore<AppSettings> Store = new(
        "ClipTray",
        migrations: Array.Empty<ISettingsMigration>());

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
