using System;

namespace WindowsAppCore;

/// <summary>
/// Wires AppDomain unhandled exception events to <see cref="CrashSink"/>.
/// Call Install() once at startup, before any other initialisation.
/// </summary>
public static class UnhandledExceptionWatcher
{
    public static void Install(AppLog log, string appName)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception
                ?? new Exception($"Non-exception object: {e.ExceptionObject}");
            log.Error("unhandled.exception", ex, new { isTerminating = e.IsTerminating });
            CrashSink.Write(appName, "AppDomain", ex);
        };
    }
}
