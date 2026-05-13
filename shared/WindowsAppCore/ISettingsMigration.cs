using System.Text.Json;

namespace WindowsAppCore;

public interface ISettingsMigration
{
    /// <summary>The schema version this migration transforms FROM.</summary>
    int FromVersion { get; }

    /// <summary>
    /// Transforms the raw JSON document from version <see cref="FromVersion"/> to
    /// <see cref="FromVersion"/> + 1. Must set SchemaVersion in the returned document.
    /// </summary>
    JsonDocument Migrate(JsonDocument raw);
}
