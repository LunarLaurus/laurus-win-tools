using System.Drawing;
using System.Windows.Forms;
using FluentAssertions;
using WindowsTrayCore;
using Xunit;
using NetProfileSwitcher.UI;

namespace NetProfileSwitcher.Tests;

public class ThemeTests : IDisposable
{
    public void Dispose() => TrayTheme.Current.SimulatePreferenceChanged(isLight: false);

    [WindowsFact]
    public void Fonts_AreNotNull()
    {
        Theme.Body.Should().NotBeNull();
        Theme.BodyBold.Should().NotBeNull();
        Theme.Header.Should().NotBeNull();
        Theme.Small.Should().NotBeNull();
    }

    [WindowsFact]
    public void Body_IsSegoeUi_9_5()
    {
        Theme.Body.FontFamily.Name.Should().Be("Segoe UI");
        Theme.Body.Size.Should().Be(9.5f);
        Theme.Body.Bold.Should().BeFalse();
    }

    [WindowsFact]
    public void BodyBold_IsSegoeUi_9_5_Bold()
    {
        Theme.BodyBold.FontFamily.Name.Should().Be("Segoe UI");
        Theme.BodyBold.Size.Should().Be(9.5f);
        Theme.BodyBold.Bold.Should().BeTrue();
    }

    [WindowsFact]
    public void Header_IsSegoeUi_13_Bold()
    {
        Theme.Header.FontFamily.Name.Should().Be("Segoe UI");
        Theme.Header.Size.Should().Be(13f);
        Theme.Header.Bold.Should().BeTrue();
    }

    [WindowsFact]
    public void Small_IsSegoeUi_8()
    {
        Theme.Small.FontFamily.Name.Should().Be("Segoe UI");
        Theme.Small.Size.Should().Be(8f);
    }

    [WindowsFact]
    public void StyleTextBox_SetsColorsFromCurrentTheme()
    {
        TrayTheme.Current.SimulatePreferenceChanged(isLight: false);
        var tb = new TextBox();
        Theme.StyleTextBox(tb);
        tb.BackColor.Should().Be(TrayTheme.Current.SurfaceAlt);
        tb.ForeColor.Should().Be(TrayTheme.Current.Foreground);
        tb.Font.Should().Be(Theme.Body);
        tb.BorderStyle.Should().Be(BorderStyle.FixedSingle);
    }

    [WindowsFact]
    public void StyleTextBox_UpdatesWhenThemeChanges()
    {
        TrayTheme.Current.SimulatePreferenceChanged(isLight: false);
        var tb = new TextBox();
        Theme.StyleTextBox(tb);
        var darkBg = tb.BackColor;

        TrayTheme.Current.SimulatePreferenceChanged(isLight: true);
        Theme.StyleTextBox(tb);
        tb.BackColor.Should().NotBe(darkBg);
    }

    [WindowsFact]
    public void StyleListBox_SetsOwnerDrawAndItemHeight()
    {
        var lb = new ListBox();
        Theme.StyleListBox(lb);
        lb.DrawMode.Should().Be(DrawMode.OwnerDrawFixed);
        lb.ItemHeight.Should().Be(30);
        lb.BorderStyle.Should().Be(BorderStyle.None);
        lb.Font.Should().Be(Theme.Body);
    }

    [WindowsFact]
    public void StyleListBox_SetsColorsFromCurrentTheme()
    {
        TrayTheme.Current.SimulatePreferenceChanged(isLight: false);
        var lb = new ListBox();
        Theme.StyleListBox(lb);
        lb.BackColor.Should().Be(TrayTheme.Current.SurfaceAlt);
        lb.ForeColor.Should().Be(TrayTheme.Current.Foreground);
    }
}
