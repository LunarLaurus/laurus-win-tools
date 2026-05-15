using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class ThemePreferenceTests
{
    [Fact]
    public void Enum_HasThreeMembers_InExpectedOrder()
    {
        ((int)ThemePreference.Auto).Should().Be(0);
        ((int)ThemePreference.Light).Should().Be(1);
        ((int)ThemePreference.Dark).Should().Be(2);
    }
}
