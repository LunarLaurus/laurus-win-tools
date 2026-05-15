using FluentAssertions;
using System.Windows.Forms;
using WindowsAppTesting;
using WindowsTrayCore;
using Xunit;

namespace ClipTray.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_MatchSpec()
    {
        using var temp = new TempAppData("ClipTray");
        var s = AppSettings.Load();

        s.SchemaVersion.Should().Be(1);
        s.PickerHotkeyModifiers.Should().Be(HotkeyModifiers.Control | HotkeyModifiers.Shift);
        s.PickerHotkeyKey.Should().Be(Keys.V);
        s.TextHistoryCap.Should().Be(50);
        s.ImageHistoryCap.Should().Be(10);
        s.DiskQuotaMb.Should().Be(100);
        s.PauseOnLockScreen.Should().BeTrue();
        s.PasswordHeuristicEnabled.Should().BeTrue();
        s.PasswordHeuristicMinLength.Should().Be(8);
        s.PasswordHeuristicMaxLength.Should().Be(64);
        s.ForegroundBlocklist.Should().Contain(new[] { "keepass", "1password", "bitwarden", "lastpass" });
        s.PickerWidth.Should().Be(400);
        s.PickerHeight.Should().Be(360);
    }

    [Fact]
    public void SaveLoad_RoundTripsAllFields()
    {
        using var temp = new TempAppData("ClipTray");
        var w = AppSettings.Load();
        w.PickerHotkeyKey = Keys.Q;
        w.TextHistoryCap = 123;
        w.ForegroundBlocklist.Add("custom-app");
        w.PauseCapture = true;
        w.Save();

        var r = AppSettings.Load();
        r.PickerHotkeyKey.Should().Be(Keys.Q);
        r.TextHistoryCap.Should().Be(123);
        r.ForegroundBlocklist.Should().Contain("custom-app");
        r.PauseCapture.Should().BeTrue();
    }
}
