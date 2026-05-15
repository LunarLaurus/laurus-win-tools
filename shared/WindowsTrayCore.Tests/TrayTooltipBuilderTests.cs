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

    [Fact]
    public void Build_OverBudget_DropsLastOptional()
    {
        // Three 50-char lines: 50 + 1 + 50 + 1 + 50 = 152. Over 127 by 25.
        var line50 = new string('a', 50);
        var result = new TrayTooltipBuilder()
            .AddRequired(line50)
            .AddOptional(line50)
            .AddOptional(line50)
            .Build();

        // Drop the last optional. 50 + 1 + 50 = 101 chars, under budget.
        result.Should().Be(line50 + "\n" + line50);
        result.Length.Should().Be(101);
    }

    [Fact]
    public void Build_OverBudget_DropsOptionalsInReverseAddOrder()
    {
        // Two 70-char optionals; together with required they overflow.
        // Required (50) + LF + opt1 (70) + LF + opt2 (70) = 192. Over by 65.
        var req = new string('R', 50);
        var opt1 = new string('1', 70);
        var opt2 = new string('2', 70);

        var result = new TrayTooltipBuilder()
            .AddRequired(req)
            .AddOptional(opt1)
            .AddOptional(opt2)
            .Build();

        // After dropping opt2: 50 + 1 + 70 = 121. Under budget.
        result.Should().Be(req + "\n" + opt1);
    }

    [Fact]
    public void Build_OverBudget_DropsAllOptionalsUntilFits()
    {
        // 80-char required + two 30-char optionals.
        // 80 + 1 + 30 + 1 + 30 = 142. Over by 15.
        // Drop opt2: 80 + 1 + 30 = 111. Under. Stop.
        var req = new string('R', 80);
        var opt1 = new string('1', 30);
        var opt2 = new string('2', 30);

        var result = new TrayTooltipBuilder()
            .AddRequired(req)
            .AddOptional(opt1)
            .AddOptional(opt2)
            .Build();

        result.Should().Be(req + "\n" + opt1);
    }

    [Fact]
    public void Build_OverBudget_PreservesRequiredAfterOptionalsDropped()
    {
        // Interleaved: R1, O1, R2, O2.
        // 40 + 1 + 40 + 1 + 40 + 1 + 40 = 163. Over by 36.
        // Drop O2 (the last optional in add order): 40 + 1 + 40 + 1 + 40 = 122. Under.
        var r1 = new string('A', 40);
        var o1 = new string('B', 40);
        var r2 = new string('C', 40);
        var o2 = new string('D', 40);

        var result = new TrayTooltipBuilder()
            .AddRequired(r1)
            .AddOptional(o1)
            .AddRequired(r2)
            .AddOptional(o2)
            .Build();

        // r1, o1, r2 in their original positions, o2 gone.
        result.Should().Be(r1 + "\n" + o1 + "\n" + r2);
    }
}
