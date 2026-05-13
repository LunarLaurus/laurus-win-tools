using FluentAssertions;
using ProgramHider;
using Xunit;

namespace ProgramHider.Tests;

public class ActiveWindowTrackerTests
{
    [Fact]
    public void FallsBackToLastTrackedWindow()
    {
        var platform = new FakeWindowPlatform(
            new FakeWindowState(
                new NativeWindowSnapshot((nint)10, "Tracked Window", "NormalClass", "powershell", 10, 0, 0, false),
                IsVisible: true, IsAlive: true),
            new FakeWindowState(
                new NativeWindowSnapshot((nint)11, "Tray Host", "Shell_TrayWnd", "explorer", 11, 0, 0, false),
                IsVisible: true, IsAlive: true));
        var tracker = new ActiveWindowTracker(platform);

        platform.SetForegroundWindowForTest((nint)10);
        tracker.CaptureCurrentSnapshot(WindowCatalog.IsManageableWindow);

        platform.SetForegroundWindowForTest((nint)11);
        var resolved = tracker.ResolveSnapshot(WindowCatalog.IsManageableWindow);

        resolved.Should().NotBeNull();
        resolved!.Value.Handle.Should().Be((nint)10);
    }

    [Fact]
    public void ClearsStaleFallback()
    {
        var platform = new FakeWindowPlatform(
            new FakeWindowState(
                new NativeWindowSnapshot((nint)10, "Tracked Window", "NormalClass", "powershell", 10, 0, 0, false),
                IsVisible: true, IsAlive: true),
            new FakeWindowState(
                new NativeWindowSnapshot((nint)11, "Tray Host", "Shell_TrayWnd", "explorer", 11, 0, 0, false),
                IsVisible: true, IsAlive: true));
        var tracker = new ActiveWindowTracker(platform);

        platform.SetForegroundWindowForTest((nint)10);
        tracker.CaptureCurrentSnapshot(WindowCatalog.IsManageableWindow);

        platform.SetAlive((nint)10, false);
        platform.SetForegroundWindowForTest((nint)11);

        tracker.ResolveSnapshot(WindowCatalog.IsManageableWindow).Should().BeNull();
    }

    [Fact]
    public void RemembersExplicitForegroundHandles()
    {
        var platform = new FakeWindowPlatform(
            new FakeWindowState(
                new NativeWindowSnapshot((nint)21, "Tracked Window", "NormalClass", "powershell", 21, 0, 0, false),
                IsVisible: true, IsAlive: true),
            new FakeWindowState(
                new NativeWindowSnapshot((nint)22, "Menu Host", "Shell_TrayWnd", "explorer", 22, 0, 0, false),
                IsVisible: true, IsAlive: true));
        var tracker = new ActiveWindowTracker(platform);

        tracker.CaptureSnapshotForHandle((nint)21, WindowCatalog.IsManageableWindow).Should().NotBeNull();

        platform.SetForegroundWindowForTest((nint)22);
        var resolved = tracker.ResolveSnapshot(WindowCatalog.IsManageableWindow);

        resolved.Should().NotBeNull();
        resolved!.Value.Handle.Should().Be((nint)21);
    }
}
