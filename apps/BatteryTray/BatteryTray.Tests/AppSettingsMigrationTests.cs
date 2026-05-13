using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;

namespace BatteryTray.Tests;

public class AppSettingsMigrationTests
{
    [Fact]
    public void V1ToV2_BumpsDefaultIntervalFromFiveToThirty()
    {
        var v1Json = """
            {
              "SchemaVersion": 1,
              "UpdateIntervalSeconds": 5,
              "LowBatteryThreshold": 20
            }
            """;

        var output = new AppSettingsMigrationV1ToV2().Migrate(JsonDocument.Parse(v1Json));
        var node = JsonNode.Parse(output.RootElement.GetRawText())!.AsObject();

        ((int)node["UpdateIntervalSeconds"]!).Should().Be(30,
            because: "v2 bumps the old 5s default to 30s");
    }

    [Fact]
    public void V1ToV2_LeavesNonDefaultIntervalAlone()
    {
        var v1Json = """
            {
              "SchemaVersion": 1,
              "UpdateIntervalSeconds": 15
            }
            """;

        var output = new AppSettingsMigrationV1ToV2().Migrate(JsonDocument.Parse(v1Json));
        var node = JsonNode.Parse(output.RootElement.GetRawText())!.AsObject();

        ((int)node["UpdateIntervalSeconds"]!).Should().Be(15,
            because: "we only migrate the old default, not user customizations");
    }

    [Fact]
    public void V2ToV3_RemovesDeadBatterySaverFields()
    {
        var v2Json = """
            {
              "SchemaVersion": 2,
              "AutoEnableBatterySaver": true,
              "BatterySaverThreshold": 25,
              "UpdateIntervalSeconds": 30
            }
            """;

        var output = new AppSettingsMigrationV2ToV3().Migrate(JsonDocument.Parse(v2Json));
        var node = JsonNode.Parse(output.RootElement.GetRawText())!.AsObject();

        node.ContainsKey("AutoEnableBatterySaver").Should().BeFalse(
            because: "v3 removed the dead field that pretended to enable Battery Saver");
        node.ContainsKey("BatterySaverThreshold").Should().BeFalse();
    }

    [Fact]
    public void V1ToV3_FullChain_AppliesAllMigrations()
    {
        var v1Json = """
            {
              "SchemaVersion": 1,
              "UpdateIntervalSeconds": 5,
              "AutoEnableBatterySaver": true,
              "BatterySaverThreshold": 25
            }
            """;

        var afterV2 = new AppSettingsMigrationV1ToV2().Migrate(JsonDocument.Parse(v1Json));
        var afterV3 = new AppSettingsMigrationV2ToV3().Migrate(afterV2);
        var node = JsonNode.Parse(afterV3.RootElement.GetRawText())!.AsObject();

        ((int)node["UpdateIntervalSeconds"]!).Should().Be(30);
        ((int)node["SchemaVersion"]!).Should().Be(3);
        node.ContainsKey("AutoEnableBatterySaver").Should().BeFalse();
        node.ContainsKey("BatterySaverThreshold").Should().BeFalse();
    }
}
