using Microsoft.Win32;

namespace WindowsAppCore;

/// <summary>
/// Registers an app in HKCU\...\Run so it launches at sign-in without elevation.
/// Suitable for user-mode apps (ProgramHider, SoundTracker) that do not require
/// elevated startup.
/// </summary>
public sealed class RunKeyStartupRegistration : IStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _valueName;
    private readonly string _commandLine;

    public RunKeyStartupRegistration(string valueName, string exePath, string arguments = "")
    {
        _valueName = valueName;
        _commandLine = string.IsNullOrEmpty(arguments)
            ? $"\"{exePath}\""
            : $"\"{exePath}\" {arguments}";
    }

    public bool IsRegistered
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                return key?.GetValue(_valueName) is not null;
            }
            catch { return false; }
        }
    }

    public StartupRegistrationResult Register()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            key?.SetValue(_valueName, _commandLine);
            return StartupRegistrationResult.Success;
        }
        catch { return StartupRegistrationResult.Failed; }
    }

    public StartupRegistrationResult Unregister()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(_valueName) is not null)
                key.DeleteValue(_valueName, throwOnMissingValue: false);
            return StartupRegistrationResult.Success;
        }
        catch { return StartupRegistrationResult.Failed; }
    }
}
