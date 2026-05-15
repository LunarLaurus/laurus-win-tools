using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class TrayTooltipBuilderTests
{
    [Fact]
    public void MaxLength_Is127()
    {
        TrayTooltipBuilder.MaxLength.Should().Be(127);
    }

    [Fact]
    public void Build_NoLines_ReturnsEmpty()
    {
        new TrayTooltipBuilder().Build().Should().BeEmpty();
    }

    [Fact]
    public void Build_SingleRequired_PassesThrough()
    {
        var result = new TrayTooltipBuilder()
            .AddRequired("hello")
            .Build();

        result.Should().Be("hello");
    }

    [Fact]
    public void Build_SingleOptional_PassesThrough()
    {
        var result = new TrayTooltipBuilder()
            .AddOptional("hello")
            .Build();

        result.Should().Be("hello");
    }
}
