using System.Drawing;
using System.Windows.Forms;
using WindowsTrayCore;

namespace NetProfileSwitcher.UI;

public static class Theme
{
    public static Color Bg       => TrayTheme.Current.Background;
    public static Color Surface  => TrayTheme.Current.Surface;
    public static Color Surface2 => TrayTheme.Current.IsLight
        ? Color.FromArgb(225, 225, 240)
        : Color.FromArgb(48, 48, 68);
    public static Color Accent    => TrayTheme.Current.Accent;
    public static Color AccentDim => TrayTheme.Current.IsLight
        ? Color.FromArgb(74, 63, 178)
        : Color.FromArgb(90, 80, 180);
    public static Color Green => TrayTheme.Current.Success;
    public static Color Red   => TrayTheme.Current.Error;
    public static Color Text  => TrayTheme.Current.Text;
    public static Color Muted => TrayTheme.Current.TextMuted;
    public static Color Field => TrayTheme.Current.Field;

    public static readonly Font Body     = new("Segoe UI", 9.5f);
    public static readonly Font BodyBold = new("Segoe UI", 9.5f, FontStyle.Bold);
    public static readonly Font Header   = new("Segoe UI", 13f, FontStyle.Bold);
    public static readonly Font Small    = new("Segoe UI", 8f);

    public static void StyleTextBox(TextBox tb)
    {
        tb.BackColor = Field;
        tb.ForeColor = Text;
        tb.Font = Body;
        tb.BorderStyle = BorderStyle.FixedSingle;
    }

    public static void StyleListBox(ListBox lb)
    {
        lb.BackColor = Surface;
        lb.ForeColor = Text;
        lb.Font = Body;
        lb.BorderStyle = BorderStyle.None;
        lb.DrawMode = DrawMode.OwnerDrawFixed;
        lb.ItemHeight = 30;
        lb.DrawItem += (s, e) =>
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using var bg = new SolidBrush(sel ? Accent : Surface);
            using var fg = new SolidBrush(sel ? Color.White : Text);
            e.Graphics.FillRectangle(bg, e.Bounds);
            e.Graphics.DrawString(lb.Items[e.Index]?.ToString(), Body, fg,
                                  e.Bounds.X + 10, e.Bounds.Y + 5);
        };
    }
}
