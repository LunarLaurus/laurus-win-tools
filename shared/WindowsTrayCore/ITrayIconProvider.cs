using System.Drawing;

namespace WindowsTrayCore;

/// <summary>
/// App-specific icon renderer. Implement this per application; the manager calls
/// <see cref="Render"/> when a refresh is needed, passing the current theme so the
/// provider can choose the appropriate colour palette.
/// Providers are typically stateful — they hold domain state (battery level, volume,
/// SSID, …) and use <paramref name="theme"/> only for colour decisions.
/// </summary>
public interface ITrayIconProvider
{
    Icon Render(TrayTheme theme);
}
