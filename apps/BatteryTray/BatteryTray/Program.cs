using System.Diagnostics;
using WindowsAppCore;

namespace BatteryTray;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        CrashLogger.Install();

        var startup = new ScheduledTaskStartupRegistration(
            "BatteryTray",
            Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty,
            "BatteryTray — auto-start configurable battery indicator");

        if (startup.TryHandleHelperArgs(args) is int code)
            return code;

        if (!SingleInstanceActivation.TryClaim("BatteryTray", dispatchToUi: null, out var activation))
            return 0;

        using var log = new AppLog("BatteryTray", Application.ProductVersion);
        UnhandledExceptionWatcher.Install(log, "BatteryTray");

        var settings = AppSettings.Load();

        var startupOptions = StartupOptions.Parse(args);
        int delaySeconds = startupOptions.DelaySeconds > 0
            ? startupOptions.DelaySeconds
            : settings.StartupDelaySeconds;
        if (delaySeconds > 0)
            Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));

        ApplicationConfiguration.Initialize();
        using (activation!)
        using (var context = new BatteryTrayContext(settings, activation!, startup))
        {
            Application.Run(context);
        }

        return 0;
    }
}
