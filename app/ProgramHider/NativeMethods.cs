using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;

namespace ProgramHider;

internal static class NativeMethods
{
    internal const int GWL_EXSTYLE = -20;
    internal const int MOD_CONTROL = 0x0002;
    internal const int MOD_SHIFT = 0x0004;
    internal const int MOD_NOREPEAT = 0x4000;
    internal const int SW_HIDE = 0;
    internal const int SW_SHOWMAXIMIZED = 3;
    internal const int SW_RESTORE = 9;
    internal const int WM_HOTKEY = 0x0312;
    internal const int MOD_ALT = 0x0001;
    internal const int MOD_WIN = 0x0008;
    internal const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
    internal const uint GW_OWNER = 4;
    internal const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
    internal const nint WS_EX_TOOLWINDOW = 0x00000080;
    internal const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    internal const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    private delegate bool EnumWindowsProc(nint handle, nint lParam);
    internal delegate void WinEventProc(
        nint eventHookHandle,
        uint eventType,
        nint handle,
        int objectId,
        int childId,
        uint eventThreadId,
        uint eventTime);

    internal static IEnumerable<NativeWindowSnapshot> EnumerateTopLevelWindows()
    {
        var windows = new List<NativeWindowSnapshot>();
        EnumWindows(
            (handle, _) =>
            {
                var snapshot = TryCreateWindowSnapshot(handle);
                if (snapshot is not null)
                {
                    windows.Add(snapshot.Value);
                }

                return true;
            },
            0);

        return windows;
    }

    internal static NativeWindowSnapshot? TryCreateWindowSnapshot(nint handle)
    {
        if (handle == 0 || !IsWindow(handle) || !IsWindowVisible(handle))
        {
            return null;
        }

        var title = GetWindowText(handle);
        var className = GetClassName(handle);
        var owner = GetWindow(handle, GW_OWNER);
        var extendedStyle = GetWindowLongPtr(handle, GWL_EXSTYLE);
        var isMaximized = IsZoomed(handle);
        var processName = GetProcessName(handle);

        return new NativeWindowSnapshot(handle, title, className, processName, owner, extendedStyle, isMaximized);
    }

    internal static WindowPlacement? TryGetWindowPlacement(nint handle)
    {
        var placement = WindowPlacement.Create();
        if (!GetWindowPlacement(handle, ref placement))
        {
            return null;
        }

        return placement;
    }

    internal static bool TrySetWindowPlacement(nint handle, WindowPlacement placement)
    {
        placement.Length = Marshal.SizeOf<WindowPlacement>();
        return SetWindowPlacement(handle, ref placement);
    }

    internal static string TryGetMonitorDeviceNameForWindow(nint handle)
    {
        var monitorHandle = MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST);
        if (monitorHandle == 0)
        {
            return string.Empty;
        }

        var monitorInfo = MonitorInfoEx.Create();
        return GetMonitorInfoW(monitorHandle, ref monitorInfo)
            ? monitorInfo.DeviceName.TrimEnd('\0')
            : string.Empty;
    }

    private static string GetWindowText(nint handle)
    {
        var length = GetWindowTextLengthW(handle);
        if (length <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 1);
        GetWindowTextW(handle, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string GetClassName(nint handle)
    {
        var builder = new StringBuilder(256);
        GetClassNameW(handle, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string GetProcessName(nint handle)
    {
        GetWindowThreadProcessId(handle, out var processId);
        if (processId == 0)
        {
            return string.Empty;
        }

        try
        {
            return Process.GetProcessById((int)processId).ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetWindowThreadProcessId(nint handle, out uint processId);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static extern nint GetWindowLongPtr(nint handle, int index);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint GetWindow(nint handle, uint command);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextW(nint handle, StringBuilder text, int maxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowTextLengthW(nint handle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassNameW(nint handle, StringBuilder className, int maxCount);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(nint handle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(nint handle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsZoomed(nint handle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowPlacement(nint handle, ref WindowPlacement placement);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPlacement(nint handle, [In] ref WindowPlacement placement);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(nint handle, int id, int modifiers, uint virtualKeyCode);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(nint handle, int id);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(nint handle);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SetWinEventHook(
        uint eventMin,
        uint eventMax,
        nint moduleHandle,
        WinEventProc callback,
        uint processId,
        uint threadId,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(nint handle, int command);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint MonitorFromWindow(nint handle, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfoW(nint monitorHandle, ref MonitorInfoEx monitorInfo);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWinEvent(nint eventHookHandle);

    [StructLayout(LayoutKind.Sequential)]
    internal struct WindowPlacement
    {
        public int Length;
        public int Flags;
        public int ShowCommand;
        public Point MinPosition;
        public Point MaxPosition;
        public Rect NormalPosition;

        public static WindowPlacement Create()
        {
            return new WindowPlacement
            {
                Length = Marshal.SizeOf<WindowPlacement>()
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MonitorInfoEx
    {
        public int Size;
        public Rect MonitorArea;
        public Rect WorkArea;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        public static MonitorInfoEx Create()
        {
            return new MonitorInfoEx
            {
                Size = Marshal.SizeOf<MonitorInfoEx>(),
                DeviceName = string.Empty
            };
        }
    }
}

internal readonly record struct NativeWindowSnapshot(
    nint Handle,
    string Title,
    string ClassName,
    string ProcessName,
    nint Owner,
    nint ExtendedStyle,
    bool IsMaximized);
