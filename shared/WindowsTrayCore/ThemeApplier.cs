namespace WindowsTrayCore;

/// <summary>
/// Recursively applies <see cref="TrayTheme"/> colours to every control in a
/// Form so legacy / hand-built forms get theme support without per-control edits.
/// Apply once at the end of the form constructor, then call again from a
/// <see cref="TrayTheme.Changed"/> handler to live-update on system theme flips.
/// </summary>
public static class ThemeApplier
{
    public static void ApplyTo(Form form) => ApplyTo(form, TrayTheme.Current);

    public static void ApplyTo(Form form, TrayTheme theme)
    {
        ApplyTitleBar(form, !theme.IsLight);
        form.BackColor = theme.Background;
        form.ForeColor = theme.Text;
        ApplyToControls(form.Controls, theme);
    }

    /// <summary>
    /// Applies (or removes) the Windows 10 dark title-bar tint for a form via
    /// DwmSetWindowAttribute. Tries attribute 20 (DWMWA_USE_IMMERSIVE_DARK_MODE,
    /// official since Win10 1903) first, falls back to attribute 19 (the
    /// undocumented predecessor for 1809-1902). No-op on older builds and on
    /// any HRESULT failure; the title bar stays default-themed.
    /// </summary>
    public static void ApplyTitleBar(Form form, bool dark)
    {
        if (form is null || !form.IsHandleCreated) return;

        int useDark = dark ? 1 : 0;
        var hr = Native.TrayNativeMethods.DwmSetWindowAttribute(
            form.Handle,
            Native.TrayNativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE,
            ref useDark,
            sizeof(int));

        if (hr != 0)
        {
            Native.TrayNativeMethods.DwmSetWindowAttribute(
                form.Handle,
                Native.TrayNativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE_PRE_20H1,
                ref useDark,
                sizeof(int));
        }
    }

    private static void ApplyToControls(Control.ControlCollection controls, TrayTheme theme)
    {
        foreach (Control c in controls)
        {
            ApplyToControl(c, theme);
            if (c.HasChildren)
                ApplyToControls(c.Controls, theme);
        }
    }

    private static void ApplyToControl(Control c, TrayTheme theme)
    {
        switch (c)
        {
            case Button btn:
                btn.BackColor = theme.Surface;
                btn.ForeColor = theme.Text;
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderColor = theme.Accent;
                break;
            case TextBox tb:
                tb.BackColor = theme.Field;
                tb.ForeColor = theme.Text;
                tb.BorderStyle = BorderStyle.FixedSingle;
                break;
            case NumericUpDown num:
                num.BackColor = theme.Field;
                num.ForeColor = theme.Text;
                num.BorderStyle = BorderStyle.FixedSingle;
                break;
            case ComboBox cb:
                cb.BackColor = theme.Field;
                cb.ForeColor = theme.Text;
                cb.FlatStyle = FlatStyle.Flat;
                break;
            case CheckBox chk:
                chk.BackColor = theme.Background;
                chk.ForeColor = theme.Text;
                break;
            case RadioButton rb:
                rb.BackColor = theme.Background;
                rb.ForeColor = theme.Text;
                break;
            case ListView lv:
                lv.BackColor = theme.Field;
                lv.ForeColor = theme.Text;
                break;
            case ListBox lb:
                lb.BackColor = theme.Field;
                lb.ForeColor = theme.Text;
                break;
            case TabControl tc:
                tc.BackColor = theme.Background;
                tc.ForeColor = theme.Text;
                break;
            case TabPage tp:
                tp.BackColor = theme.Background;
                tp.ForeColor = theme.Text;
                break;
            case GroupBox gb:
                gb.BackColor = theme.Background;
                gb.ForeColor = theme.Text;
                break;
            case LinkLabel lk:
                lk.BackColor = theme.Background;
                lk.LinkColor = theme.Accent;
                lk.ActiveLinkColor = theme.Accent;
                lk.VisitedLinkColor = theme.Accent;
                break;
            case Label lbl:
                lbl.BackColor = theme.Background;
                lbl.ForeColor = theme.Text;
                break;
            case TableLayoutPanel tlp:
                tlp.BackColor = theme.Background;
                tlp.ForeColor = theme.Text;
                break;
            case FlowLayoutPanel flp:
                flp.BackColor = theme.Background;
                flp.ForeColor = theme.Text;
                break;
            case Panel p:
                p.BackColor = theme.Background;
                p.ForeColor = theme.Text;
                break;
            default:
                c.BackColor = theme.Background;
                c.ForeColor = theme.Text;
                break;
        }
    }
}
