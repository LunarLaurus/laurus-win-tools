using System.IO;
using System.Text.Json;
using FluentAssertions;
using WindowsAppTesting;
using Xunit;

namespace BatteryTray.Tests;

public class AppSettingsV4IntegrationTests
{
    [Fact]
    public void Load_FreshFile_HasSchemaVersionFourAndNullHardwareActions()
    {
        using var temp = new TempAppData("BatteryTray");

        var settings = AppSettings.Load();
        try
        {
            settings.SchemaVersion.Should().Be(4);
            settings.HardwareActions.Should().BeNull(
                because: "a brand-new file has no user-configured policy");
        }
        finally
        {
            settings.Save();
        }
    }

    [Fact]
    public void Load_FromV3OnDiskFile_MigratesToV4WithNullHardwareActions()
    {
        using var temp = new TempAppData("BatteryTray");

        var settingsPath = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(settingsPath, """
            {
              "SchemaVersion": 3,
              "UpdateIntervalSeconds": 45
            }
            """);

        var settings = AppSettings.Load();

        settings.SchemaVersion.Should().Be(4);
        settings.UpdateIntervalSeconds.Should().Be(45,
            because: "the v3 -> v4 migration must preserve existing values");
        settings.HardwareActions.Should().BeNull();
    }

    [Fact]
    public void SaveAndLoad_PreservesHardwareActionsPolicy()
    {
        using var temp = new TempAppData("BatteryTray");

        var written = AppSettings.Load();
        written.HardwareActions = new HardwareActionPolicy
        {
            PowerButton          = HardwareAction.Hibernate,
            LidClose             = HardwareAction.DoNothing,
            DifferOnBattery      = true,
            PowerButtonOnBattery = HardwareAction.ShutDown,
            LidCloseOnBattery    = HardwareAction.TurnOffDisplay,
        };
        written.Save();

        var read = AppSettings.Load();

        read.HardwareActions.Should().NotBeNull();
        read.HardwareActions!.PowerButton.Should().Be(HardwareAction.Hibernate);
        read.HardwareActions.LidClose.Should().Be(HardwareAction.DoNothing);
        read.HardwareActions.DifferOnBattery.Should().BeTrue();
        read.HardwareActions.PowerButtonOnBattery.Should().Be(HardwareAction.ShutDown);
        read.HardwareActions.LidCloseOnBattery.Should().Be(HardwareAction.TurnOffDisplay);
    }
}
