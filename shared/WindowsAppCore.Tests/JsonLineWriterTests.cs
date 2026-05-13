using System;
using System.IO;
using System.Threading;
using FluentAssertions;
using Xunit;

namespace WindowsAppCore.Tests;

public class JsonLineWriterTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), $"jlw-{Guid.NewGuid():N}");

    public JsonLineWriterTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Dispose_FlushesAllWrittenLines()
    {
        string path;
        using (var writer = new JsonLineWriter(_tempDir, "test"))
        {
            writer.Write("{\"a\":1}");
            writer.Write("{\"a\":2}");
            path = writer.CurrentPath;
        }

        var lines = File.ReadAllLines(path);
        lines.Should().HaveCount(2);
        lines[0].Should().Be("{\"a\":1}");
        lines[1].Should().Be("{\"a\":2}");
    }

    [Fact]
    public void CurrentPath_ContainsPrefixAndTodaysDate()
    {
        using var writer = new JsonLineWriter(_tempDir, "myapp");
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        writer.CurrentPath.Should().Contain($"myapp-{date}");
        writer.CurrentPath.Should().EndWith(".jsonl");
    }

    [Fact]
    public void Write_EventuallyAppearsOnDisk()
    {
        string path;
        using (var writer = new JsonLineWriter(_tempDir, "flush"))
        {
            writer.Write("{\"evt\":\"test\"}");
            path = writer.CurrentPath;

            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (!File.Exists(path) && DateTime.UtcNow < deadline)
                Thread.Sleep(50);

            File.Exists(path).Should().BeTrue("drain thread should flush within 3 seconds");
        }
    }

    [Fact]
    public void Write_ManyLines_AllPresentAfterDispose()
    {
        string path;
        using (var writer = new JsonLineWriter(_tempDir, "bulk"))
        {
            for (int i = 0; i < 20; i++)
                writer.Write($"{{\"n\":{i}}}");
            path = writer.CurrentPath;
        }

        File.ReadAllLines(path).Should().HaveCount(20);
    }

    [Fact]
    public void SizeCap_ExceededFile_RollsToNumberedFile()
    {
        // 64 bytes per line, cap at 256 bytes → should roll after ~4 lines
        string path;
        using (var writer = new JsonLineWriter(_tempDir, "roll", maxSizeBytes: 256))
        {
            var line = new string('x', 60);
            for (int i = 0; i < 10; i++)
                writer.Write($"{{\"d\":\"{line}\"}}");
            path = writer.CurrentPath;
        }

        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var rolled = Directory.GetFiles(_tempDir, $"roll-{date}-*.jsonl");
        rolled.Should().NotBeEmpty("size cap should have triggered at least one roll");
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var writer = new JsonLineWriter(_tempDir, "idem");
        writer.Write("{\"x\":1}");
        writer.Dispose();
        var act = () => writer.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void DateRollover_WritesToNewFileAtMidnight()
    {
        var d1 = new DateTimeOffset(2025, 1, 14, 23, 59, 59, TimeSpan.Zero);
        var d2 = d1.AddSeconds(2);
        var clock = new FakeClock(d1);
        var d1Path = Path.Combine(_tempDir, $"dayroll-{d1:yyyyMMdd}.jsonl");
        var d2Path = Path.Combine(_tempDir, $"dayroll-{d2:yyyyMMdd}.jsonl");

        using (var writer = new JsonLineWriter(_tempDir, "dayroll", clock: clock))
        {
            writer.Write("{\"day\":1}");

            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (!File.Exists(d1Path) && DateTime.UtcNow < deadline)
                Thread.Sleep(50);

            clock.Set(d2);
            writer.Write("{\"day\":2}");
        }

        File.ReadAllLines(d1Path).Should().ContainSingle(l => l.Contains("\"day\":1"));
        File.ReadAllLines(d2Path).Should().ContainSingle(l => l.Contains("\"day\":2"));
    }
}
