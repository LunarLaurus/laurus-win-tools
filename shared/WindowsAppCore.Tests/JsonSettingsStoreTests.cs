using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FluentAssertions;
using Xunit;

namespace WindowsAppCore.Tests;

public class JsonSettingsStoreTests : IDisposable
{
    private readonly string _appName = $"StoreTest-{Guid.NewGuid():N}";
    private readonly TempAppData _temp;

    public JsonSettingsStoreTests()
    {
        _temp = new TempAppData(_appName);
    }

    public void Dispose() => _temp.Dispose();

    private string SettingsFile => Path.Combine(_temp.Path, "settings.json");

    // ── Happy path ─────────────────────────────────────────────────────────

    [Fact]
    public void Load_NoFile_ReturnsDefaultInstance()
    {
        var store = new JsonSettingsStore<FlatSettings>(_appName);
        var s = store.Load();
        s.Should().NotBeNull();
        s.Name.Should().Be("default");
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var store = new JsonSettingsStore<FlatSettings>(_appName);
        store.Save(new FlatSettings { Name = "voyager", Count = 42 });
        var s = store.Load();
        s.Name.Should().Be("voyager");
        s.Count.Should().Be(42);
    }

    [Fact]
    public void Save_WritesValidIndentedJson()
    {
        var store = new JsonSettingsStore<FlatSettings>(_appName);
        store.Save(new FlatSettings { Name = "test" });

        File.Exists(SettingsFile).Should().BeTrue();
        var text = File.ReadAllText(SettingsFile);
        text.Should().Contain(Environment.NewLine, "WriteIndented must be true");

        var parsed = JsonDocument.Parse(text);
        parsed.RootElement.GetProperty("Name").GetString().Should().Be("test");
    }

    [Fact]
    public void Save_IsAtomic_NoTmpFileAfterSave()
    {
        var store = new JsonSettingsStore<FlatSettings>(_appName);
        store.Save(new FlatSettings { Name = "check" });
        var tmpFiles = Directory.GetFiles(_temp.Path, "*.tmp");
        tmpFiles.Should().BeEmpty("temp file must be cleaned up after atomic move");
    }

    // ── Corruption quarantine ───────────────────────────────────────────────

    [Fact]
    public void Load_CorruptJson_ReturnsDefault()
    {
        Directory.CreateDirectory(_temp.Path);
        File.WriteAllText(SettingsFile, "{ this is garbage !!! }");
        var store = new JsonSettingsStore<FlatSettings>(_appName);
        var s = store.Load();
        s.Name.Should().Be("default", "corrupt file must yield default, not throw");
    }

    [Fact]
    public void Load_CorruptJson_QuarantinesBrokenFile()
    {
        Directory.CreateDirectory(_temp.Path);
        File.WriteAllText(SettingsFile, "null");
        var store = new JsonSettingsStore<FlatSettings>(_appName);
        store.Load();
        var broken = Directory.GetFiles(_temp.Path, "settings.json.broken-*");
        broken.Should().NotBeEmpty("broken file must be renamed for forensics");
    }

    // ── Normalize delegate ──────────────────────────────────────────────────

    [Fact]
    public void Load_WithNormalize_ClampsCalled()
    {
        File.WriteAllText(SettingsFile, """{"Name":"x","Count":-99}""");
        var store = new JsonSettingsStore<FlatSettings>(
            _appName,
            normalize: s => { s.Count = Math.Max(0, s.Count); return s; });
        var s = store.Load();
        s.Count.Should().Be(0, "normalize must clamp negative count");
    }

    // ── Migration chain ─────────────────────────────────────────────────────

    [Fact]
    public void Load_WithMigration_AppliesSingleStep()
    {
        File.WriteAllText(SettingsFile, """{"Name":"old","SchemaVersion":1}""");
        var store = new JsonSettingsStore<FlatSettings>(
            _appName,
            migrations: new[] { new RenameV1ToV2() });
        var s = store.Load();
        s.Name.Should().Be("migrated");
        s.SchemaVersion.Should().Be(2);
    }

    [Fact]
    public void Load_WithMigrationChain_AppliesAllSteps()
    {
        File.WriteAllText(SettingsFile, """{"Name":"start","Count":0,"SchemaVersion":1}""");
        var store = new JsonSettingsStore<FlatSettings>(
            _appName,
            migrations: new ISettingsMigration[] { new RenameV1ToV2(), new BumpCountV2ToV3() });
        var s = store.Load();
        s.Name.Should().Be("migrated");
        s.Count.Should().Be(100);
        s.SchemaVersion.Should().Be(3);
    }

    [Fact]
    public void Load_FileAtCurrentVersion_SkipsMigrations()
    {
        File.WriteAllText(SettingsFile, """{"Name":"fresh","Count":5,"SchemaVersion":3}""");
        var tracker = new TrackingMigration(fromVersion: 1);
        var store = new JsonSettingsStore<FlatSettings>(_appName, migrations: new[] { tracker });
        var s = store.Load();
        tracker.CallCount.Should().Be(0, "migration must not run when file is already at/above FromVersion");
        s.Name.Should().Be("fresh");
    }

    [Fact]
    public void Load_NoSchemaVersion_TreatedAsV0_AllMigrationsRun()
    {
        File.WriteAllText(SettingsFile, """{"Name":"legacy","Count":0}""");
        var store = new JsonSettingsStore<FlatSettings>(
            _appName,
            migrations: new[] { new BumpCountV2ToV3() });
        // BumpCountV2ToV3 has FromVersion=2, and file has no version (treated as 0),
        // so 0 < 2 means migration runs.
        var s = store.Load();
        s.Count.Should().Be(100);
    }

    // ── Per-store serialiser options ────────────────────────────────────────

    [Fact]
    public void Save_WithCamelCaseOptions_WritesCamelCase()
    {
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var store = new JsonSettingsStore<FlatSettings>(_appName, options: opts);
        store.Save(new FlatSettings { Name = "camel" });
        var text = File.ReadAllText(SettingsFile);
        text.Should().Contain("\"name\":", "camelCase policy must apply to property names");
    }

    [Fact]
    public void Save_AlwaysWritesIndented_EvenWithCustomOptions()
    {
        var opts = new JsonSerializerOptions { WriteIndented = false };
        var store = new JsonSettingsStore<FlatSettings>(_appName, options: opts);
        store.Save(new FlatSettings { Name = "indent" });
        File.ReadAllText(SettingsFile).Should().Contain(Environment.NewLine);
    }

    // ── Test types ──────────────────────────────────────────────────────────

    internal sealed class FlatSettings
    {
        public string Name { get; set; } = "default";
        public int Count { get; set; }
        public int SchemaVersion { get; set; }
    }

    private sealed class RenameV1ToV2 : ISettingsMigration
    {
        public int FromVersion => 1;
        public JsonDocument Migrate(JsonDocument raw)
        {
            var node = JsonNode.Parse(raw.RootElement.GetRawText())!.AsObject();
            node["Name"] = "migrated";
            node["SchemaVersion"] = 2;
            return JsonDocument.Parse(node.ToJsonString());
        }
    }

    private sealed class BumpCountV2ToV3 : ISettingsMigration
    {
        public int FromVersion => 2;
        public JsonDocument Migrate(JsonDocument raw)
        {
            var node = JsonNode.Parse(raw.RootElement.GetRawText())!.AsObject();
            node["Count"] = (node["Count"]?.GetValue<int>() ?? 0) + 100;
            node["SchemaVersion"] = 3;
            return JsonDocument.Parse(node.ToJsonString());
        }
    }

    private sealed class TrackingMigration : ISettingsMigration
    {
        public TrackingMigration(int fromVersion) => FromVersion = fromVersion;
        public int FromVersion { get; }
        public int CallCount { get; private set; }
        public JsonDocument Migrate(JsonDocument raw)
        {
            CallCount++;
            return JsonDocument.Parse(raw.RootElement.GetRawText());
        }
    }
}
