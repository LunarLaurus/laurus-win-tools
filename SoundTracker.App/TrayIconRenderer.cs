using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using SoundTracker.App.Audio;

namespace SoundTracker.App;

internal static class TrayIconRenderer
{
    public static Icon Render(EndpointVolumeSnapshot snapshot)
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        DrawSpeaker(graphics);
        DrawLevel(graphics, snapshot);
        if (snapshot.IsMuted)
        {
            DrawMuteOverlay(graphics);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var tempIcon = Icon.FromHandle(handle);
            return (Icon)tempIcon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static void DrawSpeaker(Graphics graphics)
    {
        using var brush = new SolidBrush(Color.FromArgb(34, 34, 34));
        using var outlinePen = new Pen(Color.FromArgb(70, 70, 70), 1.4f);

        graphics.FillRectangle(brush, 3, 12, 5, 8);
        var points = new[]
        {
            new PointF(8, 12),
            new PointF(15, 7),
            new PointF(15, 25),
            new PointF(8, 20),
        };
        graphics.FillPolygon(brush, points);
        graphics.DrawPolygon(outlinePen, points);
    }

    private static void DrawLevel(Graphics graphics, EndpointVolumeSnapshot snapshot)
    {
        using var offBrush = new SolidBrush(Color.FromArgb(210, 214, 219));
        using var onBrush = new SolidBrush(snapshot.IsMuted ? Color.FromArgb(150, 80, 80) : Color.FromArgb(42, 157, 77));
        using var unavailableBrush = new SolidBrush(Color.FromArgb(150, 150, 150));

        var segmentCount = 5;
        var litSegments = snapshot.IsAvailable
            ? (int)Math.Ceiling(snapshot.Percent / 20.0)
            : 0;

        for (var index = 0; index < segmentCount; index++)
        {
            var height = 4 + (index * 3);
            var x = 18 + (index * 2);
            var y = 23 - height;
            var rectangle = new Rectangle(x, y, 2, height);

            if (!snapshot.IsAvailable)
            {
                graphics.FillRectangle(unavailableBrush, rectangle);
            }
            else
            {
                graphics.FillRectangle(index < litSegments ? onBrush : offBrush, rectangle);
            }
        }
    }

    private static void DrawMuteOverlay(Graphics graphics)
    {
        using var pen = new Pen(Color.FromArgb(214, 64, 69), 2.6f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        graphics.DrawLine(pen, 18, 7, 29, 25);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
