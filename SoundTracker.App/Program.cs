using SoundTracker.App.Diagnostics;

namespace SoundTracker.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        AppLog.Info($"application starting version={AppMetadata.DisplayVersion} log={AppLog.LogPath}");
        Application.ThreadException += (_, args) => AppLog.Error("ui thread exception", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error("unhandled exception", args.ExceptionObject as Exception);

        try
        {
            ApplicationConfiguration.Initialize();
            AppLog.Info("winforms initialized");
            Application.Run(new TrayApplicationContext());
            AppLog.Info("application exited cleanly");
        }
        catch (Exception ex)
        {
            AppLog.Error("application terminated with exception", ex);
            throw;
        }
    }
}
