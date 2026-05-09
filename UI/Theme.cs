using System.Drawing;
using System.Windows.Forms;

namespace NetProfileSwitcher.UI;

public static class Theme
{
    public static readonly Color Bg        = Color.FromArgb(24, 24, 37);
    public static readonly Color Surface   = Color.FromArgb(36, 36, 54);
    public static readonly Color Surface2  = Color.FromArgb(48, 48, 68);
    public static readonly Color Accent    = Color.FromArgb(124, 111, 247);
    public static readonly Color AccentDim = Color.FromArgb(90, 80, 180);
    public static readonly Color Green     = Color.FromArgb(80, 200, 120);
    public static readonly Color Red       = Color.FromArgb(224, 85, 102);
    public static readonly Color Text      = Color.FromArgb(224, 222, 244);
    public static readonly Color Muted     = Color.FromArgb(110, 106, 134);
    public static readonly Color Field     = Color.FromArgb(42, 42, 64);
    public static readonly Font  Body      = new("Segoe UI", 9.5f);
    public static readonly Font  BodyBold  = new("Segoe UI", 9.5f, FontStyle.Bold);
    public static readonly Font  Header    = new("Segoe UI", 13f, FontStyle.Bold);
    public static readonly Font  Small     = new("Segoe UI", 8f);

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
