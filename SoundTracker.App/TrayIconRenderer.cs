using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using SoundTracker.App.Audio;

namespace SoundTracker.App;

internal static class TrayIconRenderer
{
    public static Icon Render(EndpointVolumeSnapshot snapshot, bool isLightTaskbarTheme)
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        DrawBadgeBackground(graphics, snapshot, isLightTaskbarTheme);
        DrawSpeaker(graphics, snapshot, isLightTaskbarTheme);
        DrawLevel(graphics, snapshot, isLightTaskbarTheme);
        if (snapshot.IsMuted)
        {
            DrawMuteOverlay(graphics, isLightTaskbarTheme);
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

    private static void DrawBadgeBackground(Graphics graphics, EndpointVolumeSnapshot snapshot, bool isLightTaskbarTheme)
    {
        var rimColor = isLightTaskbarTheme
            ? Color.FromArgb(92, 106, 122)
            : Color.FromArgb(214, 221, 230);
        var fillColor = !snapshot.IsAvailable
            ? (isLightTaskbarTheme ? Color.FromArgb(232, 236, 240) : Color.FromArgb(54, 58, 64))
            : snapshot.IsMuted
                ? (isLightTaskbarTheme ? Color.FromArgb(252, 232, 233) : Color.FromArgb(92, 48, 54))
                : (isLightTaskbarTheme ? Color.FromArgb(234, 245, 238) : Color.FromArgb(40, 77, 61));

        using var fillBrush = new SolidBrush(fillColor);
        using var rimPen = new Pen(rimColor, 1.6f);
        graphics.FillEllipse(fillBrush, 1.5f, 1.5f, 29f, 29f);
        graphics.DrawEllipse(rimPen, 1.5f, 1.5f, 29f, 29f);

        if (!snapshot.IsAvailable)
        {
            return;
        }

        var sweepAngle = Math.Clamp(snapshot.Percent, 0, 100) / 100f * 300f;
        var startAngle = 120f;
        var accentColor = snapshot.IsMuted
            ? Color.FromArgb(216, 74, 81)
            : Color.FromArgb(39, 159, 90);
        using var accentPen = new Pen(accentColor, 2.8f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        graphics.DrawArc(accentPen, 4.5f, 4.5f, 23f, 23f, startAngle, sweepAngle);
    }

    private static void DrawSpeaker(Graphics graphics, EndpointVolumeSnapshot snapshot, bool isLightTaskbarTheme)
    {
        var glyphColor = isLightTaskbarTheme
            ? Color.FromArgb(26, 31, 36)
            : Color.FromArgb(244, 247, 250);
        var outlineColor = isLightTaskbarTheme
            ? Color.FromArgb(70, 78, 88)
            : Color.FromArgb(210, 216, 224);
        using var brush = new SolidBrush(snapshot.IsAvailable ? glyphColor : Color.FromArgb(140, outlineColor));
        using var outlinePen = new Pen(outlineColor, 1.2f);

        graphics.FillRectangle(brush, 6, 13, 4, 6);
        var points = new[]
        {
            new PointF(10, 13),
            new PointF(16, 9),
            new PointF(16, 23),
            new PointF(10, 19),
        };
        graphics.FillPolygon(brush, points);
        graphics.DrawPolygon(outlinePen, points);
    }

    private static void DrawLevel(Graphics graphics, EndpointVolumeSnapshot snapshot, bool isLightTaskbarTheme)
    {
        using var offBrush = new SolidBrush(isLightTaskbarTheme ? Color.FromArgb(201, 207, 214) : Color.FromArgb(108, 116, 126));
        using var onBrush = new SolidBrush(snapshot.IsMuted ? Color.FromArgb(192, 88, 96) : Color.FromArgb(40, 165, 95));
        using var unavailableBrush = new SolidBrush(Color.FromArgb(150, 150, 150));

        var segmentCount = 5;
        var litSegments = snapshot.IsAvailable
            ? (int)Math.Ceiling(snapshot.Percent / 20.0)
            : 0;

        for (var index = 0; index < segmentCount; index++)
        {
            var height = 3 + (index * 2);
            var x = 19 + (index * 2);
            var y = 22 - height;
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

    private static void DrawMuteOverlay(Graphics graphics, bool isLightTaskbarTheme)
    {
        using var pen = new Pen(isLightTaskbarTheme ? Color.FromArgb(214, 64, 69) : Color.FromArgb(255, 126, 134), 2.4f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        graphics.DrawLine(pen, 18, 9, 27, 22);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
