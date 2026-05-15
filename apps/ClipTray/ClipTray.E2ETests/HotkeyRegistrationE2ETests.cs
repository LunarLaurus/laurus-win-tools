using FluentAssertions;
using WindowsTrayCore;
using Xunit;

namespace ClipTray.E2ETests;

public class HotkeyRegistrationE2ETests
{
    [WindowsFact]
    public void Register_RealHotkey_AcceptsRare()
    {
        const int id = 7771;
        using var h = new HotkeyRegistration();
        var ok = h.Register(id,
            HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift,
            System.Windows.Forms.Keys.F19);
        ok.Should().BeTrue();
        h.Unregister(id).Should().BeTrue();
    }
}
