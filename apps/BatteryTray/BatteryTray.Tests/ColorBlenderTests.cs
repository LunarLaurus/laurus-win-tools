using System.Drawing;
using FluentAssertions;
using Xunit;

namespace BatteryTray.Tests;

public class ColorBlenderTests
{
    [Fact]
    public void Lerp_AtZero_ReturnsFirstColor()
    {
        var a = Color.FromArgb(255, 100, 50, 25);
        var b = Color.FromArgb(0,   0,   0,  0);

        var result = ColorBlender.Lerp(a, b, 0);

        result.Should().Be(a);
    }

    [Fact]
    public void Lerp_AtOne_ReturnsSecondColor()
    {
        var a = Color.FromArgb(255, 100, 50, 25);
        var b = Color.FromArgb(0,   0,   0,  0);

        var result = ColorBlender.Lerp(a, b, 1);

        result.Should().Be(b);
    }

    [Fact]
    public void Lerp_AtHalf_ReturnsMidpoint()
    {
        var a = Color.FromArgb(255, 0,   0,   0);
        var b = Color.FromArgb(255, 200, 100, 50);

        var result = ColorBlender.Lerp(a, b, 0.5);

        result.A.Should().Be(255);
        result.R.Should().Be(100);
        result.G.Should().Be(50);
        result.B.Should().Be(25);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(-0.5)]
    public void Lerp_NegativeT_ClampsToFirstColor(double t)
    {
        var a = Color.Red;
        var b = Color.Blue;
        ColorBlender.Lerp(a, b, t).Should().Be(a);
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void Lerp_TGreaterThanOne_ClampsToSecondColor(double t)
    {
        var a = Color.Red;
        var b = Color.Blue;
        ColorBlender.Lerp(a, b, t).Should().Be(b);
    }
}
