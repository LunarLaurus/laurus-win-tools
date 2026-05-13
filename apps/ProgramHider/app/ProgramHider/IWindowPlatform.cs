namespace ProgramHider;

// Minimal platform surface for window enumeration and show/hide primitives.
// This lets runtime logic stay testable without binding every test to Win32.
internal interface IWindowPlatform
{
    IReadOnlyList<NativeWindowSnapshot> EnumerateTopLevelWindows();
    NativeWindowSnapshot? TryCreateWindowSnapshot(nint handle);
    NativeMethods.WindowPlacement? TryGetWindowPlacement(nint handle);
    bool TrySetWindowPlacement(nint handle, NativeMethods.WindowPlacement placement);
    string TryGetMonitorDeviceNameForWindow(nint handle);
    bool ShowWindow(nint handle, int command);
    bool SetForegroundWindow(nint handle);
    bool IsWindow(nint handle);
    nint GetForegroundWindow();
}

// Production Win32 implementation of the window platform abstraction.
internal sealed class Win32WindowPlatform : IWindowPlatform
{
    public IReadOnlyList<NativeWindowSnapshot> EnumerateTopLevelWindows()
    {
        return NativeMethods.EnumerateTopLevelWindows().ToArray();
    }

    public NativeWindowSnapshot? TryCreateWindowSnapshot(nint handle)
    {
        return NativeMethods.TryCreateWindowSnapshot(handle);
    }

    public NativeMethods.WindowPlacement? TryGetWindowPlacement(nint handle)
    {
        return NativeMethods.TryGetWindowPlacement(handle);
    }

    public bool TrySetWindowPlacement(nint handle, NativeMethods.WindowPlacement placement)
    {
        return NativeMethods.TrySetWindowPlacement(handle, placement);
    }

    public string TryGetMonitorDeviceNameForWindow(nint handle)
    {
        return NativeMethods.TryGetMonitorDeviceNameForWindow(handle);
    }

    public bool ShowWindow(nint handle, int command)
    {
        return NativeMethods.ShowWindow(handle, command);
    }

    public bool SetForegroundWindow(nint handle)
    {
        return NativeMethods.SetForegroundWindow(handle);
    }

    public bool IsWindow(nint handle)
    {
        return NativeMethods.IsWindow(handle);
    }

    public nint GetForegroundWindow()
    {
        return NativeMethods.GetForegroundWindow();
    }
}
