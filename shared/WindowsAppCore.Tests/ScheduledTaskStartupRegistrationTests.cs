using FluentAssertions;
using Xunit;

namespace WindowsAppCore.Tests;

public class ScheduledTaskStartupRegistrationTests
{
    private static ScheduledTaskStartupRegistration Make(string? taskName = null) =>
        new(taskName ?? $"WACTest-{Guid.NewGuid():N}", @"C:\test\app.exe");

    [WindowsFact]
    public void IsRegistered_False_ForAbsentTask()
    {
        Make().IsRegistered.Should().BeFalse();
    }

    [Fact]
    public void TryHandleHelperArgs_ReturnsNull_OnEmptyArgs()
    {
        Make().TryHandleHelperArgs([]).Should().BeNull();
    }

    [Fact]
    public void TryHandleHelperArgs_ReturnsNull_OnUnrelatedArgs()
    {
        Make().TryHandleHelperArgs(["--other", "value"]).Should().BeNull();
    }
}
