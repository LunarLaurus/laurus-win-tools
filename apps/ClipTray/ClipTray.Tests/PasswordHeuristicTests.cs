using FluentAssertions;
using Xunit;

namespace ClipTray.Tests;

public class PasswordHeuristicTests
{
    [Theory]
    [InlineData("abc123XYZ!")]
    [InlineData("hunter2hunter")]
    [InlineData("a1b2c3d4e5")]
    [InlineData("Qb2!Lp7$Mz3")]
    [InlineData("base64==WithPad")]
    [InlineData("0123456789abcdef")]
    public void LooksLikeSecret_KnownPasswords_ReturnsTrue(string text)
    {
        PasswordHeuristic.LooksLikeSecret(text).Should().BeTrue();
    }

    [Theory]
    [InlineData("hello world")]
    [InlineData("CAFEDEADBEEF")]
    [InlineData("12345678")]
    [InlineData("a")]
    [InlineData("password")]
    [InlineData("the quick brown fox")]
    public void LooksLikeSecret_NonPasswords_ReturnsFalse(string text)
    {
        PasswordHeuristic.LooksLikeSecret(text).Should().BeFalse();
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void LooksLikeSecret_LengthBoundaries_RespectsDefaults(int length, bool expected)
    {
        var text = new string('A', length / 2) + new string('1', length - length / 2);
        PasswordHeuristic.LooksLikeSecret(text).Should().Be(expected);
    }

    [Fact]
    public void LooksLikeSecret_CustomBounds_Respected()
    {
        PasswordHeuristic.LooksLikeSecret("abc12345XY", minLen: 12, maxLen: 32).Should().BeFalse();
        PasswordHeuristic.LooksLikeSecret("abc12345XY").Should().BeTrue();
    }
}
