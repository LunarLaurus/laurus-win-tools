namespace WindowsTrayCore;

/// <summary>
/// Per-app theme override. <see cref="Auto"/> follows the system
/// AppsUseLightTheme flag; Light and Dark force the corresponding palette
/// regardless of system state.
/// </summary>
public enum ThemePreference
{
    Auto = 0,
    Light = 1,
    Dark = 2,
}
