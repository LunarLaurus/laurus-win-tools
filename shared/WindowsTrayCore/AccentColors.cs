using System;
using System.Drawing;

namespace WindowsTrayCore;

internal static class AccentColors
{
    /// <summary>
    /// WCAG 2.1 relative luminance for an sRGB color. Returns 0.0 (black) to 1.0 (white).
    /// </summary>
    internal static double Luminance(Color c)
    {
        static double Channel(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    /// <summary>
    /// Picks black or white text for adequate contrast against the supplied
    /// accent. Threshold: relative luminance > 0.55 yields black; else white.
    /// </summary>
    internal static Color DeriveOn(Color accent) =>
        Luminance(accent) > 0.55 ? Color.FromArgb(0, 0, 0) : Color.FromArgb(0xFF, 0xFF, 0xFF);

    /// <summary>
    /// Alpha-blends the accent at 24% opacity over the surface.
    /// Used for hover / focus rings.
    /// </summary>
    internal static Color DeriveSubtle(Color accent, Color surface)
    {
        const double alpha = 0.24;
        int r = (int)Math.Round(alpha * accent.R + (1 - alpha) * surface.R);
        int g = (int)Math.Round(alpha * accent.G + (1 - alpha) * surface.G);
        int b = (int)Math.Round(alpha * accent.B + (1 - alpha) * surface.B);
        return Color.FromArgb(Clamp(r), Clamp(g), Clamp(b));
    }

    private static int Clamp(int v) => v < 0 ? 0 : v > 255 ? 255 : v;
}
