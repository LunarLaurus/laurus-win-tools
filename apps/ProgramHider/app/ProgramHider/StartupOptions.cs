namespace ProgramHider;

using CoreStartupOptions = WindowsAppCore.StartupOptions;

// Extends the common --startup / --delay= flags with ProgramHider-specific
// switches used for elevation retry handoff between instances.
internal sealed class StartupOptions
{
    public bool IsStartupLaunch { get; init; }
    public int DelaySeconds { get; init; }
    public bool SafeMode { get; init; }
    public nint PendingHideHandle { get; init; }

    public static StartupOptions Parse(string[] args)
    {
        var core = CoreStartupOptions.Parse(args);
        bool safeMode = false;
        nint pendingHideHandle = 0;

        foreach (var arg in args)
        {
            if (arg.Equals("--safe-mode", StringComparison.OrdinalIgnoreCase))
            {
                safeMode = true;
            }
            else if (arg.StartsWith("--rehide=", StringComparison.OrdinalIgnoreCase) &&
                     ElevationService.TryParseHandle(arg["--rehide=".Length..], out var h))
            {
                pendingHideHandle = h;
            }
        }

        return new StartupOptions
        {
            IsStartupLaunch = core.IsStartupLaunch,
            DelaySeconds = core.DelaySeconds,
            SafeMode = safeMode,
            PendingHideHandle = pendingHideHandle
        };
    }
}
