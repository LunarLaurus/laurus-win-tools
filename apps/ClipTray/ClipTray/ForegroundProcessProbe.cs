using System.Diagnostics;

namespace ClipTray;

internal static class ForegroundProcessProbe
{
    /// <summary>
    /// Returns the process name of the foreground window, lowercased.
    /// Returns null if anything in the chain fails (foreground HWND
    /// disappears, process exits between calls, etc).
    /// </summary>
    public static string? GetCurrentName()
    {
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;
            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return null;
            using var proc = Process.GetProcessById((int)pid);
            return proc.ProcessName.ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Returns the foreground window's HWND (used to restore focus before paste).</summary>
    public static IntPtr GetForegroundHwnd() => NativeMethods.GetForegroundWindow();
}
