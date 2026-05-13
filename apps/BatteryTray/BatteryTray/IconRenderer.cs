using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.InteropServices;

namespace BatteryTray;

public static class IconRenderer
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern int GetDpiForSystem();

    public static Icon Create(BatteryState state, AppSettings settings)
    {
        int size = settings.DpiAwareIcon ? PickIconSize() : 32;

        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        try
        {
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);

                var bg = SelectBackgroundColor(state, settings);
                var fg = ResolveTextColor(settings);

                if (settings.Style == IconStyle.Bar)
                {
                    DrawBatteryShape(g, size, state, bg, fg, settings);
                }
                else
                {
                    DrawRoundedFill(g, size, bg);
                    DrawPercentText(g, size, state, fg);
                    if (state.IsCharging) DrawChargingDot(g, size);
                    if (settings.Style == IconStyle.Both) DrawCornerBar(g, size, state, fg);
                }

                // Battery-saver leaf overlay sits on top of everything else, top-left
                // corner, where it doesn't occlude the percentage number.
                if (state.BatterySaverActive && settings.ShowBatterySaverIndicator)
                {
                    DrawBatterySaverLeaf(g, size);
                }
            }

            return Icon.FromHandle(bmp.GetHicon());
        }
        finally
        {
            bmp.Dispose();
        }
    }

    public static void Free(Icon icon)
    {
        var handle = icon.Handle;
        icon.Dispose();
        DestroyIcon(handle);
    }

    private static int PickIconSize()
    {
        try
        {
            var dpi = GetDpiForSystem();
            return dpi switch
            {
                <= 96  => 32,
                <= 120 => 40,
                <= 144 => 48,
                _      => 64,
            };
        }
        catch { return 32; }
    }

    private static Color SelectBackgroundColor(BatteryState state, AppSettings settings)
    {
        if (!state.HasBattery) return ParseColor(settings.ColorCharging, Color.SteelBlue);
        if (state.IsCharging)  return ParseColor(settings.ColorCharging, Color.SteelBlue);

        var critical = ParseColor(settings.ColorCritical, Color.Crimson);
        var low      = ParseColor(settings.ColorLow,      Color.DarkOrange);
        var normal   = ParseColor(settings.ColorNormal,   Color.SeaGreen);

        if (!settings.SmoothColorTransitions)
        {
            if (state.Percent <= settings.CriticalBatteryThreshold) return critical;
            if (state.Percent <= settings.LowBatteryThreshold)      return low;
            return normal;
        }

        const int half = 5;
        int lowT  = settings.LowBatteryThreshold;
        int critT = settings.CriticalBatteryThreshold;

        if (state.Percent <= critT - half) return critical;
        if (state.Percent <= critT + half)
        {
            double t = (state.Percent - (critT - half)) / (double)(2 * half);
            return ColorBlender.Lerp(critical, low, t);
        }
        if (state.Percent <= lowT - half) return low;
        if (state.Percent <= lowT + half)
        {
            double t = (state.Percent - (lowT - half)) / (double)(2 * half);
            return ColorBlender.Lerp(low, normal, t);
        }
        return normal;
    }

    private static Color ResolveTextColor(AppSettings settings)
        => ParseColor(settings.ColorText, Color.White);

    private static void DrawRoundedFill(Graphics g, int size, Color color)
    {
        using var path = RoundedRectPath(new Rectangle(0, 0, size, size), radius: size / 5);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    private static void DrawPercentText(Graphics g, int size, BatteryState state, Color textColor)
    {
        string text =
            !state.HasBattery   ? "AC" :
            state.Percent >= 100 ? "F"  :
                                   state.Percent.ToString(CultureInfo.InvariantCulture);

        float baseEm = size * 0.75f;
        float emSize = text.Length switch
        {
            1 => baseEm,
            2 => baseEm * 0.79f,
            _ => baseEm * 0.58f,
        };

        using var font = new Font("Segoe UI", emSize, FontStyle.Bold, GraphicsUnit.Pixel);
        var measured = g.MeasureString(text, font);
        var x = (size - measured.Width) / 2f;
        var y = (size - measured.Height) / 2f - (size * 0.03f);
        using var brush = new SolidBrush(textColor);
        g.DrawString(text, font, brush, x, y);
    }

    private static void DrawChargingDot(Graphics g, int size)
    {
        var dot = Math.Max(6, size / 4);
        var rect = new Rectangle(size - dot - 2, size - dot - 2, dot, dot);
        using var fill = new SolidBrush(Color.FromArgb(255, 255, 215, 64));
        g.FillEllipse(fill, rect);
        using var pen = new Pen(Color.FromArgb(180, 0, 0, 0), Math.Max(1, size / 32));
        g.DrawEllipse(pen, rect);
    }

    private static void DrawCornerBar(Graphics g, int size, BatteryState state, Color color)
    {
        var width = (int)Math.Round(state.Percent / 100f * (size - size / 6));
        if (width <= 0) return;
        var bar = new Rectangle(size / 12, size - size / 10, width, Math.Max(2, size / 16));
        using var brush = new SolidBrush(color);
        g.FillRectangle(brush, bar);
    }

    private static void DrawBatteryShape(Graphics g, int size, BatteryState state, Color fillColor, Color textColor, AppSettings settings)
    {
        bool darkTheme = settings.Theme switch
        {
            IconTheme.Light => false,
            IconTheme.Dark  => true,
            _               => ThemeDetector.IsTaskbarDark(),
        };
        var shellColor = darkTheme
            ? Color.FromArgb(220, 230, 230, 230)
            : Color.FromArgb(220, 33, 33, 33);

        var pad   = size / 10;
        var bodyW = size - 2 * pad - size / 8;
        var bodyH = (int)(size * 0.55f);
        var bodyX = pad;
        var bodyY = (size - bodyH) / 2;
        var body  = new Rectangle(bodyX, bodyY, bodyW, bodyH);

        var tipW = Math.Max(3, size / 10);
        var tipH = Math.Max(4, bodyH / 2);
        var tipX = body.Right + Math.Max(1, size / 32);
        var tipY = body.Y + (body.Height - tipH) / 2;

        var stroke = Math.Max(2, size / 16);

        using (var bodyPen = new Pen(shellColor, stroke))
        using (var tipBrush = new SolidBrush(shellColor))
        {
            g.DrawRectangle(bodyPen, body);
            g.FillRectangle(tipBrush, new Rectangle(tipX, tipY, tipW, tipH));
        }

        var inner = Rectangle.Inflate(body, -stroke, -stroke);
        var fillW = (int)Math.Round(inner.Width * (state.Percent / 100f));
        if (fillW > 0)
        {
            using var fillBrush = new SolidBrush(fillColor);
            g.FillRectangle(fillBrush, new Rectangle(inner.X, inner.Y, fillW, inner.Height));
        }

        if (state.IsCharging) DrawBolt(g, body, textColor);
    }

    private static void DrawBolt(Graphics g, Rectangle area, Color color)
    {
        var cx = area.X + area.Width / 2f;
        var cy = area.Y + area.Height / 2f;
        var unit = area.Height / 5f;
        var pts = new PointF[]
        {
            new(cx + unit * 0.4f,  cy - unit * 1.6f),
            new(cx - unit * 0.9f,  cy + unit * 0.2f),
            new(cx - unit * 0.1f,  cy + unit * 0.2f),
            new(cx - unit * 0.4f,  cy + unit * 1.6f),
            new(cx + unit * 0.9f,  cy - unit * 0.2f),
            new(cx + unit * 0.1f,  cy - unit * 0.2f),
        };
        using var brush = new SolidBrush(color);
        g.FillPolygon(brush, pts);
    }

    /// <summary>
    /// Small leaf-shaped overlay in the top-left corner indicating Battery Saver
    /// is active. We use a stylised teardrop with a centerline rather than a literal
    /// leaf — much more legible at 16-32px than detailed foliage.
    /// </summary>
    private static void DrawBatterySaverLeaf(Graphics g, int size)
    {
        var leafSize = Math.Max(8, size / 3);
        var rect = new Rectangle(2, 2, leafSize, leafSize);

        // Build leaf as two arcs on a square — looks like a stylised teardrop / leaf.
        using var path = new GraphicsPath();
        path.AddArc(rect, 180, 90);
        path.AddArc(rect, 0,   90);
        path.CloseFigure();

        // Background halo for legibility on any color.
        using (var halo = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
        {
            var haloRect = Rectangle.Inflate(rect, 1, 1);
            using var haloPath = new GraphicsPath();
            haloPath.AddArc(haloRect, 180, 90);
            haloPath.AddArc(haloRect, 0,   90);
            haloPath.CloseFigure();
            g.FillPath(halo, haloPath);
        }

        using (var leaf = new SolidBrush(Color.FromArgb(255, 129, 199, 132))) // soft green
        {
            g.FillPath(leaf, path);
        }

        // Center vein.
        using var vein = new Pen(Color.FromArgb(220, 27, 94, 32), Math.Max(1, leafSize / 12));
        g.DrawLine(vein, rect.Left + leafSize / 2, rect.Top + 2,
                         rect.Left + leafSize / 2, rect.Bottom - 2);
    }

    private static GraphicsPath RoundedRectPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(bounds.X,         bounds.Y,         d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y,         d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d,d, d, 0,   90);
        path.AddArc(bounds.X,         bounds.Bottom - d,d, d, 90,  90);
        path.CloseFigure();
        return path;
    }

    private static Color ParseColor(string hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try { return ColorTranslator.FromHtml(hex); }
        catch { return fallback; }
    }
}
