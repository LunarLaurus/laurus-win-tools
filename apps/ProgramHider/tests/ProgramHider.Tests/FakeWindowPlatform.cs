using ProgramHider;

namespace ProgramHider.Tests;

internal sealed record FakeWindowState(
    NativeWindowSnapshot Snapshot,
    bool IsVisible,
    bool IsAlive,
    NativeMethods.WindowPlacement? Placement = null,
    string MonitorDeviceName = "DISPLAY1");

internal sealed class FakeWindowPlatform : IWindowPlatform
{
    private readonly Dictionary<nint, FakeWindowState> _windows;
    private nint _foregroundHandle;

    public FakeWindowPlatform(params FakeWindowState[] windows)
    {
        _windows = windows.ToDictionary(w => w.Snapshot.Handle);
        _foregroundHandle = 0;
        foreach (var w in windows)
        {
            if (w.IsAlive && w.IsVisible) { _foregroundHandle = w.Snapshot.Handle; break; }
        }
    }

    public IReadOnlyList<NativeWindowSnapshot> EnumerateTopLevelWindows() =>
        _windows.Values.Where(w => w.IsAlive && w.IsVisible).Select(w => w.Snapshot).ToArray();

    public NativeWindowSnapshot? TryCreateWindowSnapshot(nint handle)
    {
        if (!_windows.TryGetValue(handle, out var w) || !w.IsAlive || !w.IsVisible) return null;
        return w.Snapshot;
    }

    public NativeMethods.WindowPlacement? TryGetWindowPlacement(nint handle) =>
        _windows.TryGetValue(handle, out var w) ? w.Placement : null;

    public bool TrySetWindowPlacement(nint handle, NativeMethods.WindowPlacement placement)
    {
        if (!_windows.TryGetValue(handle, out var w)) return false;
        _windows[handle] = w with { Placement = placement };
        return true;
    }

    public string TryGetMonitorDeviceNameForWindow(nint handle) =>
        _windows.TryGetValue(handle, out var w) ? w.MonitorDeviceName : string.Empty;

    public bool ShowWindow(nint handle, int command)
    {
        if (!_windows.TryGetValue(handle, out var w) || !w.IsAlive) return false;
        _windows[handle] = w with { IsVisible = command != NativeMethods.SW_HIDE };
        return true;
    }

    public bool SetForegroundWindow(nint handle) => _windows.ContainsKey(handle);

    public bool IsWindow(nint handle) => _windows.TryGetValue(handle, out var w) && w.IsAlive;

    public nint GetForegroundWindow()
    {
        if (_foregroundHandle != 0 &&
            _windows.TryGetValue(_foregroundHandle, out var fw) && fw.IsAlive && fw.IsVisible)
            return _foregroundHandle;
        var w = _windows.Values.FirstOrDefault(w => w.IsAlive && w.IsVisible);
        return w is null ? 0 : w.Snapshot.Handle;
    }

    public bool IsVisible(nint handle) => _windows.TryGetValue(handle, out var w) && w.IsVisible;

    public void SetAlive(nint handle, bool isAlive)
    {
        if (_windows.TryGetValue(handle, out var w))
            _windows[handle] = w with { IsAlive = isAlive };
    }

    public void SetForegroundWindowForTest(nint handle) => _foregroundHandle = handle;
}
