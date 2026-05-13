using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WindowsAppCore;

/// <summary>
/// Stores settings as a human-readable JSON file with atomic saves, corruption
/// quarantine, and a versioned migration chain.
/// </summary>
public sealed class JsonSettingsStore<T> where T : class, new()
{
    private readonly string _settingsFile;
    private readonly Func<T, T>? _normalize;
    private readonly IReadOnlyList<ISettingsMigration> _migrations;
    private readonly JsonSerializerOptions _serOptions;

    public JsonSettingsStore(
        string appName,
        Func<T, T>? normalize = null,
        IEnumerable<ISettingsMigration>? migrations = null,
        JsonSerializerOptions? options = null)
    {
        var dir = AppPaths.SettingsDir(appName);
        _settingsFile = Path.Combine(dir, "settings.json");
        _normalize = normalize;
        _migrations = (migrations ?? Enumerable.Empty<ISettingsMigration>())
            .OrderBy(m => m.FromVersion)
            .ToList();

        // Always write indented; copy caller options so we don't mutate their instance.
        _serOptions = options != null
            ? new JsonSerializerOptions(options) { WriteIndented = true }
            : new JsonSerializerOptions { WriteIndented = true };
    }

    /// <summary>Absolute path of the settings file managed by this store.</summary>
    public string SettingsPath => _settingsFile;

    public T Load()
    {
        if (!File.Exists(_settingsFile))
            return Normalised(new T());

        try
        {
            var json = File.ReadAllText(_settingsFile);
            var migratedJson = ApplyMigrations(json);
            var settings = JsonSerializer.Deserialize<T>(migratedJson, _serOptions);
            if (settings == null)
                return QuarantineAndDefault("deserialized to null");
            return Normalised(settings);
        }
        catch (JsonException ex)
        {
            return QuarantineAndDefault(ex.Message);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return QuarantineAndDefault(ex.Message);
        }
    }

    public void Save(T settings)
    {
        var dir = Path.GetDirectoryName(_settingsFile)!;
        Directory.CreateDirectory(dir);

        var tmp = _settingsFile + ".tmp";
        var json = JsonSerializer.Serialize(settings, _serOptions);
        File.WriteAllText(tmp, json);
        File.Move(tmp, _settingsFile, overwrite: true);
    }

    private string ApplyMigrations(string json)
    {
        if (_migrations.Count == 0) return json;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch { return json; }

        int fileVersion = ReadSchemaVersion(doc);

        foreach (var migration in _migrations.Where(m => m.FromVersion >= fileVersion))
        {
            JsonDocument next;
            try { next = migration.Migrate(doc); }
            catch { doc.Dispose(); return json; }
            doc.Dispose();
            doc = next;
        }

        var result = doc.RootElement.GetRawText();
        doc.Dispose();
        return result;
    }

    private static int ReadSchemaVersion(JsonDocument doc)
    {
        var root = doc.RootElement;
        if (root.TryGetProperty("SchemaVersion", out var v) ||
            root.TryGetProperty("schemaVersion", out v))
        {
            return v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
        }
        return 0;
    }

    private T QuarantineAndDefault(string _)
    {
        try
        {
            if (File.Exists(_settingsFile))
            {
                var stamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Move(_settingsFile, _settingsFile + $".broken-{stamp}", overwrite: false);
            }
        }
        catch { }
        return Normalised(new T());
    }

    private T Normalised(T settings) =>
        _normalize != null ? _normalize(settings) : settings;
}
