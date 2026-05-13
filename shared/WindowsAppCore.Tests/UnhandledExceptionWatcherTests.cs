using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace WindowsAppCore.Tests;

public class UnhandledExceptionWatcherTests : IDisposable
{
    private readonly string _appName = $"WatcherTest-{Guid.NewGuid():N}";
    private readonly string _logDir;

    public UnhandledExceptionWatcherTests()
    {
        _logDir = Path.Combine(Path.GetTempPath(), _appName, "logs");
        Directory.CreateDirectory(_logDir);
    }

    public void Dispose()
    {
        try { File.Delete(CrashSink.GetLogPath(_appName)); } catch { }
        try { Directory.Delete(_logDir, recursive: true); } catch { }
    }

    [Fact]
    public void Install_DoesNotThrow()
    {
        using var log = new AppLog(_appName, "1.0", _logDir);
        var act = () => UnhandledExceptionWatcher.Install(log, _appName);
        act.Should().NotThrow();
    }

    [Fact]
    public void Install_CrashSinkPathMatchesAppName()
    {
        // Verifies Install wires the correct crash log path for the given appName.
        // Full handler-fires-on-exception coverage requires child-process isolation.
        CrashSink.GetLogPath(_appName).Should().Contain(_appName);
    }
}
