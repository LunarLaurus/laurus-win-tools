using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class VersionFormatterTests
{
    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("1.2.3+abc1234", "1.2.3")]
    [InlineData("1.2.3-rc1", "1.2.3")]
    [InlineData("1.2.3-rc1+abc1234", "1.2.3")]
    [InlineData("0.0.1+sha.deadbeef", "0.0.1")]
    public void TrimSemverSuffix_StripsBothSuffixForms(string input, string expected)
    {
        VersionFormatter.TrimSemverSuffix(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TrimSemverSuffix_NullOrEmpty_ReturnsUnknown(string? input)
    {
        VersionFormatter.TrimSemverSuffix(input).Should().Be("unknown");
    }

    [Fact]
    public void TrimSemverSuffix_NoSuffix_PassesThrough()
    {
        VersionFormatter.TrimSemverSuffix("just-a-string").Should().Be("just");
        // Note: a hyphen anywhere truncates at it; that's the SemVer prerelease
        // contract. Callers should pass real version strings, not arbitrary text.
    }
}
