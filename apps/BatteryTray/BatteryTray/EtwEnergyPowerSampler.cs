using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace BatteryTray;

/// <summary>
/// Top-tier sampler — subscribes to Microsoft-Windows-Kernel-Power's
/// EnergyEstimation events, the same source Task Manager uses for its
/// "Power usage" column.
///
/// Caveats:
/// - Requires admin (kernel ETW session creation).
/// - Has up to ~30s warmup before first event.
/// - On many recent Windows builds the EnergyEstimation engine is OFF by
///   default unless the system is on battery, or even just disabled entirely
///   on builds where Microsoft has shifted to a different telemetry path.
///
/// Watchdog behaviour (added v1.9 after a stuck-warming-up bug):
/// A separate timer-driven check evaluates every few seconds whether we've
/// received ANY events since session start. If we hit the no-events deadline
/// (default 60s) we self-demote IsHealthy=false so the coordinator can
/// promote a lower tier. The earlier implementation only checked at sample
/// computation time, but no events meant no sample computation, meaning
/// the demotion never ran. Now demotion is wallclock-driven.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class EtwEnergyPowerSampler : IProcessPowerSampler
{
    private const string SessionName = "BatteryTrayEnergySession";
    private static readonly Guid KernelPowerProviderGuid =
        new("331c3b3a-2005-44c2-ac5e-77220c37d6b4");

    private const ulong EnergyKeywords = 0x4000_0000UL  // EnergyEstimationGeneric
                                       | 0x0008_0000UL; // EnergyEstimationDetailed

    private readonly TimeSpan _aggregationWindow;
    private readonly TimeSpan _noEventsDeadline;
    private readonly Thread _processThread;
    private readonly Thread _aggregatorThread;
    private readonly Thread _watchdogThread;
    private readonly CancellationTokenSource _cts = new();

    private TraceEventSession? _session;
    private volatile ProcessPowerSample[] _latest = Array.Empty<ProcessPowerSample>();
    private volatile bool _hasSampled;
    private volatile bool _isHealthy = true;
    private volatile bool _sessionStarted;
    private string _statusMessage = "Initializing…";
    private DateTime _startedAtUtc;

    // Counter incremented from any event; the watchdog reads it.
    private long _eventsReceived;

    private readonly object _stateGate = new();
    private readonly Dictionary<int, EnergyState> _state = new();

    private record class EnergyState
    {
        public string Name = "";
        public ulong LastCumulative;
        public DateTime LastSeenUtc;
    }

    public PowerSamplerSource Source => PowerSamplerSource.EtwEnergyEstimation;
    public bool HasFirstSample => _hasSampled;
    public bool IsHealthy => _isHealthy;
    public string StatusMessage
    {
        get { lock (this) return _statusMessage; }
        private set { lock (this) _statusMessage = value; }
    }

    public EtwEnergyPowerSampler(TimeSpan? aggregationWindow = null, TimeSpan? noEventsDeadline = null)
    {
        _aggregationWindow = aggregationWindow ?? TimeSpan.FromSeconds(5);
        _noEventsDeadline = noEventsDeadline ?? TimeSpan.FromSeconds(60);

        _processThread = new Thread(RunSession)
        {
            IsBackground = true,
            Name = "BatteryTray.EtwEnergySampler",
        };
        _aggregatorThread = new Thread(AggregateLoop)
        {
            IsBackground = true,
            Name = "BatteryTray.EtwAggregator",
        };
        _watchdogThread = new Thread(WatchdogLoop)
        {
            IsBackground = true,
            Name = "BatteryTray.EtwWatchdog",
        };

        _processThread.Start();
        _aggregatorThread.Start();
        _watchdogThread.Start();
    }

    public static bool IsAvailable()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    public ProcessPowerSample[] GetLatest() => _latest;

    private void RunSession()
    {
        try
        {
            try { TraceEventSession.GetActiveSession(SessionName)?.Stop(); } catch { }

            _session = new TraceEventSession(SessionName) { StopOnDispose = true };
            _session.EnableProvider(KernelPowerProviderGuid,
                TraceEventLevel.Informational, EnergyKeywords);
            _session.Source.Dynamic.All += OnEvent;

            _isHealthy = true;
            _sessionStarted = true;
            _startedAtUtc = DateTime.UtcNow;
            StatusMessage = "ETW EnergyEstimation (warming up, ~30s for first reading)";

            _session.Source.Process();  // blocks until session stops
        }
        catch (UnauthorizedAccessException)
        {
            _isHealthy = false;
            _sessionStarted = true;  // Mark as "tried" so watchdog doesn't keep saying initializing
            StatusMessage = "ETW unavailable — needs admin (run elevated)";
        }
        catch (Exception ex)
        {
            _isHealthy = false;
            _sessionStarted = true;
            StatusMessage = $"ETW unavailable: {ex.GetType().Name}";
            CrashLogger.Write("EtwEnergy.RunSession", ex);
        }
    }

    private void OnEvent(TraceEvent ev)
    {
        Interlocked.Increment(ref _eventsReceived);

        try
        {
            int? pid = TryReadInt(ev, "ProcessId") ?? TryReadInt(ev, "PID");
            if (pid is null) return;

            ulong? cumulative =
                TryReadUInt64(ev, "EnergyChange") ??
                TryReadUInt64(ev, "TotalEnergy") ??
                TryReadUInt64(ev, "Energy");

            if (cumulative is null) return;

            string name = ev.PayloadByName("ProcessName") as string
                       ?? ev.ProcessName
                       ?? $"PID {pid}";

            lock (_stateGate)
            {
                if (!_state.TryGetValue(pid.Value, out var s))
                {
                    s = new EnergyState { Name = name, LastCumulative = cumulative.Value };
                    _state[pid.Value] = s;
                    return;
                }
                s.Name = name;
                s.LastCumulative = cumulative.Value;
                s.LastSeenUtc = DateTime.UtcNow;
            }
        }
        catch
        {
            // Schema mismatch — skip.
        }
    }

    private static int? TryReadInt(TraceEvent ev, string field)
    {
        try
        {
            var v = ev.PayloadByName(field);
            return v switch
            {
                null => null,
                int i => i,
                uint u => (int)u,
                long l => (int)l,
                _ => int.TryParse(v.ToString(), out var x) ? x : null,
            };
        }
        catch { return null; }
    }

    private static ulong? TryReadUInt64(TraceEvent ev, string field)
    {
        try
        {
            var v = ev.PayloadByName(field);
            return v switch
            {
                null => null,
                ulong u => u,
                long l => l >= 0 ? (ulong)l : null,
                uint ui => ui,
                int i => i >= 0 ? (ulong)i : null,
                _ => ulong.TryParse(v.ToString(), out var x) ? x : null,
            };
        }
        catch { return null; }
    }

    private void AggregateLoop()
    {
        var token = _cts.Token;
        var lastSnapshot = new Dictionary<int, ulong>();

        while (!token.IsCancellationRequested)
        {
            if (token.WaitHandle.WaitOne(_aggregationWindow)) break;

            // CRUCIAL FIX: do not early-exit when state is empty — the watchdog
            // relies on us reaching the demotion check below. Instead just use
            // an empty current-snapshot.
            Dictionary<int, (string Name, ulong Cumulative)> current;
            lock (_stateGate)
            {
                current = new Dictionary<int, (string, ulong)>(_state.Count);
                foreach (var kv in _state)
                    current[kv.Key] = (kv.Value.Name, kv.Value.LastCumulative);
            }

            var elapsedSeconds = _aggregationWindow.TotalSeconds;
            var samples = new List<ProcessPowerSample>(current.Count);

            foreach (var (pid, (name, cum)) in current)
            {
                if (!lastSnapshot.TryGetValue(pid, out var prev))
                {
                    lastSnapshot[pid] = cum;
                    continue;
                }
                if (cum < prev) { lastSnapshot[pid] = cum; continue; }

                var deltaMicrojoules = cum - prev;
                lastSnapshot[pid] = cum;
                if (deltaMicrojoules == 0) continue;

                var microwatts = deltaMicrojoules / elapsedSeconds;
                var watts = microwatts / 1_000_000.0;
                if (watts < 0.01) continue;

                samples.Add(new ProcessPowerSample(pid, name, 0, 0, watts));
            }

            var keysToRemove = lastSnapshot.Keys.Where(k => !current.ContainsKey(k)).ToList();
            foreach (var k in keysToRemove) lastSnapshot.Remove(k);

            if (samples.Count > 0)
            {
                samples.Sort((a, b) => b.EstimatedWatts.CompareTo(a.EstimatedWatts));
                _latest = samples.ToArray();
                _hasSampled = true;
                StatusMessage = "ETW EnergyEstimation (live)";
            }
        }
    }

    /// <summary>
    /// Wallclock-driven self-demotion. Independent of event flow so the
    /// "no events ever arrive" failure mode actually trips the deadline.
    /// </summary>
    private void WatchdogLoop()
    {
        var token = _cts.Token;
        var checkInterval = TimeSpan.FromSeconds(2);

        while (!token.IsCancellationRequested)
        {
            if (token.WaitHandle.WaitOne(checkInterval)) break;

            // Wait until session start has been attempted before judging.
            if (!_sessionStarted) continue;

            // If we've ever produced a sample, we're fine — no demotion needed.
            if (_hasSampled) continue;

            // If we're already unhealthy (RunSession threw), stay there.
            if (!_isHealthy) continue;

            var elapsed = DateTime.UtcNow - _startedAtUtc;
            if (elapsed < _noEventsDeadline) continue;

            var events = Interlocked.Read(ref _eventsReceived);
            if (events == 0)
            {
                _isHealthy = false;
                StatusMessage =
                    $"ETW EnergyEstimation produced no events after {(int)_noEventsDeadline.TotalSeconds}s. " +
                    "The provider is registered but inactive on this build — common on Win10 1903+ desktops " +
                    "and any system where the EnergyEstimation engine is suppressed.";
                return;
            }
            else
            {
                // Events arrived but no usable sample yet. Some builds emit events
                // we can't parse; demote rather than claim healthy forever.
                _isHealthy = false;
                StatusMessage =
                    $"ETW EnergyEstimation received {events} events in {(int)elapsed.TotalSeconds}s but " +
                    "couldn't extract usable per-process energy data. Schema may not match this Windows build.";
                return;
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _session?.Stop(); } catch { }
        try { _session?.Dispose(); } catch { }
        try { _processThread.Join(TimeSpan.FromSeconds(3)); } catch { }
        try { _aggregatorThread.Join(TimeSpan.FromSeconds(2)); } catch { }
        try { _watchdogThread.Join(TimeSpan.FromSeconds(2)); } catch { }
        _cts.Dispose();
    }
}
