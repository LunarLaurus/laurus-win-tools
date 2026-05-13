using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace WindowsAppCore.Tests;

public class CrashSinkTests : IDisposable
{
    // Unique per test run so files don't bleed across tests
    private readonly string _appName = $"CrashTest-{Guid.NewGuid():N}";

    public void Dispose()
    {
        try { File.Delete(CrashSink.GetLogPath(_appName)); } catch { }
    }

    [Fact]
    public void GetLogPath_ReturnsPathInTempDirWithAppName()
    {
        var path = CrashSink.GetLogPath("SomeApp");
        path.Should().StartWith(Path.GetTempPath().TrimEnd('\\', '/'));
        path.Should().EndWith("SomeApp-crash.log");
    }

    [Fact]
    public void Write_CreatesFileContainingExceptionInfo()
    {
        CrashSink.Write(_appName, "test-source", new ArgumentException("test msg"));

        var path = CrashSink.GetLogPath(_appName);
        File.Exists(path).Should().BeTrue();
        var content = File.ReadAllText(path);
        content.Should().Contain("test msg");
        content.Should().Contain("test-source");
    }

    [Fact]
    public void Write_NeverThrows()
    {
        var act = () => CrashSink.Write(_appName, "src", new Exception("x"));
        act.Should().NotThrow();
    }

    [Fact]
    public void Write_MultipleCalls_AppendAllEntries()
    {
        CrashSink.Write(_appName, "s1", new Exception("first"));
        CrashSink.Write(_appName, "s2", new Exception("second"));

        var content = File.ReadAllText(CrashSink.GetLogPath(_appName));
        content.Should().Contain("first");
        content.Should().Contain("second");
    }
}
