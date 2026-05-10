using System.Text.Json;

namespace ProgramHider;

// Appends structured JSONL events for runtime diagnostics and smoke-test
// evidence without taking the tray process down if logging fails.
internal sealed class AppLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly string _logDirectory;

    public AppLogger()
    {
        var appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _logDirectory = Path.Combine(appDataDirectory, "ProgramHider", "logs");
    }

    public string LogDirectory => _logDirectory;

    public void Write(string eventName, object payload)
    {
        try
        {
            Directory.CreateDirectory(_logDirectory);
            var envelope = new
            {
                ts = DateTimeOffset.UtcNow,
                evt = eventName,
                data = payload
            };
            var line = JsonSerializer.Serialize(envelope, JsonOptions);
            var logPath = Path.Combine(_logDirectory, $"program-hider-{DateTime.UtcNow:yyyyMMdd}.jsonl");
            File.AppendAllText(logPath, line + Environment.NewLine);
        }
        catch
        {
            // Logging must never take the tray app down.
        }
    }
}
