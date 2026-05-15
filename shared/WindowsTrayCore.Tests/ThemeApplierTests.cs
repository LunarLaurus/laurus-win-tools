using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class ThemeApplierTests
{
    [WindowsFact]
    public void ApplyTo_SetsFormBackgroundAndText()
    {
        using var form = new Form();
        var theme = new TrayTheme(isLight: false);
        ThemeApplier.ApplyTo(form, theme);
        form.BackColor.Should().Be(theme.Surface);
        form.ForeColor.Should().Be(theme.Foreground);
    }

    [WindowsFact]
    public void ApplyTo_RecursesIntoNestedControls()
    {
        using var form = new Form();
        var outer = new Panel();
        var inner = new Label { Text = "hi" };
        outer.Controls.Add(inner);
        form.Controls.Add(outer);

        var theme = new TrayTheme(isLight: false);
        ThemeApplier.ApplyTo(form, theme);

        inner.ForeColor.Should().Be(theme.Foreground);
        inner.BackColor.Should().Be(theme.Surface);
    }

    [WindowsFact]
    public void ApplyTo_TextBox_UsesFieldColorAndFixedBorder()
    {
        using var form = new Form();
        var tb = new TextBox();
        form.Controls.Add(tb);
        var theme = new TrayTheme(isLight: true);
        ThemeApplier.ApplyTo(form, theme);
        tb.BackColor.Should().Be(theme.SurfaceAlt);
        tb.BorderStyle.Should().Be(BorderStyle.FixedSingle);
    }

    [WindowsFact]
    public void ApplyTo_Button_UsesSurfaceAltAndFlat()
    {
        using var form = new Form();
        var btn = new Button { Text = "OK" };
        form.Controls.Add(btn);
        var theme = new TrayTheme(isLight: true);
        ThemeApplier.ApplyTo(form, theme);
        btn.BackColor.Should().Be(theme.SurfaceAlt);
        btn.FlatStyle.Should().Be(FlatStyle.Flat);
        btn.FlatAppearance.BorderColor.Should().Be(theme.Accent);
    }

    [WindowsFact]
    public void ApplyTo_Form_DarkTheme_DoesNotThrow()
    {
        using var form = new Form();
        var dark = new TrayTheme(isLight: false);

        var act = () => ThemeApplier.ApplyTo(form, dark);
        act.Should().NotThrow();
    }

    [WindowsFact]
    public void ApplyTitleBar_NoHandle_IsSafeNoOp()
    {
        using var form = new Form();
        // Handle is not yet created.
        var act = () => ThemeApplier.ApplyTitleBar(form, dark: true);
        act.Should().NotThrow();
    }
}
