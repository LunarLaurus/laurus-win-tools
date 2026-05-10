using Microsoft.Win32;

namespace ProgramHider;

internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "ProgramHider";

    internal static void Apply(bool enabled)
    {
        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
        if (runKey is null)
        {
            return;
        }

        if (enabled)
        {
            runKey.SetValue(RunValueName, $"\"{Application.ExecutablePath}\"");
        }
        else if (runKey.GetValue(RunValueName) is not null)
        {
            runKey.DeleteValue(RunValueName, false);
        }
    }
}
