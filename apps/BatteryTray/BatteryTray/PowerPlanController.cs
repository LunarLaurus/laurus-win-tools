using System.Diagnostics;
using System.Text.RegularExpressions;

namespace BatteryTray;

public readonly record struct PowerPlan(Guid Guid, string Name, bool IsActive);

/// <summary>
/// Wraps powercfg.exe for power-plan listing/switching. We shell out rather than
/// use PowrProf P/Invoke because powercfg gives us localized plan names for free
/// and is stable across Windows builds.
/// </summary>
public static class PowerPlanController
{
    public static IReadOnlyList<PowerPlan> List()
    {
        var output = RunPowerCfg("/list");
        if (output is null) return Array.Empty<PowerPlan>();

        // Lines look like:
        //   Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced) *
        // Trailing asterisk marks the active scheme.
        var rx = new Regex(
            @"GUID:\s+([0-9a-fA-F-]{36})\s+\(([^)]+)\)(\s*\*)?",
            RegexOptions.Compiled);

        var list = new List<PowerPlan>();
        foreach (Match m in rx.Matches(output))
        {
            if (Guid.TryParse(m.Groups[1].Value, out var g))
            {
                list.Add(new PowerPlan(g, m.Groups[2].Value.Trim(), m.Groups[3].Success));
            }
        }
        return list;
    }

    public static Guid? GetActive()
    {
        var output = RunPowerCfg("/getactivescheme");
        if (output is null) return null;
        var m = Regex.Match(output, "GUID:\\s+([0-9a-fA-F-]{36})");
        return m.Success && Guid.TryParse(m.Groups[1].Value, out var g) ? g : null;
    }

    public static bool SetActive(Guid planGuid)
    {
        var result = RunPowerCfg($"/setactive {planGuid}");
        return result is not null;
    }

    private static string? RunPowerCfg(string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (p is null) return null;
            var stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            return p.ExitCode == 0 ? stdout : null;
        }
        catch (Exception ex)
        {
            CrashLogger.Write("powercfg", ex);
            return null;
        }
    }
}
