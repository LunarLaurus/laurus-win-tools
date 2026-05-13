using System;
using System.IO;

namespace WindowsAppCore;

public static class AppPaths
{
    /// <summary>
    /// %APPDATA%\{appName}\ — or env override root when {APPNAME}_DATA is set.
    /// </summary>
    public static string SettingsDir(string appName)
    {
        var root = EnvOverride(appName);
        return root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            appName);
    }

    /// <summary>
    /// %LOCALAPPDATA%\{appName}\logs\ — or {override}\logs\ when env override is set.
    /// </summary>
    public static string LogDir(string appName)
    {
        var root = EnvOverride(appName);
        return root != null
            ? Path.Combine(root, "logs")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                appName, "logs");
    }

    /// <summary>
    /// %LOCALAPPDATA%\{appName}\history\ — or {override}\history\ when env override is set.
    /// </summary>
    public static string HistoryDir(string appName)
    {
        var root = EnvOverride(appName);
        return root != null
            ? Path.Combine(root, "history")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                appName, "history");
    }

    /// <summary>
    /// %TEMP%\{appName}-crash.log — always in system temp, never overridden.
    /// </summary>
    public static string CrashLogPath(string appName) =>
        Path.Combine(Path.GetTempPath(), $"{appName}-crash.log");

    // Key: {APPNAME_UPPER_UNDERSCORED}_DATA  e.g. "my-app" → "MY_APP_DATA"
    private static string? EnvOverride(string appName)
    {
        var key = appName.ToUpperInvariant().Replace('-', '_').Replace(' ', '_') + "_DATA";
        return Environment.GetEnvironmentVariable(key);
    }
}
