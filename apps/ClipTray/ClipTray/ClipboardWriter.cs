using System.Windows.Forms;

namespace ClipTray;

internal static class ClipboardWriter
{
    public static void SetText(string text)
    {
        Clipboard.SetText(text, TextDataFormat.UnicodeText);
    }

    public static void SetImage(string pngPath)
    {
        using var img = System.Drawing.Image.FromFile(pngPath);
        Clipboard.SetImage(img);
    }

    /// <summary>
    /// Synthesises Ctrl-down, V-down, V-up, Ctrl-up to the foreground
    /// window. Returns false if SendInput fails (typically a UAC
    /// integrity-level mismatch); caller should fall back to a manual-paste balloon.
    /// </summary>
    public static bool SendCtrlV(IntPtr targetWindow)
    {
        if (targetWindow != IntPtr.Zero)
            NativeMethods.SetForegroundWindow(targetWindow);

        var inputs = new NativeMethods.INPUT[]
        {
            MakeKey(NativeMethods.VK_CONTROL, keyUp: false),
            MakeKey(NativeMethods.VK_V,       keyUp: false),
            MakeKey(NativeMethods.VK_V,       keyUp: true),
            MakeKey(NativeMethods.VK_CONTROL, keyUp: true),
        };

        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, NativeMethods.INPUT.Size);
        return sent == (uint)inputs.Length;
    }

    private static NativeMethods.INPUT MakeKey(ushort vk, bool keyUp) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT
            {
                wVk = vk,
                dwFlags = keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0u,
            },
        },
    };
}
