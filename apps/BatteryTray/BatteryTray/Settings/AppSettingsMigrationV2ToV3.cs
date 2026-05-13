using System.Text.Json;
using System.Text.Json.Nodes;
using WindowsAppCore;

namespace BatteryTray;

internal sealed class AppSettingsMigrationV2ToV3 : ISettingsMigration
{
    public int FromVersion => 2;

    public JsonDocument Migrate(JsonDocument raw)
    {
        var node = JsonNode.Parse(raw.RootElement.GetRawText()) as JsonObject;
        if (node is null) return raw;

        // v2 → v3: drop the dead Battery Saver fields. Explicit removal keeps the
        // saved file clean even though the deserialiser would silently ignore them.
        node.Remove("AutoEnableBatterySaver");
        node.Remove("BatterySaverThreshold");
        node["SchemaVersion"] = 3;
        return JsonDocument.Parse(node.ToJsonString());
    }
}
