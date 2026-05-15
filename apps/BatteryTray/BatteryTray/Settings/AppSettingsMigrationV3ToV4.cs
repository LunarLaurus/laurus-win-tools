using System.Text.Json;
using System.Text.Json.Nodes;
using WindowsAppCore;

namespace BatteryTray;

internal sealed class AppSettingsMigrationV3ToV4 : ISettingsMigration
{
    public int FromVersion => 3;

    public JsonDocument Migrate(JsonDocument raw)
    {
        var node = JsonNode.Parse(raw.RootElement.GetRawText()) as JsonObject;
        if (node is null) return raw;

        // v3 -> v4: no field rename, no value remap. The bump exists to
        // keep the migration-chain pattern intact and to ensure loading a
        // v3 file produces a fully-populated v4 instance with
        // HardwareActions == null (sentinel for "user has never configured
        // this", which drives the Settings dialog's initial-state logic).
        node["SchemaVersion"] = 4;
        return JsonDocument.Parse(node.ToJsonString());
    }
}
