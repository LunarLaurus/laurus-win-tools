using FluentAssertions;
using ProgramHider;
using Xunit;

namespace ProgramHider.Tests;

public class WindowHideServiceTests
{
    [Fact]
    public void HideRestoreAndPrune_Works()
    {
        var handle = (nint)99;
        var platform = new FakeWindowPlatform(
            new FakeWindowState(
                new NativeWindowSnapshot(handle, "Adguard Home", "ConsoleWindowClass", "powershell", 99, 0, 0, false),
                IsVisible: true, IsAlive: true));
        var service = new WindowHideService(platform);
        var hidden = new Dictionary<nint, HiddenWindow>();

        service.TryHideWindow(handle, 0, hidden, null, out var hiddenWindow).Should().BeTrue();
        hidden.Should().ContainKey(handle);
        hiddenWindow.Should().NotBeNull();
        platform.IsVisible(handle).Should().BeFalse();

        service.TryRestoreWindow(handle, hidden, restoreWithoutFocus: false, out var restoredWindow).Should().BeTrue();
        restoredWindow.Should().NotBeNull();
        platform.IsVisible(handle).Should().BeTrue();

        platform.SetAlive(handle, false);
        hidden[handle] = hiddenWindow!;
        service.PruneDeadWindows(hidden).Should().Be(1);
        hidden.Should().NotContainKey(handle);
    }

    [Fact]
    public void RejectsExcludedAndDuplicateWindows()
    {
        var handle = (nint)77;
        var platform = new FakeWindowPlatform(
            new FakeWindowState(
                new NativeWindowSnapshot(handle, "Visible Window", "NormalClass", "powershell", 77, 0, 0, false),
                IsVisible: true, IsAlive: true));
        var service = new WindowHideService(platform);
        var hidden = new Dictionary<nint, HiddenWindow>();

        service.TryHideWindow(handle, handle, hidden, null, out _).Should().BeFalse("excluded handle must not hide");

        service.TryHideWindow(handle, 0, hidden, null, out _).Should().BeTrue();
        service.TryHideWindow(handle, 0, hidden, null, out _).Should().BeFalse("duplicate hide must be rejected");
    }
}
