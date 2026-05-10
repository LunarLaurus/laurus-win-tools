using Microsoft.Win32;

namespace ProgramHider;

internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "ProgramHider";

    internal static void Apply(bool enabled, int startupDelaySeconds)
    {
        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
        if (runKey is null)
        {
            return;
        }

        if (enabled)
        {
            var arguments = $"--startup --delay={Math.Clamp(startupDelaySeconds, 0, 300)}";
            runKey.SetValue(RunValueName, $"\"{Application.ExecutablePath}\" {arguments}");
        }
        else if (runKey.GetValue(RunValueName) is not null)
        {
            runKey.DeleteValue(RunValueName, false);
        }
    }
}
