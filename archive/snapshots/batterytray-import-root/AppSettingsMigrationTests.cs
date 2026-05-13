using System.IO;
using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;

namespace BatteryTray.Tests;

/// <summary>
/// AppSettings.Migrate is internal — these tests use InternalsVisibleTo to reach it
/// directly. We test by feeding JSON strings rather than going through Load(), which
/// keeps the tests independent of %AppData%.
/// </summary>
public class AppSettingsMigrationTests
{
    [Fact]
    public void V1ToV2_BumpsDefaultIntervalFromFiveToThirty()
    {
        var v1 = """
            {
              "SchemaVersion": 1,
              "UpdateIntervalSeconds": 5,
              "LowBatteryThreshold": 20
            }
            """;

        var migrated = InvokeMigrate(v1, fromVersion: 1);
        var node = JsonNode.Parse(migrated)!.AsObject();

        ((int)node["UpdateIntervalSeconds"]!).Should().Be(30,
            because: "v2 is event-driven and 5s polling was wasteful — old default got bumped");
    }

    [Fact]
    public void V1ToV2_LeavesNonDefaultIntervalAlone()
    {
        var v1 = """
            {
              "SchemaVersion": 1,
              "UpdateIntervalSeconds": 15
            }
            """;

        var migrated = InvokeMigrate(v1, fromVersion: 1);
        var node = JsonNode.Parse(migrated)!.AsObject();

        ((int)node["UpdateIntervalSeconds"]!).Should().Be(15,
            because: "we only migrate the old default, not user customizations");
    }

    [Fact]
    public void V2ToV3_RemovesDeadBatterySaverFields()
    {
        var v2 = """
            {
              "SchemaVersion": 2,
              "AutoEnableBatterySaver": true,
              "BatterySaverThreshold": 25,
              "UpdateIntervalSeconds": 30
            }
            """;

        var migrated = InvokeMigrate(v2, fromVersion: 2);
        var node = JsonNode.Parse(migrated)!.AsObject();

        node.ContainsKey("AutoEnableBatterySaver").Should().BeFalse(
            because: "v3 removed the dead field that pretended to enable Battery Saver");
        node.ContainsKey("BatterySaverThreshold").Should().BeFalse();
    }

    [Fact]
    public void Migrate_AlreadyAtCurrentVersion_IsNoOp()
    {
        var current = $$"""
            {
              "SchemaVersion": {{AppSettings.CurrentSchemaVersion}},
              "UpdateIntervalSeconds": 42
            }
            """;

        var migrated = InvokeMigrate(current, fromVersion: AppSettings.CurrentSchemaVersion);
        migrated.Should().Be(current);
    }

    [Fact]
    public void Load_CorruptFile_BacksUpAndReturnsDefaults()
    {
        // Write a bogus settings file, point AppData at a temp dir, ensure Load returns defaults.
        var tempAppData = Path.Combine(Path.GetTempPath(), $"BatteryTrayTest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempAppData, "BatteryTray"));
        var path = Path.Combine(tempAppData, "BatteryTray", "settings.json");
        File.WriteAllText(path, "{ this is not valid json !!!");

        // We can't easily redirect ApplicationData per-process without env tricks.
        // Skip the redirect by directly testing the public Load behaviour in the
        // real %AppData% — but only if we can clean up afterwards.
        try
        {
            Environment.SetEnvironmentVariable("APPDATA", tempAppData);

            var settings = AppSettings.Load();

            settings.Should().NotBeNull();
            settings.SchemaVersion.Should().Be(AppSettings.CurrentSchemaVersion);

            // Original file should have been moved to a .broken-* path.
            var brokenFiles = Directory.GetFiles(Path.GetDirectoryName(path)!, "settings.json.broken-*");
            brokenFiles.Should().NotBeEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable("APPDATA", null);
            try { Directory.Delete(tempAppData, recursive: true); } catch { }
        }
    }

    /// <summary>
    /// Migrate is private; we reach it via reflection. Using reflection rather than
    /// promoting the method to internal because it's a deliberate private detail —
    /// callers should use Load() in production.
    /// </summary>
    private static string InvokeMigrate(string json, int fromVersion)
    {
        var method = typeof(AppSettings).GetMethod("Migrate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (string)method.Invoke(null, new object[] { json, fromVersion })!;
    }
}
