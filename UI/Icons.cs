using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

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
        using var bmp = new Bitmap(32, 32);
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

        return IconFromBitmap(bmp);
    }

    private static Icon BuildTrayIcon(TrayState state)
    {
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.Bg);

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

        return IconFromBitmap(bmp);
    }

    // GetHicon allocates a GDI HICON that Icon.FromHandle does not own.
    // Clone copies the icon bits into managed memory so we can immediately
    // release the GDI handle without affecting the returned Icon.
    private static Icon IconFromBitmap(Bitmap bmp)
    {
        var hicon = bmp.GetHicon();
        try   { return (Icon)Icon.FromHandle(hicon).Clone(); }
        finally { DestroyIcon(hicon); }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

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
