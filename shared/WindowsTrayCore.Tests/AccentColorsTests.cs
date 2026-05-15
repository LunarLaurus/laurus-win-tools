using System.Drawing;
using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class AccentColorsTests
{
    [Theory]
    [InlineData(0xFF, 0xFF, 0xFF, 1.0)]
    [InlineData(0x00, 0x00, 0x00, 0.0)]
    [InlineData(0x80, 0x80, 0x80, 0.21586)]
    public void Luminance_KnownValues_MatchWCAGFormula(int r, int g, int b, double expected)
    {
        var l = AccentColors.Luminance(Color.FromArgb(r, g, b));
        l.Should().BeApproximately(expected, 0.005);
    }

    [Theory]
    [InlineData(0xFB, 0xBF, 0x24)]
    [InlineData(0xAD, 0xD8, 0xE6)]
    [InlineData(0xFF, 0xFF, 0xFF)]
    public void DeriveOn_LightAccent_ReturnsBlack(int r, int g, int b)
    {
        var on = AccentColors.DeriveOn(Color.FromArgb(r, g, b));
        on.Should().Be(Color.FromArgb(0, 0, 0));
    }

    [Theory]
    [InlineData(0x00, 0x78, 0xD4)]
    [InlineData(0x5A, 0x4F, 0xD4)]
    [InlineData(0x00, 0x00, 0x00)]
    public void DeriveOn_DarkAccent_ReturnsWhite(int r, int g, int b)
    {
        var on = AccentColors.DeriveOn(Color.FromArgb(r, g, b));
        on.Should().Be(Color.FromArgb(0xFF, 0xFF, 0xFF));
    }

    [Fact]
    public void DeriveSubtle_BlendsAccentAt24PercentOverSurface()
    {
        var accent = Color.FromArgb(0xFF, 0, 0);
        var surface = Color.FromArgb(0xFF, 0xFF, 0xFF);

        var subtle = AccentColors.DeriveSubtle(accent, surface);

        subtle.R.Should().Be(0xFF);
        subtle.G.Should().BeInRange((byte)0xC1, (byte)0xC3);
        subtle.B.Should().BeInRange((byte)0xC1, (byte)0xC3);
    }

    [Fact]
    public void DeriveSubtle_AccentEqualSurface_PassesThrough()
    {
        var c = Color.FromArgb(0x80, 0x80, 0x80);
        AccentColors.DeriveSubtle(c, c).Should().Be(c);
    }
}
