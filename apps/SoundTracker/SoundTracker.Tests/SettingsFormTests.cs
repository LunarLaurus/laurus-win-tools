using FluentAssertions;
using SoundTracker.App;
using WindowsAppCore;
using WindowsAppTesting;
using Xunit;

namespace SoundTracker.Tests;

public class SettingsFormTests
{
    [WindowsFact]
    public void Construct_WithConfigAndStartup_HasExpectedTitle()
    {
        using var temp = new TempAppData("SoundTracker");
        var config = SoundTrackerConfig.Load();
        config.StartupDelaySeconds = 12;
        config.Save();
        var startup = new RunKeyStartupRegistration("SoundTrackerTest", "fake.exe");

        using var dlg = new SettingsForm(config, startup);
        dlg.Text.Should().Be("SoundTracker Settings");
    }

    [WindowsFact]
    public void Construct_LoadsCurrentDelay()
    {
        using var temp = new TempAppData("SoundTracker");
        var config = SoundTrackerConfig.Load();
        config.StartupDelaySeconds = 42;
        config.Save();
        var startup = new RunKeyStartupRegistration("SoundTrackerTest", "fake.exe");

        using var dlg = new SettingsForm(config, startup);
        var numeric = dlg.Controls.OfType<NumericUpDown>().FirstOrDefault();
        numeric.Should().NotBeNull();
        numeric!.Value.Should().Be(42);
    }
}
