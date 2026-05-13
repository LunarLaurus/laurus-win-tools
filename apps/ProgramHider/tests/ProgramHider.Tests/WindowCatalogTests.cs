using FluentAssertions;
using ProgramHider;
using Xunit;

namespace ProgramHider.Tests;

public class WindowCatalogTests
{
    [Fact]
    public void FindByTitle_RespectsProcessFilter()
    {
        var platform = new FakeWindowPlatform(
            new FakeWindowState(
                new NativeWindowSnapshot((nint)1, "Program Hider Smoke Window", "SmokeClass", "ProgramHider.SmokeWindow", 1, 0, 0, false),
                IsVisible: true, IsAlive: true),
            new FakeWindowState(
                new NativeWindowSnapshot((nint)2, "Program Hider Smoke Window", "FirefoxClass", "firefox", 2, 0, 0, false),
                IsVisible: true, IsAlive: true));

        var match = WindowCatalog.FindFirstByTitleContains(platform, "Smoke Window", "ProgramHider.SmokeWindow");
        match.Should().NotBeNull();
        match!.Value.Handle.Should().Be((nint)1);

        WindowCatalog.FindFirstByTitleContains(platform, "Nope", "ProgramHider.SmokeWindow").Should().BeNull();
    }

    [Fact]
    public void Enumerate_FiltersHiddenExcludedAndNonManageableWindows()
    {
        var platform = new FakeWindowPlatform(
            new FakeWindowState(
                new NativeWindowSnapshot((nint)1, "Visible Window", "NormalClass", "powershell", 1, 0, 0, false),
                IsVisible: true, IsAlive: true),
            new FakeWindowState(
                new NativeWindowSnapshot((nint)2, "Hidden Tracked", "NormalClass", "powershell", 2, 0, 0, false),
                IsVisible: true, IsAlive: true),
            new FakeWindowState(
                new NativeWindowSnapshot((nint)3, "Tool Window", "NormalClass", "powershell", 3, 0, NativeMethods.WS_EX_TOOLWINDOW, false),
                IsVisible: true, IsAlive: true),
            new FakeWindowState(
                new NativeWindowSnapshot((nint)4, "Owned Window", "NormalClass", "powershell", 4, (nint)123, 0, false),
                IsVisible: true, IsAlive: true),
            new FakeWindowState(
                new NativeWindowSnapshot((nint)5, "Progman", "Progman", "explorer", 5, 0, 0, false),
                IsVisible: true, IsAlive: true));

        var windows = WindowCatalog.EnumerateManageableWindows(platform, new[] { (nint)2 }, excludedHandle: (nint)99);

        windows.Should().HaveCount(1);
        windows[0].Handle.Should().Be((nint)1);
    }
}
