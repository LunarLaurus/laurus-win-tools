using System.Text.Json;
using System.Text.Json.Nodes;
using WindowsAppCore;

namespace BatteryTray;

internal sealed class AppSettingsMigrationV1ToV2 : ISettingsMigration
{
    public int FromVersion => 1;

    public JsonDocument Migrate(JsonDocument raw)
    {
        var node = JsonNode.Parse(raw.RootElement.GetRawText()) as JsonObject;
        if (node is null) return raw;

        // v1 → v2: bump default polling interval if it was the old 5-second default
        if (node["UpdateIntervalSeconds"] is JsonNode interval && (int?)interval == 5)
            node["UpdateIntervalSeconds"] = 30;

        node["SchemaVersion"] = 2;
        return JsonDocument.Parse(node.ToJsonString());
    }
}
