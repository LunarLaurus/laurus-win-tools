using FluentAssertions;
using Xunit;

namespace WindowsAppCore.Tests;

public class StartupOptionsTests
{
    [Fact]
    public void Parse_EmptyArgs_ReturnsDefaults()
    {
        var opts = StartupOptions.Parse([]);
        opts.IsStartupLaunch.Should().BeFalse();
        opts.DelaySeconds.Should().Be(0);
    }

    [Fact]
    public void Parse_Startup_SetsFlag()
    {
        StartupOptions.Parse(["--startup"]).IsStartupLaunch.Should().BeTrue();
    }

    [Theory]
    [InlineData("--STARTUP")]
    [InlineData("--Startup")]
    public void Parse_Startup_IsCaseInsensitive(string arg)
    {
        StartupOptions.Parse([arg]).IsStartupLaunch.Should().BeTrue();
    }

    [Fact]
    public void Parse_Delay_SetsSeconds()
    {
        StartupOptions.Parse(["--delay=10"]).DelaySeconds.Should().Be(10);
    }

    [Fact]
    public void Parse_Delay_ClampsToMax300()
    {
        StartupOptions.Parse(["--delay=999"]).DelaySeconds.Should().Be(300);
    }

    [Fact]
    public void Parse_Delay_ClampsNegativeToZero()
    {
        StartupOptions.Parse(["--delay=-5"]).DelaySeconds.Should().Be(0);
    }

    [Fact]
    public void Parse_Delay_IgnoresNonNumeric()
    {
        StartupOptions.Parse(["--delay=abc"]).DelaySeconds.Should().Be(0);
    }

    [Theory]
    [InlineData("--DELAY=5")]
    [InlineData("--Delay=5")]
    public void Parse_Delay_IsCaseInsensitive(string arg)
    {
        StartupOptions.Parse([arg]).DelaySeconds.Should().Be(5);
    }

    [Fact]
    public void Parse_UnknownArgs_AreIgnored()
    {
        var opts = StartupOptions.Parse(["--unknown", "--safe-mode", "--rehide=12345"]);
        opts.IsStartupLaunch.Should().BeFalse();
        opts.DelaySeconds.Should().Be(0);
    }

    [Fact]
    public void Parse_MultipleArgs_AllParsed()
    {
        var opts = StartupOptions.Parse(["--startup", "--delay=7"]);
        opts.IsStartupLaunch.Should().BeTrue();
        opts.DelaySeconds.Should().Be(7);
    }
}
