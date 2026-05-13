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

    /// <summary>
    /// Returns <c>true</c> when domain state has changed since the last <see cref="Render"/>
    /// call, meaning a new icon should be produced. Providers reset this to <c>false</c>
    /// inside <see cref="Render"/> and set it back to <c>true</c> whenever their state
    /// changes. The default implementation always returns <c>true</c> (always dirty) so
    /// providers that don't implement a dirty-flag still work correctly.
    /// <para>
    /// <see cref="TrayIconManager.RequestRefresh"/> skips rendering when this is <c>false</c>.
    /// <see cref="TrayIconManager.ForceRefresh"/> ignores this property entirely.
    /// </para>
    /// </summary>
    bool HasChanged => true;
}
