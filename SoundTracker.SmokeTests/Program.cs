using SoundTracker.App;
using SoundTracker.App.Audio;
using SoundTracker.App.Processes;
using System.Diagnostics;
using System.Media;
using System.Text;

namespace SoundTracker.SmokeTests;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0)
        {
            return RunChildMode(args);
        }

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
            ("AudioSessionMonitor live playback callbacks", AudioSessionMonitor_LivePlaybackCallbacks),
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

    private static int RunChildMode(string[] args)
    {
        return args[0] switch
        {
            "--play-test-wave" => PlayTestWave(args),
            "--probe-live-playback-callbacks" => ProbeLivePlaybackCallbacks(),
            _ => 2,
        };
    }

    private static int PlayTestWave(string[] args)
    {
        var durationMs = args.Length > 1 && int.TryParse(args[1], out var parsedDuration)
            ? parsedDuration
            : 3000;

        var wavePath = Path.Combine(Path.GetTempPath(), $"sound-tracker-smoke-{Guid.NewGuid():N}.wav");

        try
        {
            WriteSineWaveFile(wavePath, durationMs, sampleRate: 44100, frequencyHz: 440.0, amplitude: 0.25);
            Thread.Sleep(500);

            using var player = new SoundPlayer(wavePath);
            player.Load();
            player.PlaySync();

            Thread.Sleep(500);
            return 0;
        }
        finally
        {
            try
            {
                if (File.Exists(wavePath))
                {
                    File.Delete(wavePath);
                }
            }
            catch
            {
            }
        }
    }

    private static void TooltipFormatter_IdleState()
    {
        Assert.Equal("SoundTracker 0.2.2: idle", TooltipFormatter.Build(Array.Empty<string>()));
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
        Assert.True(tooltip.StartsWith("SoundTracker 0.2.2: "), "Tooltip should include the application version.");
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

    private static void AudioSessionMonitor_LivePlaybackCallbacks()
    {
        using var probe = StartLivePlaybackProbe();
        probe.WaitForExit(30000);

        Assert.True(probe.HasExited, "Live playback callback probe should finish within 30 seconds.");
        Assert.Equal(0, probe.ExitCode);
    }

    private static void TrayApplicationContext_InitialRefresh()
    {
        using var source = new FakeAudioSessionSource("vlc.exe", "spotify.exe");
        using var context = new TrayApplicationContext(source, ownsAudioSessionSource: false, showNotifyIcon: false);

        Assert.Equal("SoundTracker 0.2.2: vlc.exe, spotify.exe", context.CurrentTooltipText);
        Assert.Equal("vlc.exe, spotify.exe", context.CurrentStatusText);
    }

    private static void TrayApplicationContext_EventDrivenRefresh()
    {
        using var source = new FakeAudioSessionSource("vlc.exe");
        using var context = new TrayApplicationContext(source, ownsAudioSessionSource: false, showNotifyIcon: false);

        source.SetSessions("spotify.exe", "vlc.exe", "zebra.exe", "chrome.exe");
        source.RaiseChanged();

        Assert.Equal("SoundTracker 0.2.2: spotify.exe, vlc.exe, zebra.exe +1", context.CurrentTooltipText);
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

    private static Process StartPlaybackChild(int durationMs)
    {
        var exePath = Environment.ProcessPath ?? throw new InvalidOperationException("Current process path is unavailable.");
        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = $"--play-test-wave {durationMs}",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start playback child process.");
    }

    private static Process StartLivePlaybackProbe()
    {
        var exePath = Environment.ProcessPath ?? throw new InvalidOperationException("Current process path is unavailable.");
        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = "--probe-live-playback-callbacks",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start live playback callback probe.");
    }

    private static bool WaitUntil(TimeSpan timeout, Func<bool> condition)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < timeout)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(100);
        }

        return condition();
    }

    private static void WriteSineWaveFile(string path, int durationMs, int sampleRate, double frequencyHz, double amplitude)
    {
        var totalSamples = (int)((long)sampleRate * durationMs / 1000);
        var bytesPerSample = 2;
        var channelCount = 1;
        var dataLength = totalSamples * bytesPerSample * channelCount;

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataLength);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channelCount);
        writer.Write(sampleRate);
        writer.Write(sampleRate * bytesPerSample * channelCount);
        writer.Write((short)(bytesPerSample * channelCount));
        writer.Write((short)(bytesPerSample * 8));
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);

        for (var sampleIndex = 0; sampleIndex < totalSamples; sampleIndex++)
        {
            var sample = Math.Sin(2 * Math.PI * frequencyHz * sampleIndex / sampleRate);
            var scaled = (short)(sample * short.MaxValue * amplitude);
            writer.Write(scaled);
        }
    }

    private static int ProbeLivePlaybackCallbacks()
    {
        var monitor = new AudioSessionMonitor();
        var changeCount = 0;

        void HandleSessionsChanged(object? sender, EventArgs e) => Interlocked.Increment(ref changeCount);

        monitor.SessionsChanged += HandleSessionsChanged;

        try
        {
            using var child = StartPlaybackChild(3000);

            var sawStartEvent = WaitUntil(
                timeout: TimeSpan.FromSeconds(12),
                condition: () => Volatile.Read(ref changeCount) > 0);
            if (!sawStartEvent)
            {
                Console.Error.WriteLine("Live callback probe did not observe a playback-start callback.");
                return 1;
            }

            child.WaitForExit(15000);
            if (!child.HasExited || child.ExitCode != 0)
            {
                Console.Error.WriteLine("Live callback probe playback child did not exit cleanly.");
                return 1;
            }

            var sawStopEvent = WaitUntil(
                timeout: TimeSpan.FromSeconds(12),
                condition: () => Volatile.Read(ref changeCount) > 1);
            if (!sawStopEvent)
            {
                Console.Error.WriteLine("Live callback probe did not observe a playback-stop callback.");
                return 1;
            }

            return 0;
        }
        finally
        {
            monitor.SessionsChanged -= HandleSessionsChanged;
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
