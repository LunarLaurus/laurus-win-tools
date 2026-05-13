namespace ProgramHider;

// Parses command-line switches used for startup launches and elevation retry
// handoff between Program Hider instances.
internal sealed class StartupOptions
{
    public bool IsStartupLaunch { get; init; }
    public int DelaySeconds { get; init; }
    public bool SafeMode { get; init; }
    public nint PendingHideHandle { get; init; }

    public static StartupOptions Parse(string[] args)
    {
        var options = new StartupOptions();
        var delaySeconds = 0;
        var isStartupLaunch = false;
        var safeMode = false;
        nint pendingHideHandle = 0;

        foreach (var arg in args)
        {
            if (string.Equals(arg, "--startup", StringComparison.OrdinalIgnoreCase))
            {
                isStartupLaunch = true;
                continue;
            }

            if (string.Equals(arg, "--safe-mode", StringComparison.OrdinalIgnoreCase))
            {
                safeMode = true;
                continue;
            }

            if (arg.StartsWith("--delay=", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(arg["--delay=".Length..], out var parsedDelay))
            {
                delaySeconds = Math.Clamp(parsedDelay, 0, 300);
                continue;
            }

            if (arg.StartsWith("--rehide=", StringComparison.OrdinalIgnoreCase) &&
                ElevationService.TryParseHandle(arg["--rehide=".Length..], out var parsedHandle))
            {
                pendingHideHandle = parsedHandle;
            }
        }

        return new StartupOptions
        {
            IsStartupLaunch = isStartupLaunch,
            DelaySeconds = delaySeconds,
            SafeMode = safeMode,
            PendingHideHandle = pendingHideHandle
        };
    }
}
