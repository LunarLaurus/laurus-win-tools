using SoundTracker.App.Diagnostics;
using SingleInstanceActivation = WindowsAppCore.SingleInstanceActivation;

namespace SoundTracker.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        if (!SingleInstanceActivation.TryClaim("SoundTracker", dispatchToUi: null, out var activation))
            return;

        AppLog.Info($"application starting version={AppMetadata.DisplayVersion} log={AppLog.LogPath}");
        var settings = SoundTrackerConfig.Load();
        AppLog.Info($"settings loaded path={settings.SettingsFilePath}");
        Application.ThreadException += (_, args) => AppLog.Error("ui thread exception", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error("unhandled exception", args.ExceptionObject as Exception);

        try
        {
            ApplicationConfiguration.Initialize();
            AppLog.Info("winforms initialized");
            using (activation!)
                Application.Run(new TrayApplicationContext(activation!));
            AppLog.Info("application exited cleanly");
        }
        catch (Exception ex)
        {
            AppLog.Error("application terminated with exception", ex);
            throw;
        }
    }
}
