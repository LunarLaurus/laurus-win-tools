using System.Diagnostics;

namespace SoundTracker.App.Processes;

internal sealed class ProcessNameResolver
{
    public string? TryGetProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            var name = process.ProcessName.Trim();
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? name
                : $"{name}.exe";
        }
        catch
        {
            return null;
        }
    }
}
