namespace WindowsAppCore;

public sealed class StartupOptions
{
    public bool IsStartupLaunch { get; }
    public int DelaySeconds { get; }

    private StartupOptions(bool isStartupLaunch, int delaySeconds)
    {
        IsStartupLaunch = isStartupLaunch;
        DelaySeconds = delaySeconds;
    }

    public static StartupOptions Parse(string[] args)
    {
        bool isStartupLaunch = false;
        int delaySeconds = 0;

        foreach (var arg in args)
        {
            if (arg.Equals("--startup", StringComparison.OrdinalIgnoreCase))
            {
                isStartupLaunch = true;
            }
            else if (arg.StartsWith("--delay=", StringComparison.OrdinalIgnoreCase) &&
                     int.TryParse(arg["--delay=".Length..], out var d))
            {
                delaySeconds = Math.Clamp(d, 0, 300);
            }
        }

        return new StartupOptions(isStartupLaunch, delaySeconds);
    }
}
