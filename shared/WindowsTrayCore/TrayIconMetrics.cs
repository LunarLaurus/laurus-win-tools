using System.Runtime.InteropServices;

namespace WindowsTrayCore;

/// <summary>
/// DPI-aware sizing for system tray icons. Tray icons are displayed at the
/// small-icon size scaled by the user's DPI factor: 16px at 100%, 20 at 125%,
/// 24 at 150%, 32 at 200%. Rendering at exactly that size leaves no room for
/// anti-aliasing margin; rendering significantly above and letting Windows
/// downscale gives sharper edges.
/// </summary>
public static class TrayIconMetrics
{
    [DllImport("user32.dll")]
    private static extern int GetDpiForSystem();

    /// <summary>
    /// Returns the displayed (post-DPI) tray icon size in pixels —
    /// what Windows will actually paint into the tray slot.
    /// </summary>
    public static int DisplayedSize()
    {
        try
        {
            var dpi = GetDpiForSystem();
            return dpi switch
            {
                <= 96  => 16,
                <= 120 => 20,
                <= 144 => 24,
                <= 192 => 32,
                _      => 48,
            };
        }
        catch
        {
            return 16;
        }
    }

    /// <summary>
    /// Recommended render-time bitmap size. Render here, let Windows
    /// downscale to <see cref="DisplayedSize"/>. The 2× factor balances
    /// crispness against GDI throughput on small machines.
    /// </summary>
    public static int RecommendedRenderSize() => DisplayedSize() * 2;
}
