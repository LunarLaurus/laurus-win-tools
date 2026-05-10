namespace ProgramHider;

internal sealed class StartupOptions
{
    public bool IsStartupLaunch { get; init; }
    public int DelaySeconds { get; init; }
    public bool SafeMode { get; init; }

    public static StartupOptions Parse(string[] args)
    {
        var options = new StartupOptions();
        var delaySeconds = 0;
        var isStartupLaunch = false;
        var safeMode = false;

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
            }
        }

        return new StartupOptions
        {
            IsStartupLaunch = isStartupLaunch,
            DelaySeconds = delaySeconds,
            SafeMode = safeMode
        };
    }
}
