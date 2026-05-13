using System.Drawing;

namespace BatteryTray;

internal static class ColorBlender
{
    /// <summary>
    /// Linear RGB interpolation. t is clamped to [0, 1].
    /// </summary>
    public static Color Lerp(Color a, Color b, double t)
    {
        if (t <= 0) return a;
        if (t >= 1) return b;
        return Color.FromArgb(
            Lerp(a.A, b.A, t),
            Lerp(a.R, b.R, t),
            Lerp(a.G, b.G, t),
            Lerp(a.B, b.B, t));
    }

    private static int Lerp(int a, int b, double t) =>
        (int)Math.Round(a + (b - a) * t);
}
