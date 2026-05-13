using System.Windows.Forms;
using FluentAssertions;
using ProgramHider;
using WindowsAppCore;
using Xunit;

namespace ProgramHider.Tests;

public class MiscTests
{
    [Fact]
    public void SettingsStore_HonorsEnvironmentOverridePath()
    {
        using var temp = new TempAppData("ProgramHider");
        var store = new JsonSettingsStore<AppSettings>("ProgramHider");
        var expectedPath = Path.Combine(temp.Path, "settings.json");
        store.SettingsPath.Should().Be(expectedPath);
    }

    [Fact]
    public void StartupOptions_ParsesFlags()
    {
        var options = StartupOptions.Parse(new[] { "--startup", "--delay=42", "--safe-mode", "--rehide=0x1A2B" });

        options.IsStartupLaunch.Should().BeTrue();
        options.DelaySeconds.Should().Be(42);
        options.SafeMode.Should().BeTrue();
        options.PendingHideHandle.Should().Be((nint)0x1A2B);
    }

    [Fact]
    public void ElevationService_RestartArguments_PreserveRetryState()
    {
        var options = new StartupOptions
        {
            IsStartupLaunch = true,
            DelaySeconds = 12,
            SafeMode = true
        };

        var arguments = ElevationService.BuildRestartArguments(options, (nint)0x45AF);

        arguments.Should().Contain("--startup");
        arguments.Should().Contain("--safe-mode");
        arguments.Should().Contain("--delay=12");
        arguments.Should().Contain("--rehide=0x45AF");
    }

    [Fact]
    public void PinSecurity_HashesAndVerifiesSecrets()
    {
        var hash = PinSecurity.HashSecret("secret");

        hash.Should().NotBeNullOrWhiteSpace();
        PinSecurity.VerifySecret("secret", hash).Should().BeTrue();
        PinSecurity.VerifySecret("wrong", hash).Should().BeFalse();
    }

    [Fact]
    public void HotkeySettings_NormalizesDefaults()
    {
        var settings = new HotkeySettings
        {
            Control = false, Shift = false, Alt = false, Windows = false, Key = Keys.None
        };

        settings.Normalize();

        settings.Control.Should().BeTrue("Normalize must force at least one modifier");
        settings.Key.Should().Be(Keys.H);
    }

    [WindowsFact]
    public void StartupRegistration_CreatesAndRemovesRunKey()
    {
        var reg = new RunKeyStartupRegistration(
            "ProgramHider",
            Application.ExecutablePath,
            "--startup --delay=0");

        try
        {
            reg.Register();
            reg.IsRegistered.Should().BeTrue();
            reg.Unregister();
            reg.IsRegistered.Should().BeFalse();
        }
        finally
        {
            reg.Unregister();
        }
    }
}
