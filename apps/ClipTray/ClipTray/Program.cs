using System.Diagnostics;
using WindowsAppCore;

namespace ClipTray;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (!SingleInstanceActivation.TryClaim("ClipTray", dispatchToUi: null, out var activation))
            return 0;

        using var log = new AppLog("ClipTray", Application.ProductVersion);
        UnhandledExceptionWatcher.Install(log, "ClipTray");

        var settings = AppSettings.Load();

        var startupOptions = StartupOptions.Parse(args);
        int delaySeconds = startupOptions.DelaySeconds > 0
            ? startupOptions.DelaySeconds
            : settings.StartupDelaySeconds;
        if (delaySeconds > 0)
            Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));

        ApplicationConfiguration.Initialize();
        using (activation!)
        {
            // ClipTrayContext lands in Task 14; placeholder ApplicationContext
            // keeps the project buildable through the foundation phases.
            Application.Run(new ApplicationContext());
        }

        return 0;
    }
}
