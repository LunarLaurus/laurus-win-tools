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
        form.BackColor = theme.Surface;
        form.ForeColor = theme.Foreground;
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
                btn.BackColor = theme.SurfaceAlt;
                btn.ForeColor = theme.Foreground;
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderColor = theme.Accent;
                break;
            case TextBox tb:
                tb.BackColor = theme.SurfaceAlt;
                tb.ForeColor = theme.Foreground;
                tb.BorderStyle = BorderStyle.FixedSingle;
                break;
            case NumericUpDown num:
                num.BackColor = theme.SurfaceAlt;
                num.ForeColor = theme.Foreground;
                num.BorderStyle = BorderStyle.FixedSingle;
                break;
            case ComboBox cb:
                cb.BackColor = theme.SurfaceAlt;
                cb.ForeColor = theme.Foreground;
                cb.FlatStyle = FlatStyle.Flat;
                break;
            case CheckBox chk:
                chk.BackColor = theme.Surface;
                chk.ForeColor = theme.Foreground;
                break;
            case RadioButton rb:
                rb.BackColor = theme.Surface;
                rb.ForeColor = theme.Foreground;
                break;
            case ListView lv:
                lv.BackColor = theme.SurfaceAlt;
                lv.ForeColor = theme.Foreground;
                break;
            case ListBox lb:
                lb.BackColor = theme.SurfaceAlt;
                lb.ForeColor = theme.Foreground;
                break;
            case TabControl tc:
                tc.BackColor = theme.Surface;
                tc.ForeColor = theme.Foreground;
                break;
            case TabPage tp:
                tp.BackColor = theme.Surface;
                tp.ForeColor = theme.Foreground;
                break;
            case GroupBox gb:
                gb.BackColor = theme.Surface;
                gb.ForeColor = theme.Foreground;
                break;
            case LinkLabel lk:
                lk.BackColor = theme.Surface;
                lk.LinkColor = theme.Accent;
                lk.ActiveLinkColor = theme.Accent;
                lk.VisitedLinkColor = theme.Accent;
                break;
            case Label lbl:
                lbl.BackColor = theme.Surface;
                lbl.ForeColor = theme.Foreground;
                break;
            case TableLayoutPanel tlp:
                tlp.BackColor = theme.Surface;
                tlp.ForeColor = theme.Foreground;
                break;
            case FlowLayoutPanel flp:
                flp.BackColor = theme.Surface;
                flp.ForeColor = theme.Foreground;
                break;
            case Panel p:
                p.BackColor = theme.Surface;
                p.ForeColor = theme.Foreground;
                break;
            default:
                c.BackColor = theme.Surface;
                c.ForeColor = theme.Foreground;
                break;
        }
    }
}
