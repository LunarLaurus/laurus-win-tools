namespace BatteryTray;

public readonly record struct RateSample(DateTime At, int RateMilliwatts, int Percent);

/// <summary>
/// Simple bounded ring buffer for the last hour of charge-rate samples.
/// Used by the future history graph in the info dialog. In-memory only —
/// no disk persistence, so a restart wipes it.
/// </summary>
public sealed class RateHistory
{
    private readonly Queue<RateSample> _samples = new();
    private readonly object _gate = new();

    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    public void Record(int? rateMilliwatts, int percent)
    {
        if (rateMilliwatts is not int rate) return;

        lock (_gate)
        {
            var now = DateTime.UtcNow;
            _samples.Enqueue(new RateSample(now, rate, percent));

            // Drop anything older than the rolling window.
            while (_samples.Count > 0 && now - _samples.Peek().At > Window)
            {
                _samples.Dequeue();
            }
        }
    }

    public RateSample[] Snapshot()
    {
        lock (_gate) return _samples.ToArray();
    }

    public bool HasData
    {
        get { lock (_gate) return _samples.Count > 1; }
    }

    public void Clear()
    {
        lock (_gate) _samples.Clear();
    }
}
