using System;
using System.IO;

namespace WindowsAppCore;

/// <summary>
/// Synchronous, direct-write crash log. Used for fatal paths — never routes through
/// the buffered <see cref="AppLog"/> channel, which may not flush before the process dies.
/// </summary>
public static class CrashSink
{
    private const long MaxLogBytes = 256 * 1024;

    public static string GetLogPath(string appName) =>
        Path.Combine(Path.GetTempPath(), $"{appName}-crash.log");

    public static void Write(string appName, string source, Exception ex)
    {
        try
        {
            var path = GetLogPath(appName);

            if (File.Exists(path) && new FileInfo(path).Length > MaxLogBytes)
                File.Delete(path);

            File.AppendAllText(path,
                $"[{DateTime.Now:O}] [{source}]{Environment.NewLine}" +
                $"{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Must not throw — crash logging cannot itself crash.
        }
    }
}
