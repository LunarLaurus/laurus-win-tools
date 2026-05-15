using System.Windows.Forms;
using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class HotkeyRegistrationTests
{
    [Fact]
    public void Modifiers_FlagValues_AreExpected()
    {
        ((int)HotkeyModifiers.None).Should().Be(0);
        ((int)HotkeyModifiers.Alt).Should().Be(1);
        ((int)HotkeyModifiers.Control).Should().Be(2);
        ((int)HotkeyModifiers.Shift).Should().Be(4);
        ((int)HotkeyModifiers.Win).Should().Be(8);
    }

    [WindowsFact]
    public void Construct_DoesNotThrow()
    {
        using var h = new HotkeyRegistration();
        h.Should().NotBeNull();
    }

    [WindowsFact]
    public void Dispose_IsIdempotent()
    {
        var h = new HotkeyRegistration();
        h.Dispose();
        h.Dispose();
    }

    [WindowsFact]
    public void Register_DuplicateId_ReturnsFalseOnSecondCall()
    {
        const int id = 9999;
        const HotkeyModifiers mods = HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift;
        const Keys key = Keys.F19;

        using var h = new HotkeyRegistration();
        var first = h.Register(id, mods, key);
        var second = h.Register(id, mods, key);

        first.Should().BeTrue();
        second.Should().BeFalse();

        h.Unregister(id);
    }

    [WindowsFact]
    public void Unregister_AfterRegister_ReturnsTrue()
    {
        const int id = 9998;
        using var h = new HotkeyRegistration();
        h.Register(id, HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, Keys.F18);
        h.Unregister(id).Should().BeTrue();
    }
}
