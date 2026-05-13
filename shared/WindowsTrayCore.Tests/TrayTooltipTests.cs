using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class TrayTooltipTests
{
    [Fact]
    public void Truncate_ShortText_Unchanged()
    {
        TrayTooltip.Truncate("hello").Should().Be("hello");
    }

    [Fact]
    public void Truncate_ExactlyMaxLength_Unchanged()
    {
        var text = new string('x', TrayTooltip.MaxLength);
        TrayTooltip.Truncate(text).Should().Be(text);
    }

    [Fact]
    public void Truncate_OverMaxLength_CapsAt63()
    {
        var text = new string('a', 100);
        var result = TrayTooltip.Truncate(text);
        result.Length.Should().Be(TrayTooltip.MaxLength);
    }

    [Fact]
    public void Truncate_EmptyString_ReturnsEmpty()
    {
        TrayTooltip.Truncate(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void Truncate_NullCoercedString_HandledGracefully()
    {
        TrayTooltip.Truncate("").Should().BeEmpty();
    }
}
