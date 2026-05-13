using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class AppIdGuidTests
{
    [Fact]
    public void For_SameInput_ReturnsSameGuid()
    {
        AppIdGuid.For("BatteryTray").Should().Be(AppIdGuid.For("BatteryTray"));
    }

    [Fact]
    public void For_DifferentInputs_ReturnDifferentGuids()
    {
        var a = AppIdGuid.For("BatteryTray");
        var b = AppIdGuid.For("SoundTracker");
        a.Should().NotBe(b);
    }

    [Fact]
    public void For_IsCaseSensitive()
    {
        AppIdGuid.For("BatteryTray").Should().NotBe(AppIdGuid.For("batterytray"));
    }

    [Fact]
    public void For_NullOrEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() => AppIdGuid.For(""));
        Assert.Throws<ArgumentException>(() => AppIdGuid.For("   "));
    }

    [Fact]
    public void For_ProducesRfc4122Variant()
    {
        var g = AppIdGuid.For("BatteryTray");
        var bytes = g.ToByteArray();
        // RFC 4122 variant bits: top two bits of byte 8 (indexed) must be 10.
        ((bytes[8] & 0xC0) == 0x80).Should().BeTrue();
        // Version 5 (name-based, SHA-1) — version nibble in byte 6 high nibble.
        ((bytes[6] & 0xF0) == 0x50).Should().BeTrue();
    }

    [Theory]
    [InlineData("BatteryTray")]
    [InlineData("NetProfileSwitcher")]
    [InlineData("ProgramHider")]
    [InlineData("SoundTracker")]
    public void For_ProjectAppIds_DoNotThrow(string appId)
    {
        var g = AppIdGuid.For(appId);
        g.Should().NotBe(Guid.Empty);
    }
}
