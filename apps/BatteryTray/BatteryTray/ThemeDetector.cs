using Microsoft.Win32;

namespace BatteryTray;

internal static class ThemeDetector
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>
    /// Returns true if the *taskbar* is currently using the dark theme.
    /// We check SystemUsesLightTheme rather than AppsUseLightTheme — those are
    /// independent settings on Windows 10+, and the taskbar follows the system value.
    /// </summary>
    public static bool IsTaskbarDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            if (key?.GetValue("SystemUsesLightTheme") is int v) return v == 0;
        }
        catch { }
        // If we can't read the value, assume dark — that's the common case on
        // modern Windows installs and our default colors look fine on dark.
        return true;
    }
}
