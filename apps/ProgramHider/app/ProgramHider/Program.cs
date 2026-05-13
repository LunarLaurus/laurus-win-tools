using System.Text.Json.Serialization;
using WindowsAppCore;

namespace ProgramHider;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (!SingleInstanceActivation.TryClaim("ProgramHider", dispatchToUi: null, out var activation))
            return;

        using var log = new AppLog("ProgramHider", Application.ProductVersion);
        UnhandledExceptionWatcher.Install(log, "ProgramHider");

        // Load settings to pick up user-configured startup delay as a fallback.
        var settingsStore = new JsonSettingsStore<AppSettings>(
            "ProgramHider",
            normalize: s => { s.Normalize(); return s; },
            options: new System.Text.Json.JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            });
        var settings = settingsStore.Load();

        var startupOptions = StartupOptions.Parse(args);
        int delaySeconds = startupOptions.DelaySeconds > 0
            ? startupOptions.DelaySeconds
            : settings.StartupDelaySeconds;
        if (delaySeconds > 0)
            Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));

        ApplicationConfiguration.Initialize();
        using (activation!)
            Application.Run(new ProgramHiderContext(startupOptions, activation!));
    }
}
