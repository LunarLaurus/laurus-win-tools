using System.Text;
using System.Text.Json;
using SoundTracker.App.Audio;
using SoundTracker.App.Diagnostics;

namespace SoundTracker.App.History;

internal sealed class AudioActivityHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);
    private readonly object _sync = new();

    public AudioActivityHistoryStore(string? historyPath = null)
    {
        HistoryPath = historyPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SoundTracker",
            "history",
            "audio-activity.jsonl");

        var directoryPath = Path.GetDirectoryName(HistoryPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }

    public string HistoryPath { get; }

    public IReadOnlyList<AudioActivityEvent> LoadRecent(int maxCount)
    {
        try
        {
            if (!File.Exists(HistoryPath))
            {
                return [];
            }

            List<AudioActivityEvent> loadedEvents = [];
            foreach (var line in File.ReadLines(HistoryPath, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var activity = JsonSerializer.Deserialize<AudioActivityEvent>(line, JsonOptions);
                    if (activity is not null)
                    {
                        loadedEvents.Add(activity);
                    }
                }
                catch (JsonException ex)
                {
                    AppLog.Warn($"history store skipped malformed line error=\"{ex.Message}\"");
                }
            }

            if (loadedEvents.Count <= maxCount)
            {
                return loadedEvents;
            }

            var trimmed = loadedEvents
                .TakeLast(maxCount)
                .ToList();
            Rewrite(trimmed);
            AppLog.Info($"history store trimmed persisted history from {loadedEvents.Count} to {trimmed.Count} events");
            return trimmed;
        }
        catch (Exception ex)
        {
            AppLog.Error("history store failed to load events", ex);
            return [];
        }
    }

    public void Append(AudioActivityEvent activity)
    {
        try
        {
            var jsonLine = JsonSerializer.Serialize(activity, JsonOptions) + Environment.NewLine;
            lock (_sync)
            {
                File.AppendAllText(HistoryPath, jsonLine, Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("history store failed to append event", ex);
        }
    }

    public void Rewrite(IReadOnlyList<AudioActivityEvent> activities)
    {
        try
        {
            var lines = activities
                .Select(activity => JsonSerializer.Serialize(activity, JsonOptions))
                .ToArray();

            lock (_sync)
            {
                File.WriteAllLines(HistoryPath, lines, Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("history store failed to rewrite history", ex);
        }
    }
}
