namespace BatteryTray;

internal static class Program
{
    // The "Local\" prefix scopes these objects to the current logon session, so two
    // users on the same machine each get their own instance. The {GUID} suffix makes
    // collision with random other apps astronomically unlikely.
    private const string MutexName  = @"Local\BatteryTray.SingleInstance.{F1A4D2E8-9C7B-4E5A-8F6D-2B3C1A4D5E6F}";
    private const string SignalName = @"Local\BatteryTray.Activate.{F1A4D2E8-9C7B-4E5A-8F6D-2B3C1A4D5E6F}";

    [STAThread]
    private static int Main(string[] args)
    {
        CrashLogger.Install();

        // Elevated-helper fast paths from v1.2 — these don't need single-instance.
        if (args.Length > 0)
        {
            switch (args[0])
            {
                case "--install-task":
                    return StartupManager.RunInstallAction(args.Length > 1 ? args[1] : null) ? 0 : 1;
                case "--uninstall-task":
                    return StartupManager.RunUninstallAction() ? 0 : 1;
                default:
                    return 2;
            }
        }

        using var mutex = CrossIntegrityMutex.CreateOrOpen(MutexName);
        using var signal = ActivationSignal.CreateOrOpen(SignalName);

        if (!mutex.CreatedNew)
        {
            // Another instance is already running. Wake it up so the user sees something
            // happen — silent no-op was confusing in v1.0.
            signal.TrySignal();
            return 0;
        }

        ApplicationConfiguration.Initialize();

        var settings = AppSettings.Load();
        using var context = new BatteryTrayContext(settings, signal);
        Application.Run(context);

        return 0;
    }
}
