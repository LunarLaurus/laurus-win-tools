using SoundTracker.App;
using SoundTracker.App.Audio;
using WindowsTrayCore;
using SoundTracker.App.History;
using SoundTracker.App.Processes;
using System.Diagnostics;
using System.Drawing;
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
            ("ProcessNameResolver current process", ProcessNameResolver_CurrentProcess),
            ("AudioSessionMonitor lifecycle", AudioSessionMonitor_Lifecycle),
            ("AudioSessionMonitor endpoint volume snapshot", AudioSessionMonitor_EndpointVolumeSnapshot),
            ("ActivityLabelFormatter / inline tooltip composition", InlineTooltip_Multiline),
            ("AudioSessionMonitor disposed guard", AudioSessionMonitor_DisposedGuard),
            ("AudioSessionMonitor live playback callbacks", AudioSessionMonitor_LivePlaybackCallbacks),
            ("AudioActivityTimeline persists live playback history", AudioActivityTimeline_PersistsLivePlaybackHistory),
            ("AudioActivityHistoryStore reloads captured history", AudioActivityHistoryStore_ReloadsCapturedHistory),
            ("RecentActivityForm renders screenshot", RecentActivityForm_RendersScreenshot),
            ("TrayApplicationContext reflects recent history", TrayApplicationContext_ReflectsRecentHistory),
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

    private static void AudioSessionMonitor_EndpointVolumeSnapshot()
    {
        using var monitor = new AudioSessionMonitor();
        var snapshot = monitor.GetEndpointVolume();

        Assert.True(snapshot.IsAvailable, "Endpoint volume snapshot should be available.");
        Assert.True(snapshot.Percent >= 0 && snapshot.Percent <= 100, "Endpoint volume percent should be within 0-100.");
    }

    private static void InlineTooltip_Multiline()
    {
        var volumeSnapshot = new EndpointVolumeSnapshot(IsAvailable: true, IsMuted: false, Percent: 47);
        IReadOnlyList<string> sessions = new[] { "music.exe" };
        IReadOnlyList<AudioActivityEvent> recents = Array.Empty<AudioActivityEvent>();

        var tb = new TrayTooltipBuilder()
            .AddRequired(AppMetadata.TooltipPrefix)
            .AddRequired($"Volume {volumeSnapshot.Percent}%");
        if (sessions.Count > 0)
            tb.AddOptional($"Active: {sessions[0].Replace(".exe", "")}");

        var text = tb.Build();

        if (!text.Contains(AppMetadata.TooltipPrefix))
            throw new Exception($"Tooltip missing app prefix: {text}");
        if (!text.Contains("Volume 47%"))
            throw new Exception($"Tooltip missing volume info: {text}");
        if (!text.Contains("\n"))
            throw new Exception($"Tooltip is not multi-line: {text}");
    }

    private static void AudioSessionMonitor_LivePlaybackCallbacks()
    {
        using var probe = StartLivePlaybackProbe();
        probe.WaitForExit(30000);

        Assert.True(probe.HasExited, "Live playback callback probe should finish within 30 seconds.");
        Assert.Equal(0, probe.ExitCode);
    }

    private static void AudioActivityTimeline_PersistsLivePlaybackHistory()
    {
        var capture = GetPlaybackCapture();

        Assert.True(capture.CapturedActivities.Count >= 2, "Live playback should produce persisted activity records.");
        Assert.True(
            capture.CapturedActivities.Any(activity =>
                activity.Kind == AudioActivityKind.Started &&
                activity.Description.Contains("SoundTracker.SmokeTests", StringComparison.OrdinalIgnoreCase)),
            "Captured history should contain a playback-start event for the smoke test host.");
        Assert.True(
            capture.CapturedActivities.Any(activity =>
                activity.Kind == AudioActivityKind.Stopped &&
                activity.Description.Contains("SoundTracker.SmokeTests", StringComparison.OrdinalIgnoreCase) &&
                activity.Duration is not null &&
                activity.Duration.Value > TimeSpan.Zero),
            "Captured history should contain a playback-stop event with a duration.");
        Assert.True(File.Exists(capture.HistoryPath), "History file should exist after live playback.");
    }

    private static void AudioActivityHistoryStore_ReloadsCapturedHistory()
    {
        var capture = GetPlaybackCapture();
        var reloaded = new AudioActivityHistoryStore(capture.HistoryPath).LoadRecent(100);

        Assert.True(reloaded.Count >= capture.CapturedActivities.Count, "Reloaded history should contain the captured live events.");
        Assert.True(
            reloaded.Any(activity =>
                activity.Kind == AudioActivityKind.Stopped &&
                activity.Description.Contains("SoundTracker.SmokeTests", StringComparison.OrdinalIgnoreCase)),
            "Reloaded history should preserve the playback-stop event.");
    }

    private static void RecentActivityForm_RendersScreenshot()
    {
        var capture = GetPlaybackCapture();

        Assert.True(File.Exists(capture.ScreenshotPath), "Recent Activity screenshot should be created.");
        var fileInfo = new FileInfo(capture.ScreenshotPath);
        Assert.True(fileInfo.Length > 0, "Recent Activity screenshot should not be empty.");
        Assert.True(
            capture.RenderedRows.Any(row => row.Contains("SoundTracker.SmokeTests", StringComparison.OrdinalIgnoreCase)),
            "Rendered Recent Activity rows should include the live playback entry.");
    }

    private static void TrayApplicationContext_ReflectsRecentHistory()
    {
        var capture = GetPlaybackCapture();

        Assert.True(
            capture.TooltipText.StartsWith(AppMetadata.TooltipPrefix, StringComparison.Ordinal),
            "Tray tooltip should include the current application version.");
        Assert.True(
            capture.TooltipText.Contains("\n"),
            "Tray tooltip should be multiline.");
        Assert.True(
            capture.TooltipText.Contains("Volume", StringComparison.OrdinalIgnoreCase) ||
            capture.TooltipText.Contains("Muted", StringComparison.OrdinalIgnoreCase),
            "Tray tooltip should include current volume summary.");
        Assert.True(
            capture.VolumeStatusText.Contains("Volume:", StringComparison.OrdinalIgnoreCase),
            "Tray volume status should reflect live endpoint volume.");
        Assert.True(
            capture.StatusText.Contains("Active now:", StringComparison.OrdinalIgnoreCase),
            "Tray active status should reflect current activity.");
        Assert.True(
            capture.RecentStatusText.Contains("Recent:", StringComparison.OrdinalIgnoreCase) ||
            capture.RecentStatusText.Contains("Recent activity", StringComparison.OrdinalIgnoreCase),
            "Tray recent status should reflect history.");
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

    private static PlaybackCapture GetPlaybackCapture()
    {
        if (_playbackCapture is not null)
        {
            return _playbackCapture;
        }

        var captureRoot = Path.Combine(Path.GetTempPath(), $"sound-tracker-smoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(captureRoot);

        var historyPath = Path.Combine(captureRoot, "audio-activity.jsonl");
        var screenshotPath = Path.Combine(captureRoot, "recent-activity.bmp");

        using var monitor = new AudioSessionMonitor();
        using var timeline = new AudioActivityTimeline(
            monitor,
            new AudioActivityHistoryStore(historyPath),
            maxEventCount: 512);
        using var context = new TrayApplicationContext(
            monitor,
            timeline,
            ownsAudioSessionSource: false,
            ownsActivityTimeline: false,
            showNotifyIcon: false);
        using var child = StartPlaybackChild(3000);

        var capturedSourceName = "SoundTracker.SmokeTests";
        var sawStart = WaitUntil(
            timeout: TimeSpan.FromSeconds(15),
            condition: () => timeline.GetRecentEvents(100).Any(activity =>
                activity.Kind == AudioActivityKind.Started &&
                activity.Description.Contains(capturedSourceName, StringComparison.OrdinalIgnoreCase)));
        Assert.True(sawStart, "Live capture should observe a playback-start history event.");

        child.WaitForExit(15000);
        Assert.True(child.HasExited, "Live capture playback child should exit.");
        Assert.Equal(0, child.ExitCode);

        var sawStop = WaitUntil(
            timeout: TimeSpan.FromSeconds(15),
            condition: () => timeline.GetRecentEvents(100).Any(activity =>
                activity.Kind == AudioActivityKind.Stopped &&
                activity.Description.Contains(capturedSourceName, StringComparison.OrdinalIgnoreCase)));
        Assert.True(sawStop, "Live capture should observe a playback-stop history event.");

        var capturedActivities = timeline
            .GetRecentEvents(100)
            .Where(activity => activity.Description.Contains(capturedSourceName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(activity => activity.TimestampUtc)
            .ToList();

        using var recentActivityForm = new RecentActivityForm();
        recentActivityForm.RefreshEntries(
            activeSessions: Array.Empty<string>(),
            activities: capturedActivities);
        recentActivityForm.Show();
        Application.DoEvents();

        using (var bitmap = new Bitmap(recentActivityForm.ClientSize.Width, recentActivityForm.ClientSize.Height))
        {
            recentActivityForm.DrawToBitmap(bitmap, new Rectangle(Point.Empty, recentActivityForm.ClientSize));
            bitmap.Save(screenshotPath);
        }

        var renderedRows = recentActivityForm.SnapshotRows();
        recentActivityForm.Hide();

        _playbackCapture = new PlaybackCapture(
            HistoryPath: historyPath,
            ScreenshotPath: screenshotPath,
            TooltipText: context.CurrentTooltipText,
            VolumeStatusText: context.CurrentVolumeStatusText,
            StatusText: context.CurrentStatusText,
            RecentStatusText: context.CurrentRecentStatusText,
            CapturedActivities: capturedActivities,
            RenderedRows: renderedRows);
        return _playbackCapture;
    }

    private static PlaybackCapture? _playbackCapture;

    private sealed record PlaybackCapture(
        string HistoryPath,
        string ScreenshotPath,
        string TooltipText,
        string VolumeStatusText,
        string StatusText,
        string RecentStatusText,
        IReadOnlyList<AudioActivityEvent> CapturedActivities,
        IReadOnlyList<string> RenderedRows);

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
