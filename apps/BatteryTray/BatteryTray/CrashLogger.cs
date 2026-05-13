using System.IO;

namespace BatteryTray;

/// <summary>
/// Captures unhandled exceptions from both the WinForms message pump and
/// the AppDomain background thread pool, writing them to a rolling log
/// in %TEMP%. Without this, any unhandled crash in a tray app vanishes
/// silently — there's no console to print to.
/// </summary>
internal static class CrashLogger
{
    private static readonly string LogPath =
        Path.Combine(Path.GetTempPath(), "BatteryTray-crash.log");

    private const long MaxLogBytes = 256 * 1024;  // Trim aggressively — these are diagnostics, not history.

    public static void Install()
    {
        // ThreadException catches exceptions on the UI thread when running under
        // the WinForms message pump. Setting UnhandledExceptionMode to CatchException
        // is required for this to work reliably in release builds.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Write("UI thread", e.Exception);

        // AppDomain.UnhandledException is wired by WindowsAppCore.UnhandledExceptionWatcher (see Program.cs).

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Write("Unobserved task", e.Exception);
            e.SetObserved();
        };
    }

    public static void Write(string source, Exception ex)
    {
        try
        {
            // Naive size-based rotation: when the file gets too big, truncate.
            // Good enough for a single-user diagnostic log.
            if (File.Exists(LogPath))
            {
                var info = new FileInfo(LogPath);
                if (info.Length > MaxLogBytes) File.Delete(LogPath);
            }

            File.AppendAllText(LogPath,
                $"[{DateTime.Now:O}] [{source}]{Environment.NewLine}" +
                $"{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Logging itself must never throw out of this method.
        }
    }

    public static string GetLogPath() => LogPath;
}
