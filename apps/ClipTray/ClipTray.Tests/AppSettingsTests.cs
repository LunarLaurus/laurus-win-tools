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
        // AppSettings.Load/Save read AppPaths.SettingsDir("ClipTray"), which is
        // env-redirected via CLIPTRAY_DATA. TempAppData sets that env var, but
        // it's a PROCESS-WIDE setting: if anything else were to race in this
        // process and clear or rewrite CLIPTRAY_DATA mid-test, the Save() call
        // below would silently land in the user's real %APPDATA%\ClipTray.
        // That actually happened once (see WORKLOG 2026-05-17): the parallel
        // test classes raced on CLIPTRAY_DATA and polluted live settings with
        // PauseCapture=true and Keys.Q, breaking live capture.
        //
        // Belt-and-braces: xunit.runner.json now disables collection-level
        // parallelism, AND this test uses TempAppData with the literal app
        // name "ClipTray" to exercise the production AppSettings type. The
        // env-var redirect is the only thing keeping Save() out of live
        // storage. Don't add more parallel tests that mutate the same env.
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
