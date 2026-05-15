using System.Drawing;
using System.Runtime.InteropServices;

namespace WindowsTrayCore;

/// <summary>
/// Converts a <see cref="Bitmap"/> into an <see cref="Icon"/> whose
/// <see cref="Icon.Dispose"/> safely releases the underlying GDI handle.
/// <para>
/// The naive path (<c>Icon.FromHandle(bmp.GetHicon())</c>) returns a
/// non-owning Icon: calling <c>Dispose()</c> on it does NOT call
/// <c>DestroyIcon</c>, so every periodic refresh leaks an HICON until the
/// process exits or Windows hits its 10 000 GDI-handle quota. This helper
/// clones the bits into a managed-owned Icon and immediately destroys the
/// source handle, so callers get away with plain <c>using</c> blocks.
/// </para>
/// <para>
/// This helper is the runtime-drawing path for <b>stateful</b> tray icons
/// (battery %, mute, count badges). <b>Static brand glyphs</b> belong in a
/// build-time .ico produced by <c>tools/IconGen</c> rather than here; see
/// <c>docs/conventions/tray-icons.md</c> for the static-vs-stateful rule
/// and why every app needs a static .ico regardless of whether it also
/// does runtime drawing.
/// </para>
/// </summary>
public static class IconBuilder
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// Convert <paramref name="bitmap"/> to a fully managed-owned Icon.
    /// Caller is responsible for disposing the returned Icon; the input
    /// bitmap is NOT disposed (typical pattern: caller already wrapped it
    /// in <c>using</c>).
    /// </summary>
    public static Icon FromBitmap(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var hicon = bitmap.GetHicon();
        try
        {
            using var nonOwning = Icon.FromHandle(hicon);
            return (Icon)nonOwning.Clone();
        }
        finally
        {
            DestroyIcon(hicon);
        }
    }
}
