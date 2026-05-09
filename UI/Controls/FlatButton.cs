using System.Drawing;
using System.Windows.Forms;

namespace NetProfileSwitcher.UI.Controls;

public class FlatButton : Button
{
    private Color _bg;
    private Color _hover;

    public FlatButton(Color bg, Color hover)
    {
        _bg = bg; _hover = hover;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = bg;
        ForeColor = Color.White;
        Font = Theme.BodyBold;
        Cursor = Cursors.Hand;
        Height = 32;
    }

    protected override void OnMouseEnter(EventArgs e) { BackColor = _hover; base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { BackColor = _bg; base.OnMouseLeave(e); }
}
