using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace WindowsAppCore.Tests;

public class AppLogTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), $"applog-{Guid.NewGuid():N}");

    public AppLogTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Info_WritesJsonlEnvelopeWithCorrectFields()
    {
        string path;
        using (var log = new AppLog("test-app", "1.2.3", _tempDir))
        {
            log.Info("session.started", new { key = "value" });
            path = log.LogPath;
        }

        var lines = File.ReadAllLines(path);
        lines.Should().HaveCount(1);

        var obj = JsonDocument.Parse(lines[0]).RootElement;
        obj.GetProperty("app").GetString().Should().Be("test-app");
        obj.GetProperty("v").GetString().Should().Be("1.2.3");
        obj.GetProperty("evt").GetString().Should().Be("session.started");
        obj.GetProperty("level").GetString().Should().Be("info");
        obj.TryGetProperty("ts", out _).Should().BeTrue("timestamp field must be present");
    }

    [Fact]
    public void Warn_LevelIsWarn()
    {
        string path;
        using (var log = new AppLog("test-app", "1.0.0", _tempDir))
        {
            log.Warn("something.odd");
            path = log.LogPath;
        }

        var obj = JsonDocument.Parse(File.ReadAllLines(path)[0]).RootElement;
        obj.GetProperty("level").GetString().Should().Be("warn");
    }

    [Fact]
    public void Error_IncludesExceptionTypeAndMessage()
    {
        string path;
        using (var log = new AppLog("test-app", "1.0.0", _tempDir))
        {
            log.Error("thing.broke", new InvalidOperationException("boom"));
            path = log.LogPath;
        }

        var line = File.ReadAllLines(path)[0];
        line.Should().Contain("InvalidOperationException");
        line.Should().Contain("boom");
    }

    [Fact]
    public void LogPath_PointsToTodaysJsonlFile()
    {
        using var log = new AppLog("test-app", "1.0.0", _tempDir);
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        log.LogPath.Should().Contain(date);
        log.LogPath.Should().EndWith(".jsonl");
    }

    [Fact]
    public void MultipleEntries_AllPresentAfterDispose()
    {
        string path;
        using (var log = new AppLog("test-app", "1.0.0", _tempDir))
        {
            log.Info("a");
            log.Warn("b");
            log.Error("c", new Exception("oops"));
            path = log.LogPath;
        }

        File.ReadAllLines(path).Should().HaveCount(3);
    }

    [Fact]
    public void Ts_IsIso8601Utc()
    {
        string path;
        using (var log = new AppLog("test-app", "1.0.0", _tempDir))
        {
            log.Info("tick");
            path = log.LogPath;
        }

        var obj = JsonDocument.Parse(File.ReadAllLines(path)[0]).RootElement;
        var ts = obj.GetProperty("ts").GetString()!;
        DateTimeOffset.TryParse(ts, out var parsed).Should().BeTrue("ts must be parseable");
        parsed.Offset.Should().Be(TimeSpan.Zero, "ts must be UTC (Z suffix)");
    }
}
