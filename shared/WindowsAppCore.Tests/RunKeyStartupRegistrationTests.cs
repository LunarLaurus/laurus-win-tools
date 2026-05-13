using FluentAssertions;
using Xunit;

namespace WindowsAppCore.Tests;

public class RunKeyStartupRegistrationTests
{
    private const string TestKeyPrefix = "WindowsAppCoreTest.RunKey";

    private static RunKeyStartupRegistration Make()
    {
        var name = $"{TestKeyPrefix}.{Guid.NewGuid():N}";
        return new RunKeyStartupRegistration(name, @"C:\test\app.exe", "--startup");
    }

    [WindowsFact]
    public void IsRegistered_False_WhenEntryAbsent()
    {
        Make().IsRegistered.Should().BeFalse();
    }

    [WindowsFact]
    public void Register_ReturnsSuccess_AndCreatesEntry()
    {
        var reg = Make();
        try
        {
            reg.Register().Should().Be(StartupRegistrationResult.Success);
            reg.IsRegistered.Should().BeTrue();
        }
        finally { reg.Unregister(); }
    }

    [WindowsFact]
    public void Unregister_ReturnsSuccess_AndRemovesEntry()
    {
        var reg = Make();
        reg.Register();
        reg.Unregister().Should().Be(StartupRegistrationResult.Success);
        reg.IsRegistered.Should().BeFalse();
    }

    [WindowsFact]
    public void Register_IsIdempotent()
    {
        var reg = Make();
        try
        {
            reg.Register();
            reg.Register().Should().Be(StartupRegistrationResult.Success);
            reg.IsRegistered.Should().BeTrue();
        }
        finally { reg.Unregister(); }
    }

    [WindowsFact]
    public void Unregister_IsIdempotent_WhenEntryAbsent()
    {
        Make().Unregister().Should().Be(StartupRegistrationResult.Success);
    }
}
