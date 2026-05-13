namespace NetProfileSwitcher.Models;

public class AppConfig
{
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public const int CurrentSchemaVersion = 1;

    public List<NetworkProfile> Profiles { get; set; } = new();
    public string SelectedAdapter { get; set; } = "Wi-Fi";
    public bool MonitorEnabled { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool StartMinimized { get; set; } = false;
    public bool RunOnStartup { get; set; } = false;
    public int StartupDelaySeconds { get; set; } = 0;
    public bool ShownFirstRunWelcome { get; set; }
}
