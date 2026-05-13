using Microsoft.Win32;

namespace SoundTracker.App;

internal static class AppTheme
{
    private const string PersonalizePath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static bool IsLightTaskbarTheme()
    {
        try
        {
            using var personalizeKey = Registry.CurrentUser.OpenSubKey(PersonalizePath, writable: false);
            var systemThemeValue = personalizeKey?.GetValue("SystemUsesLightTheme");
            if (systemThemeValue is int systemTheme)
            {
                return systemTheme != 0;
            }

            var appsThemeValue = personalizeKey?.GetValue("AppsUseLightTheme");
            if (appsThemeValue is int appsTheme)
            {
                return appsTheme != 0;
            }
        }
        catch
        {
        }

        return true;
    }
}
