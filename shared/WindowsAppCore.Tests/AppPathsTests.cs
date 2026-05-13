using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace WindowsAppCore.Tests;

public class AppPathsTests
{
    [Fact]
    public void SettingsDir_Default_ReturnsAppDataSubDir()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TestApp");
        AppPaths.SettingsDir("TestApp").Should().Be(expected);
    }

    [Fact]
    public void LogDir_Default_ReturnsLocalAppDataSubDir()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TestApp", "logs");
        AppPaths.LogDir("TestApp").Should().Be(expected);
    }

    [Fact]
    public void CrashLogPath_ReturnsPathInTempDir()
    {
        var path = AppPaths.CrashLogPath("TestApp");
        Path.GetDirectoryName(path).Should().Be(Path.GetTempPath().TrimEnd('\\', '/'));
        path.Should().EndWith("TestApp-crash.log");
    }

    [Fact]
    public void SettingsDir_WithEnvOverride_UsesOverride()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"apppaths-{Guid.NewGuid():N}");
        try
        {
            Environment.SetEnvironmentVariable("PATHOVERRIDE_DATA", tempRoot);
            AppPaths.SettingsDir("PathOverride").Should().Be(tempRoot);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATHOVERRIDE_DATA", null);
        }
    }

    [Fact]
    public void LogDir_WithEnvOverride_UsesOverridePlusLogs()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"apppaths-{Guid.NewGuid():N}");
        try
        {
            Environment.SetEnvironmentVariable("PATHOVERRIDE_DATA", tempRoot);
            AppPaths.LogDir("PathOverride").Should().Be(Path.Combine(tempRoot, "logs"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATHOVERRIDE_DATA", null);
        }
    }

    [Fact]
    public void CrashLogPath_NotAffectedByEnvOverride()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"apppaths-{Guid.NewGuid():N}");
        try
        {
            Environment.SetEnvironmentVariable("PATHOVERRIDE_DATA", tempRoot);
            // Crash log always goes to %TEMP%, never the per-app override
            AppPaths.CrashLogPath("PathOverride").Should().StartWith(Path.GetTempPath().TrimEnd('\\', '/'));
            AppPaths.CrashLogPath("PathOverride").Should().NotContain(tempRoot);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATHOVERRIDE_DATA", null);
        }
    }

    [Fact]
    public void EnvOverrideKey_DashesBecomesUnderscores()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"apppaths-{Guid.NewGuid():N}");
        try
        {
            Environment.SetEnvironmentVariable("MY_APP_DATA", tempRoot);
            AppPaths.SettingsDir("my-app").Should().Be(tempRoot);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MY_APP_DATA", null);
        }
    }
}
