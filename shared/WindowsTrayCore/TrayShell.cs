using System.ComponentModel;
using System.Windows.Forms;

namespace WindowsTrayCore;

/// <summary>
/// Compositional tray host. Owns the <see cref="NotifyIcon"/> lifecycle, icon refresh
/// via <see cref="TrayIconManager"/>, tooltip enforcement, and optional single-instance
/// activation hookup. Works as a field in either <see cref="ApplicationContext"/> or
/// <see cref="Form"/> — no inheritance required.
/// </summary>
public sealed class TrayShell : IDisposable
{
    public NotifyIcon NotifyIcon { get; }
    public ContextMenuStrip Menu { get; }
    public TrayIconManager Icons { get; }
    public IUserNotifier Notifier { get; }

    // ── Routed events ───────────────────────────────────────────────────────
    public event CancelEventHandler?  MenuOpening;
    public event MouseEventHandler?   IconMouseClick;
    public event MouseEventHandler?   IconMouseDoubleClick;
    public event EventHandler?        ActivationRequested;

    private readonly WindowsAppCore.SingleInstanceActivation? _activation;

    /// <param name="menu">App-owned menu strip. Shell wires it to the NotifyIcon.</param>
    /// <param name="iconProvider">App-owned renderer.</param>
    /// <param name="theme">Theme instance; shell subscribes via TrayIconManager.</param>
    /// <param name="notifier">Notification backend (balloon or toast).</param>
    /// <param name="activation">Optional; wired when app passed it at startup.</param>
    public TrayShell(
        ContextMenuStrip menu,
        ITrayIconProvider iconProvider,
        TrayTheme theme,
        IUserNotifier notifier,
        WindowsAppCore.SingleInstanceActivation? activation = null)
    {
        Menu = menu;
        Notifier = notifier;
        _activation = activation;

        NotifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = SystemIcons.Application,
            Visible = false,
        };

        Icons = new TrayIconManager(NotifyIcon, iconProvider, theme);

        NotifyIcon.ContextMenuStrip!.Opening += (s, e) => MenuOpening?.Invoke(s, e);
        NotifyIcon.MouseClick += (s, e) => IconMouseClick?.Invoke(s, e);
        NotifyIcon.MouseDoubleClick += (s, e) => IconMouseDoubleClick?.Invoke(s, e);

        if (_activation is not null)
            _activation.ActivationRequested += (s, e) => ActivationRequested?.Invoke(s, e);
    }

    /// <summary>Sets the tooltip, enforcing the 63-char WinAPI limit.</summary>
    public void SetTooltip(string text) =>
        NotifyIcon.Text = TrayTooltip.Truncate(text);

    /// <summary>Shows the context menu at the current cursor position.</summary>
    public void ShowMenuAtCursor() =>
        Menu.Show(Cursor.Position);

    /// <summary>Hides the tray icon in preparation for shutdown.</summary>
    public void BeginShutdown() =>
        NotifyIcon.Visible = false;

    public void Dispose()
    {
        Icons.Dispose();
        NotifyIcon.Visible = false;
        NotifyIcon.Dispose();
        Menu.Dispose();
        Notifier.Dispose();
    }
}
