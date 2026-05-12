using SoundTracker.App;
using SoundTracker.App.Audio;
using SoundTracker.App.Processes;

namespace SoundTracker.SmokeTests;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var tests = new (string Name, Action Run)[]
        {
            ("TooltipFormatter idle state", TooltipFormatter_IdleState),
            ("TooltipFormatter summary and truncation", TooltipFormatter_SummaryAndTruncation),
            ("TooltipFormatter menu label overflow", TooltipFormatter_MenuLabelOverflow),
            ("ProcessNameResolver current process", ProcessNameResolver_CurrentProcess),
            ("AudioSessionMonitor lifecycle", AudioSessionMonitor_Lifecycle),
            ("AudioSessionMonitor disposed guard", AudioSessionMonitor_DisposedGuard),
            ("TrayApplicationContext initial refresh", TrayApplicationContext_InitialRefresh),
            ("TrayApplicationContext event-driven refresh", TrayApplicationContext_EventDrivenRefresh),
            ("TrayApplicationContext error fallback", TrayApplicationContext_ErrorFallback),
            ("TrayApplicationContext owned source disposal", TrayApplicationContext_OwnedSourceDisposal),
        };

        var failures = new List<string>();

        foreach (var (name, run) in tests)
        {
            try
            {
                run();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                failures.Add($"{name}: {ex.Message}");
                Console.WriteLine($"FAIL {name}: {ex}");
            }
        }

        if (failures.Count == 0)
        {
            Console.WriteLine($"Smoke tests passed: {tests.Length}/{tests.Length}");
            return 0;
        }

        Console.Error.WriteLine("Smoke test failures:");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine($"- {failure}");
        }

        return 1;
    }

    private static void TooltipFormatter_IdleState()
    {
        Assert.Equal("Sound Tracker: idle", TooltipFormatter.Build(Array.Empty<string>()));
        Assert.Equal("No active audio sessions", TooltipFormatter.BuildMenuLabel(Array.Empty<string>()));
    }

    private static void TooltipFormatter_SummaryAndTruncation()
    {
        var sessions = new[]
        {
            "extremely-long-process-name-one.exe",
            "extremely-long-process-name-two.exe",
            "extremely-long-process-name-three.exe",
            "extremely-long-process-name-four.exe",
        };

        var tooltip = TooltipFormatter.Build(sessions);
        Assert.True(tooltip.StartsWith("Active audio: "), "Tooltip should use the active-audio prefix.");
        Assert.True(tooltip.Length <= 63, "Tooltip must fit NotifyIcon text limits.");
        Assert.True(tooltip.EndsWith("...") || tooltip.Contains("+1"), "Tooltip should summarize overflow.");
    }

    private static void TooltipFormatter_MenuLabelOverflow()
    {
        var sessions = Enumerable.Range(1, 10).Select(i => $"app{i}.exe").ToArray();
        var label = TooltipFormatter.BuildMenuLabel(sessions);

        Assert.True(label.Contains("app1.exe"), "Menu label should include leading sessions.");
        Assert.True(label.Contains("+2 more"), "Menu label should summarize overflow items.");
    }

    private static void ProcessNameResolver_CurrentProcess()
    {
        var resolver = new ProcessNameResolver();
        var name = resolver.TryGetProcessName((uint)Environment.ProcessId);

        Assert.NotNull(name, "Current process should resolve.");
        Assert.True(name!.EndsWith(".exe", StringComparison.OrdinalIgnoreCase), "Resolved process name should end with .exe.");
        Assert.True(name.Contains("SoundTracker.SmokeTests", StringComparison.OrdinalIgnoreCase), "Resolved name should match the smoke test host.");
    }

    private static void AudioSessionMonitor_Lifecycle()
    {
        using var monitor = new AudioSessionMonitor();
        var sessions = monitor.GetActiveSessionNames();

        Assert.NotNull(sessions, "Monitor should return a session list.");

        var sortedDistinct = sessions
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.SequenceEqual(sortedDistinct, sessions, "Monitor results should already be distinct and sorted.");
    }

    private static void AudioSessionMonitor_DisposedGuard()
    {
        var monitor = new AudioSessionMonitor();
        monitor.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => monitor.GetActiveSessionNames(),
            "Disposed monitor should reject further reads.");
    }

    private static void TrayApplicationContext_InitialRefresh()
    {
        using var source = new FakeAudioSessionSource("vlc.exe", "spotify.exe");
        using var context = new TrayApplicationContext(source, ownsAudioSessionSource: false, showNotifyIcon: false);

        Assert.Equal("Active audio: vlc.exe, spotify.exe", context.CurrentTooltipText);
        Assert.Equal("vlc.exe, spotify.exe", context.CurrentStatusText);
    }

    private static void TrayApplicationContext_EventDrivenRefresh()
    {
        using var source = new FakeAudioSessionSource("vlc.exe");
        using var context = new TrayApplicationContext(source, ownsAudioSessionSource: false, showNotifyIcon: false);

        source.SetSessions("spotify.exe", "vlc.exe", "zebra.exe", "chrome.exe");
        source.RaiseChanged();

        Assert.Equal("Active audio: spotify.exe, vlc.exe, zebra.exe +1", context.CurrentTooltipText);
        Assert.Equal("spotify.exe, vlc.exe, zebra.exe, chrome.exe", context.CurrentStatusText);
    }

    private static void TrayApplicationContext_ErrorFallback()
    {
        using var source = new FakeAudioSessionSource();
        source.ThrowOnGet = true;

        using var context = new TrayApplicationContext(source, ownsAudioSessionSource: false, showNotifyIcon: false);

        Assert.Equal("Sound Tracker: unavailable", context.CurrentTooltipText);
        Assert.Equal("Audio session query failed", context.CurrentStatusText);
    }

    private static void TrayApplicationContext_OwnedSourceDisposal()
    {
        var source = new FakeAudioSessionSource("vlc.exe");
        var context = new TrayApplicationContext(source, ownsAudioSessionSource: true, showNotifyIcon: false);

        context.ShutdownForTests();

        Assert.True(source.DisposeCallCount > 0, "Owned audio sources should be disposed with the tray context.");
    }

    private sealed class FakeAudioSessionSource : IAudioSessionSource
    {
        private IReadOnlyList<string> _sessions;

        public FakeAudioSessionSource(params string[] sessions)
        {
            _sessions = sessions;
        }

        public event EventHandler? SessionsChanged;

        public int DisposeCallCount { get; private set; }

        public bool ThrowOnGet { get; set; }

        public IReadOnlyList<string> GetActiveSessionNames()
        {
            if (ThrowOnGet)
            {
                throw new InvalidOperationException("Synthetic test failure.");
            }

            return _sessions;
        }

        public void Dispose()
        {
            DisposeCallCount++;
        }

        public void RaiseChanged()
        {
            SessionsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetSessions(params string[] sessions)
        {
            _sessions = sessions;
        }
    }

    private static class Assert
    {
        public static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException($"Expected '{expected}' but got '{actual}'.");
            }
        }

        public static void NotNull(object? value, string message)
        {
            if (value is null)
            {
                throw new InvalidOperationException(message);
            }
        }

        public static void SequenceEqual<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string message)
        {
            if (expected.Count != actual.Count)
            {
                throw new InvalidOperationException(message);
            }

            for (var i = 0; i < expected.Count; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(expected[i], actual[i]))
                {
                    throw new InvalidOperationException(message);
                }
            }
        }

        public static void Throws<TException>(Action action, string message)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"{message} Threw {ex.GetType().Name} instead of {typeof(TException).Name}.");
            }

            throw new InvalidOperationException($"{message} No exception was thrown.");
        }

        public static void True(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
