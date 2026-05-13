using FluentAssertions;
using Xunit;

namespace BatteryTray.Tests;

public class TimeFormatTests
{
    [Theory]
    [InlineData(0,     "—")]
    [InlineData(-1,    "—")]
    [InlineData(-3600, "—")]
    public void NonPositive_ReturnsDash(int seconds, string expected)
    {
        TimeFormat.Duration(seconds).Should().Be(expected);
    }

    [Theory]
    [InlineData(30,   "0m")]    // less than a minute rounds down to 0m, not "30s"
    [InlineData(60,   "1m")]
    [InlineData(599,  "9m")]
    [InlineData(3540, "59m")]
    public void UnderHour_ReturnsMinutes(int seconds, string expected)
    {
        TimeFormat.Duration(seconds).Should().Be(expected);
    }

    [Theory]
    [InlineData(3600,   "1h 00m")]
    [InlineData(3660,   "1h 01m")]
    [InlineData(7800,   "2h 10m")]
    [InlineData(86399,  "23h 59m")]
    public void HourOrMore_ReturnsHoursAndPaddedMinutes(int seconds, string expected)
    {
        TimeFormat.Duration(seconds).Should().Be(expected);
    }

    [Fact]
    public void OverTwentyFourHours_GetsCapped()
    {
        TimeFormat.Duration(86400).Should().Be(">24h");
        TimeFormat.Duration(999999).Should().Be(">24h");
    }
}
