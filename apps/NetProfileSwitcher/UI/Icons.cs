using System.Drawing;
using System.Drawing.Drawing2D;
using WindowsTrayCore;

namespace NetProfileSwitcher.UI;

public enum TrayState { Idle, MatchedProfile, Switching, Error }

public static class Icons
{
    public static Icon AppIcon { get; } = BuildAppIcon();

    private static readonly Icon?[] _cache = new Icon?[4];

    public static Icon GetTrayIcon(TrayState state)
    {
        int i = (int)state;
        return _cache[i] ??= BuildTrayIcon(state);
    }

    private static Icon BuildAppIcon()
    {
        // The window-frame app icon is large (Form.Icon / taskbar thumbnail),
        // so a fixed 32 is plenty regardless of DPI.
        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.Bg);

        using var barBrush = new SolidBrush(Theme.Accent);
        g.FillRectangle(barBrush, 5,  22, 6, 6);
        g.FillRectangle(barBrush, 13, 16, 6, 12);
        g.FillRectangle(barBrush, 21, 9,  6, 19);

        using var hilite = new SolidBrush(Color.FromArgb(60, 255, 255, 255));
        g.FillRectangle(hilite, 5,  22, 6, 2);
        g.FillRectangle(hilite, 13, 16, 6, 2);
        g.FillRectangle(hilite, 21, 9,  6, 2);

        return IconBuilder.FromBitmap(bmp);
    }

    private static Icon BuildTrayIcon(TrayState state)
    {
        // Render at DPI-aware size — the old 16x16 baseline scaled up
        // blurry on high-DPI displays. The bar layout was originally
        // designed for a 16-unit canvas; scale all coordinates uniformly.
        int size = TrayIconMetrics.RecommendedRenderSize();
        const int designUnit = 16;
        float scale = size / (float)designUnit;

        using var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.Clear(Theme.Bg);
        g.ScaleTransform(scale, scale);

        Color barColor = state switch
        {
            TrayState.Idle           => Theme.Muted,
            TrayState.MatchedProfile => Theme.Accent,
            TrayState.Switching      => Theme.Green,
            TrayState.Error          => Theme.Red,
            _                        => Theme.Muted,
        };

        using var barBrush = new SolidBrush(barColor);
        g.FillRectangle(barBrush, 2,  11, 3, 3);
        g.FillRectangle(barBrush, 6,  7,  3, 7);
        g.FillRectangle(barBrush, 10, 3,  3, 11);

        if (state == TrayState.Switching)
            DrawLightningOverlay(g);
        else if (state == TrayState.Error)
            DrawErrorOverlay(g);

        return IconBuilder.FromBitmap(bmp);
    }

    private static void DrawLightningOverlay(Graphics g)
    {
        using var p = new Pen(Color.White, 1f);
        g.DrawLines(p, new PointF[] { new(1, 1), new(3, 5), new(1, 5), new(3, 9) });
    }

    private static void DrawErrorOverlay(Graphics g)
    {
        using var b = new SolidBrush(Color.White);
        g.FillRectangle(b, 1, 1, 2, 5);
        g.FillRectangle(b, 1, 7, 2, 2);
    }
}
