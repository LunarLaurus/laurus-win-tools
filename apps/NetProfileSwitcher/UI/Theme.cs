using System.Drawing;
using System.Windows.Forms;
using WindowsTrayCore;

namespace NetProfileSwitcher.UI;

public static class Theme
{
    public static readonly Font Body     = new("Segoe UI", 9.5f);
    public static readonly Font BodyBold = new("Segoe UI", 9.5f, FontStyle.Bold);
    public static readonly Font Header   = new("Segoe UI", 13f, FontStyle.Bold);
    public static readonly Font Small    = new("Segoe UI", 8f);

    public static void StyleTextBox(TextBox tb)
    {
        tb.BackColor = TrayTheme.Current.SurfaceAlt;
        tb.ForeColor = TrayTheme.Current.Foreground;
        tb.Font = Body;
        tb.BorderStyle = BorderStyle.FixedSingle;
    }

    public static void StyleListBox(ListBox lb)
    {
        lb.BackColor = TrayTheme.Current.SurfaceAlt;
        lb.ForeColor = TrayTheme.Current.Foreground;
        lb.Font = Body;
        lb.BorderStyle = BorderStyle.None;
        lb.DrawMode = DrawMode.OwnerDrawFixed;
        lb.ItemHeight = 30;
        lb.DrawItem += (s, e) =>
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using var bg = new SolidBrush(sel ? TrayTheme.Current.Accent : TrayTheme.Current.SurfaceAlt);
            using var fg = new SolidBrush(sel ? TrayTheme.Current.AccentOn : TrayTheme.Current.Foreground);
            e.Graphics.FillRectangle(bg, e.Bounds);
            e.Graphics.DrawString(lb.Items[e.Index]?.ToString(), Body, fg,
                                  e.Bounds.X + 10, e.Bounds.Y + 5);
        };
    }
}
