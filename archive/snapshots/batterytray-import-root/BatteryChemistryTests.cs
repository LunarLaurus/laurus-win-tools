using FluentAssertions;
using Xunit;

namespace BatteryTray.Tests;

public class BatteryChemistryTests
{
    [Fact]
    public void DecodeAsciiTag_StandardLionTag_DecodesCorrectly()
    {
        var bytes = new byte[] { (byte)'L', (byte)'I', (byte)'O', (byte)'N' };
        BatteryIoctlReader.DecodeAsciiTag(bytes).Should().Be("LION");
    }

    [Fact]
    public void DecodeAsciiTag_NullTerminated_StopsAtNull()
    {
        var bytes = new byte[] { (byte)'L', (byte)'I', (byte)'P', 0 };
        BatteryIoctlReader.DecodeAsciiTag(bytes).Should().Be("LIP");
    }

    [Fact]
    public void DecodeAsciiTag_SpacePadded_TrimsResult()
    {
        var bytes = new byte[] { (byte)'L', (byte)'I', (byte)'P', (byte)' ' };
        BatteryIoctlReader.DecodeAsciiTag(bytes).Should().Be("LIP");
    }

    [Fact]
    public void DecodeAsciiTag_NonPrintable_Skipped()
    {
        var bytes = new byte[] { (byte)'L', 0x01, (byte)'I', (byte)'P' };
        BatteryIoctlReader.DecodeAsciiTag(bytes).Should().Be("LIP");
    }

    [Fact]
    public void DecodeAsciiTag_AllZero_ReturnsNull()
    {
        var bytes = new byte[] { 0, 0, 0, 0 };
        BatteryIoctlReader.DecodeAsciiTag(bytes).Should().BeNull();
    }

    [Fact]
    public void DecodeAsciiTag_Empty_ReturnsNull()
    {
        BatteryIoctlReader.DecodeAsciiTag(Array.Empty<byte>()).Should().BeNull();
    }

    [Theory]
    [InlineData("LION", "Lithium-ion")]
    [InlineData("LiP",  "Lithium Polymer")]
    [InlineData("LFP",  "Lithium Iron Phosphate")]
    [InlineData("NiMH", "Nickel Metal Hydride")]
    [InlineData("NiCd", "Nickel Cadmium")]
    [InlineData("PbAc", "Lead Acid")]
    public void MapToFriendly_KnownTag_ReturnsRecognizableName(string tag, string expectedSubstring)
    {
        var friendly = BatteryIoctlReader.MapToFriendly(tag);
        friendly.Should().Contain(expectedSubstring);
        friendly.Should().Contain(tag, because: "the original tag is preserved in parens");
    }

    [Fact]
    public void MapToFriendly_UnrecognizedTag_PassesThroughVerbatim()
    {
        var friendly = BatteryIoctlReader.MapToFriendly("XYZ");
        friendly.Should().Contain("XYZ");
        friendly.Should().Contain("unrecognized");
    }

    [Fact]
    public void MapToFriendly_NullOrEmpty_ReturnsNull()
    {
        BatteryIoctlReader.MapToFriendly(null).Should().BeNull();
        BatteryIoctlReader.MapToFriendly("").Should().BeNull();
    }
}
