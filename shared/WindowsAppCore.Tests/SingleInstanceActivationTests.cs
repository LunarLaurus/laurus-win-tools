using FluentAssertions;
using Xunit;

namespace WindowsAppCore.Tests;

public class SingleInstanceActivationTests
{
    private static string UniqueApp() => $"WACTest-{Guid.NewGuid():N}";

    [WindowsFact]
    public void TryClaim_ReturnsTrue_FirstClaim()
    {
        var app = UniqueApp();
        var result = SingleInstanceActivation.TryClaim(app, null, out var handle);
        try { result.Should().BeTrue(); handle.Should().NotBeNull(); }
        finally { handle?.Dispose(); }
    }

    [WindowsFact]
    public void TryClaim_ReturnsFalse_WhenAlreadyClaimed()
    {
        var app = UniqueApp();
        SingleInstanceActivation.TryClaim(app, null, out var first);
        try
        {
            var result = SingleInstanceActivation.TryClaim(app, null, out var second);
            result.Should().BeFalse();
            second.Should().BeNull();
        }
        finally { first?.Dispose(); }
    }

    [WindowsFact]
    public void TryClaim_ReturnsTrueAgain_AfterDispose()
    {
        var app = UniqueApp();
        SingleInstanceActivation.TryClaim(app, null, out var first);
        first!.Dispose();
        Thread.Sleep(50);

        var result = SingleInstanceActivation.TryClaim(app, null, out var second);
        try { result.Should().BeTrue(); }
        finally { second?.Dispose(); }
    }
}
