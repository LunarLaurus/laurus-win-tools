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

    [Fact]
    public void Build_RequiredOverflowsByItself_WordTruncatesLast()
    {
        const string text = "BatteryTray version 1.0.0 status charging at 47 percent with 3 hours 22 minutes remaining and Battery Saver active running on Asus ZenBook laptop";
        text.Length.Should().BeGreaterThan(127); // sanity

        var result = new TrayTooltipBuilder()
            .AddRequired(text)
            .Build();

        result.Length.Should().BeLessOrEqualTo(127);
        result.Should().EndWith(TrayTooltipBuilder.Ellipsis);
        var beforeEllipsis = result[..^TrayTooltipBuilder.Ellipsis.Length];
        beforeEllipsis.Should().NotEndWith(" ");
    }

    [Fact]
    public void Build_MultipleRequiredOverflow_TruncatesOnlyLast()
    {
        var r1 = new string('A', 40);
        var r2 = new string('B', 40);
        var r3 = "word " + new string('C', 75);

        var result = new TrayTooltipBuilder()
            .AddRequired(r1)
            .AddRequired(r2)
            .AddRequired(r3)
            .Build();

        result.Length.Should().BeLessOrEqualTo(127);
        result.Should().StartWith(r1 + "\n" + r2 + "\n");
        result.Should().EndWith(TrayTooltipBuilder.Ellipsis);
    }

    [Fact]
    public void Build_RequiredOverflow_KeepsWordBoundaryWhenAboveHalfBudget()
    {
        var parts = System.Linq.Enumerable.Range(0, 10)
            .Select(i => $"word{i:D2}xx{new string('y', 12)}");
        var text = string.Join(' ', parts);
        text.Length.Should().BeGreaterThan(127);

        var result = new TrayTooltipBuilder()
            .AddRequired(text)
            .Build();

        result.Should().EndWith(TrayTooltipBuilder.Ellipsis);
        var beforeEllipsis = result[..^TrayTooltipBuilder.Ellipsis.Length];
        beforeEllipsis.Should().NotEndWith(" ");
        beforeEllipsis.TrimEnd().Length.Should().Be(beforeEllipsis.Length);
    }

    [Fact]
    public void Build_RequiredOverflow_NoUsefulWordBoundary_HardCuts()
    {
        var text = new string('x', 200);

        var result = new TrayTooltipBuilder()
            .AddRequired(text)
            .Build();

        result.Length.Should().BeLessOrEqualTo(127);
        result.Should().EndWith(TrayTooltipBuilder.Ellipsis);
        var beforeEllipsis = result[..^TrayTooltipBuilder.Ellipsis.Length];
        beforeEllipsis.All(c => c == 'x').Should().BeTrue();
    }

    [Fact]
    public void Build_NormalisesCRLFToLF()
    {
        var result = new TrayTooltipBuilder()
            .AddRequired("first\r\nsecond")
            .Build();

        result.Should().Be("first\nsecond");
        result.Should().NotContain("\r");
    }

    [Fact]
    public void Build_NormalisesLoneCRToLF()
    {
        var result = new TrayTooltipBuilder()
            .AddRequired("first\rsecond")
            .Build();

        result.Should().Be("first\nsecond");
    }

    [Fact]
    public void Build_PreJoinedMultilineString_BecomesMultipleLogicalLines()
    {
        var resultA = new TrayTooltipBuilder()
            .AddRequired("L1\nL2\nL3")
            .Build();

        var resultB = new TrayTooltipBuilder()
            .AddRequired("L1")
            .AddRequired("L2")
            .AddRequired("L3")
            .Build();

        resultA.Should().Be(resultB);
    }

    [Fact]
    public void Build_DropsWhitespaceOnlyLines()
    {
        var result = new TrayTooltipBuilder()
            .AddRequired("alpha")
            .AddRequired("   ")
            .AddRequired("")
            .AddRequired("beta")
            .Build();

        result.Should().Be("alpha\nbeta");
    }

    [Fact]
    public void Build_NeverStartsOrEndsWithNewline()
    {
        var result = new TrayTooltipBuilder()
            .AddRequired("\nleading\n")
            .AddOptional("\ntrailing\n")
            .Build();

        result.Should().NotStartWith("\n");
        result.Should().NotEndWith("\n");
    }

    [Fact]
    public void Build_PreJoinedRequiredOverflow_FragmentsKeepTagging()
    {
        // 50 + 1 + 50 + 1 + 50 = 152
        var fragment = new string('x', 50);
        var triple = $"{fragment}\n{fragment}\n{fragment}";

        var result = new TrayTooltipBuilder()
            .AddRequired(triple)
            .Build();

        result.Length.Should().BeLessOrEqualTo(127);
        result.Should().EndWith(TrayTooltipBuilder.Ellipsis);
    }
}
