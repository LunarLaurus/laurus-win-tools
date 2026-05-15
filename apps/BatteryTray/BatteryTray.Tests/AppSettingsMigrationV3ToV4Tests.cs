using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;

namespace BatteryTray.Tests;

public class AppSettingsMigrationV3ToV4Tests
{
    [Fact]
    public void FromVersion_IsThree()
    {
        new AppSettingsMigrationV3ToV4().FromVersion.Should().Be(3);
    }

    [Fact]
    public void Migrate_BumpsSchemaVersionToFour()
    {
        var v3Json = """
            {
              "SchemaVersion": 3,
              "UpdateIntervalSeconds": 30,
              "LowBatteryThreshold": 20
            }
            """;

        var output = new AppSettingsMigrationV3ToV4().Migrate(JsonDocument.Parse(v3Json));
        var node = JsonNode.Parse(output.RootElement.GetRawText())!.AsObject();

        ((int)node["SchemaVersion"]!).Should().Be(4);
    }

    [Fact]
    public void Migrate_PreservesAllExistingFields()
    {
        var v3Json = """
            {
              "SchemaVersion": 3,
              "UpdateIntervalSeconds": 45,
              "LowBatteryThreshold": 15,
              "NotifyOnLow": false,
              "ColorCharging": "#ABCDEF"
            }
            """;

        var output = new AppSettingsMigrationV3ToV4().Migrate(JsonDocument.Parse(v3Json));
        var node = JsonNode.Parse(output.RootElement.GetRawText())!.AsObject();

        ((int)node["UpdateIntervalSeconds"]!).Should().Be(45);
        ((int)node["LowBatteryThreshold"]!).Should().Be(15);
        ((bool)node["NotifyOnLow"]!).Should().BeFalse();
        ((string)node["ColorCharging"]!).Should().Be("#ABCDEF");
    }

    [Fact]
    public void Migrate_DoesNotAddHardwareActionsField()
    {
        var v3Json = """{"SchemaVersion": 3}""";

        var output = new AppSettingsMigrationV3ToV4().Migrate(JsonDocument.Parse(v3Json));
        var node = JsonNode.Parse(output.RootElement.GetRawText())!.AsObject();

        node.ContainsKey("HardwareActions").Should().BeFalse(
            because: "the migration is a no-op for content; HardwareActions stays null which the serialiser will fill in on Load");
    }
}
