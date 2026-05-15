using System.Runtime.InteropServices;

namespace WindowsTrayCore.Native;

internal static class TrayNativeMethods
{
    public const int WM_USER = 0x0400;
    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_LBUTTONDBLCLK = 0x0203;
    public const int WM_RBUTTONUP = 0x0205;
    public const int WM_CONTEXTMENU = 0x007B;

    // NIN_* notifications arrive in HIWORD of lParam when NOTIFYICON_VERSION_4 is set.
    public const int NIN_SELECT = WM_USER;
    public const int NIN_KEYSELECT = WM_USER + 1;
    public const int NIN_BALLOONSHOW = WM_USER + 2;
    public const int NIN_BALLOONHIDE = WM_USER + 3;
    public const int NIN_BALLOONTIMEOUT = WM_USER + 4;
    public const int NIN_BALLOONUSERCLICK = WM_USER + 5;
    public const int NIN_POPUPOPEN = WM_USER + 6;
    public const int NIN_POPUPCLOSE = WM_USER + 7;

    public const int NIM_ADD = 0x0;
    public const int NIM_MODIFY = 0x1;
    public const int NIM_DELETE = 0x2;
    public const int NIM_SETFOCUS = 0x3;
    public const int NIM_SETVERSION = 0x4;

    public const int NIF_MESSAGE = 0x01;
    public const int NIF_ICON = 0x02;
    public const int NIF_TIP = 0x04;
    public const int NIF_STATE = 0x08;
    public const int NIF_INFO = 0x10;
    public const int NIF_GUID = 0x20;
    public const int NIF_SHOWTIP = 0x80;

    public const int NIS_HIDDEN = 0x01;

    public const int NIIF_NONE = 0x00;
    public const int NIIF_INFO = 0x01;
    public const int NIIF_WARNING = 0x02;
    public const int NIIF_ERROR = 0x03;
    public const int NIIF_USER = 0x04;
    public const int NIIF_NOSOUND = 0x10;
    public const int NIIF_LARGE_ICON = 0x20;
    public const int NIIF_RESPECT_QUIET_TIME = 0x80;

    public const uint NOTIFYICON_VERSION_4 = 4;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;

        public uint dwState;
        public uint dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;

        public uint uVersionOrTimeout;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;

        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmGetColorizationColor(out uint pcrColorization, out bool pfOpaqueBlend);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    public const int DWMWA_USE_IMMERSIVE_DARK_MODE_PRE_20H1 = 19;
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    internal const int WM_HOTKEY = 0x0312;
}
