using SoundTracker.App.Audio;
using SoundTracker.App.Diagnostics;

namespace SoundTracker.App.History;

internal sealed class AudioActivityTimeline : IDisposable
{
    private readonly object _sync = new();
    private readonly IAudioSessionSource _audioSessionSource;
    private readonly AudioActivityHistoryStore _historyStore;
    private readonly int _maxEventCount;
    private readonly List<AudioActivityEvent> _events;
    private bool _disposed;

    public AudioActivityTimeline(
        IAudioSessionSource audioSessionSource,
        AudioActivityHistoryStore? historyStore = null,
        int maxEventCount = 5000)
    {
        _audioSessionSource = audioSessionSource;
        _historyStore = historyStore ?? new AudioActivityHistoryStore();
        _maxEventCount = maxEventCount;
        _events = _historyStore.LoadRecent(maxEventCount).ToList();

        AppLog.Info($"activity timeline loaded count={_events.Count} path={_historyStore.HistoryPath}");

        _audioSessionSource.ActivityRecorded += HandleActivityRecorded;
        SeedFromSource(_audioSessionSource.GetRecentActivities());
    }

    public event EventHandler? HistoryChanged;

    public string HistoryPath => _historyStore.HistoryPath;

    public AudioActivityEvent? GetLatestEvent()
    {
        lock (_sync)
        {
            return _events.Count == 0
                ? null
                : _events[^1];
        }
    }

    public IReadOnlyList<AudioActivityEvent> GetRecentEvents(int maxCount)
    {
        lock (_sync)
        {
            return _events
                .OrderByDescending(activity => activity.TimestampUtc)
                .Take(maxCount)
                .ToList();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _audioSessionSource.ActivityRecorded -= HandleActivityRecorded;
        GC.SuppressFinalize(this);
    }

    private void SeedFromSource(IReadOnlyList<AudioActivityEvent> activities)
    {
        foreach (var activity in activities.OrderBy(activity => activity.TimestampUtc))
        {
            AppendActivity(activity);
        }
    }

    private void HandleActivityRecorded(object? sender, AudioActivityEventArgs args)
    {
        AppendActivity(args.Activity);
    }

    private void AppendActivity(AudioActivityEvent activity)
    {
        IReadOnlyList<AudioActivityEvent>? rewrittenEvents = null;

        lock (_sync)
        {
            if (_disposed || IsDuplicateLocked(activity))
            {
                return;
            }

            _events.Add(activity);
            if (_events.Count > _maxEventCount)
            {
                _events.RemoveRange(0, _events.Count - _maxEventCount);
                rewrittenEvents = _events.ToList();
            }
        }

        _historyStore.Append(activity);
        if (rewrittenEvents is not null)
        {
            _historyStore.Rewrite(rewrittenEvents);
        }

        AppLog.Info($"activity timeline appended kind={activity.Kind} description=\"{activity.Description}\"");
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool IsDuplicateLocked(AudioActivityEvent activity)
    {
        var comparisonCount = Math.Min(8, _events.Count);
        for (var index = _events.Count - comparisonCount; index < _events.Count; index++)
        {
            if (index >= 0 && _events[index] == activity)
            {
                return true;
            }
        }

        return false;
    }
}
