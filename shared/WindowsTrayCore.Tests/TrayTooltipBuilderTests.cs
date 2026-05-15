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

    [Fact]
    public void Build_TwoRequired_UnderBudget_JoinsWithLF()
    {
        var result = new TrayTooltipBuilder()
            .AddRequired("first line")
            .AddRequired("second line")
            .Build();

        result.Should().Be("first line\nsecond line");
    }

    [Fact]
    public void Build_RequiredAndOptional_UnderBudget_JoinsInAddOrder()
    {
        var result = new TrayTooltipBuilder()
            .AddRequired("required A")
            .AddOptional("optional B")
            .AddRequired("required C")
            .AddOptional("optional D")
            .Build();

        result.Should().Be("required A\noptional B\nrequired C\noptional D");
    }

    [Fact]
    public void Build_OnlyOptionals_UnderBudget_JoinsWithLF()
    {
        var result = new TrayTooltipBuilder()
            .AddOptional("opt1")
            .AddOptional("opt2")
            .Build();

        result.Should().Be("opt1\nopt2");
    }
}
