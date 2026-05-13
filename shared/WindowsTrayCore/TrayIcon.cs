using System.Runtime.InteropServices;
using WindowsTrayCore.Native;

namespace WindowsTrayCore;

/// <summary>
/// System tray icon with a stable Guid identity. Replacement for
/// <see cref="NotifyIcon"/> that pins the icon's tray-overflow placement
/// across rebuilds (WinForms NotifyIcon exposes no Guid setter, so its
/// identity is derived from a per-process HWND that changes every launch).
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const int CallbackMessageId = TrayNativeMethods.WM_USER + 1;
    private static readonly uint WM_TASKBARCREATED =
        RegisterWindowMessage("TaskbarCreated");

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string lpString);

    private readonly Guid _guid;
    private readonly MessageWindow _messageWindow;

    private Icon? _icon;
    private string _text = string.Empty;
    private bool _visible;
    private bool _added;
    private bool _disposed;

    public TrayIcon(Guid stableId)
    {
        _guid = stableId;
        _messageWindow = new MessageWindow(this);
        _messageWindow.CreateHandle(new CreateParams());
    }

    public static TrayIcon ForApp(string appId) => new(AppIdGuid.For(appId));

    public Guid Guid => _guid;

    public Icon? Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            UpdateAddOrModify();
        }
    }

    public string Text
    {
        get => _text;
        set
        {
            // NOTIFYICONDATA.szTip is 128 wide chars including the terminator.
            var v = value ?? string.Empty;
            if (v.Length > 127) v = v[..127];
            _text = v;
            UpdateAddOrModify();
        }
    }

    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible == value) return;
            _visible = value;
            if (value) Add(); else Remove();
        }
    }

    public ContextMenuStrip? ContextMenuStrip { get; set; }
    public string BalloonTipTitle { get; set; } = string.Empty;
    public string BalloonTipText { get; set; } = string.Empty;
    public ToolTipIcon BalloonTipIcon { get; set; } = ToolTipIcon.None;

    public event EventHandler? Click;
    public event MouseEventHandler? MouseClick;
    public event EventHandler? DoubleClick;
    public event MouseEventHandler? MouseDoubleClick;
    public event EventHandler? BalloonTipClicked;
    public event EventHandler? BalloonTipShown;
    public event EventHandler? BalloonTipClosed;

    public void ShowBalloonTip(int timeoutMs)
        => ShowBalloonTip(timeoutMs, BalloonTipTitle, BalloonTipText, BalloonTipIcon);

    public void ShowBalloonTip(int timeoutMs, string tipTitle, string tipText, ToolTipIcon tipIcon)
    {
        EnsureNotDisposed();
        if (!_visible) return;

        var data = BuildBaseData();
        data.uFlags = TrayNativeMethods.NIF_INFO | TrayNativeMethods.NIF_GUID;
        data.szInfoTitle = Truncate(tipTitle ?? string.Empty, 63);
        data.szInfo = Truncate(tipText ?? string.Empty, 255);
        data.uVersionOrTimeout = (uint)timeoutMs;
        data.dwInfoFlags = MapToolTipIcon(tipIcon);
        TrayNativeMethods.Shell_NotifyIconW(TrayNativeMethods.NIM_MODIFY, ref data);
    }

    private void Add()
    {
        if (_added) return;

        // Always clear any prior registration for this Guid before adding.
        // A previous process can leave the shell's GUID-to-HWND mapping
        // dangling if it died without sending NIM_DELETE (force-kill, crash,
        // power loss). NIM_ADD on a stale Guid is rejected by the shell and
        // the icon never appears. NIM_DELETE on a non-existent Guid is a
        // no-op, so this is always safe to run.
        var cleanup = BuildBaseData();
        cleanup.uFlags = TrayNativeMethods.NIF_GUID;
        TrayNativeMethods.Shell_NotifyIconW(TrayNativeMethods.NIM_DELETE, ref cleanup);

        var data = BuildBaseData();
        data.uFlags = TrayNativeMethods.NIF_MESSAGE | TrayNativeMethods.NIF_TIP
                    | TrayNativeMethods.NIF_GUID | TrayNativeMethods.NIF_SHOWTIP;
        if (_icon is not null)
        {
            data.uFlags |= TrayNativeMethods.NIF_ICON;
            data.hIcon = _icon.Handle;
        }
        if (!TrayNativeMethods.Shell_NotifyIconW(TrayNativeMethods.NIM_ADD, ref data))
            return;
        _added = true;

        // Opt into NOTIFYICON_VERSION_4 so we receive NIN_SELECT / context-menu
        // notifications with anchor coordinates instead of legacy WM_*BUTTONUP.
        data.uVersionOrTimeout = TrayNativeMethods.NOTIFYICON_VERSION_4;
        TrayNativeMethods.Shell_NotifyIconW(TrayNativeMethods.NIM_SETVERSION, ref data);
    }

    private void Remove()
    {
        if (!_added) return;
        var data = BuildBaseData();
        data.uFlags = TrayNativeMethods.NIF_GUID;
        TrayNativeMethods.Shell_NotifyIconW(TrayNativeMethods.NIM_DELETE, ref data);
        _added = false;
    }

    private void UpdateAddOrModify()
    {
        if (!_visible) return;
        if (!_added) { Add(); return; }
        var data = BuildBaseData();
        data.uFlags = TrayNativeMethods.NIF_TIP | TrayNativeMethods.NIF_GUID | TrayNativeMethods.NIF_SHOWTIP;
        if (_icon is not null)
        {
            data.uFlags |= TrayNativeMethods.NIF_ICON;
            data.hIcon = _icon.Handle;
        }
        TrayNativeMethods.Shell_NotifyIconW(TrayNativeMethods.NIM_MODIFY, ref data);
    }

    private TrayNativeMethods.NOTIFYICONDATAW BuildBaseData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<TrayNativeMethods.NOTIFYICONDATAW>(),
        hWnd = _messageWindow.Handle,
        uID = 1,
        guidItem = _guid,
        uCallbackMessage = (uint)CallbackMessageId,
        szTip = _text,
        szInfo = string.Empty,
        szInfoTitle = string.Empty,
    };

    private static uint MapToolTipIcon(ToolTipIcon icon) => icon switch
    {
        ToolTipIcon.Info => TrayNativeMethods.NIIF_INFO,
        ToolTipIcon.Warning => TrayNativeMethods.NIIF_WARNING,
        ToolTipIcon.Error => TrayNativeMethods.NIIF_ERROR,
        _ => TrayNativeMethods.NIIF_NONE,
    };

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private void EnsureNotDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(TrayIcon));
    }

    internal void HandleCallbackMessage(IntPtr wParam, IntPtr lParam)
    {
        // With NOTIFYICON_VERSION_4 the notification code lives in LOWORD(lParam)
        // and the (x, y) anchor coords in (LOWORD(wParam), HIWORD(wParam)).
        int notification = LowWord(lParam);
        switch (notification)
        {
            case TrayNativeMethods.NIN_SELECT:
            case TrayNativeMethods.NIN_KEYSELECT:
                Click?.Invoke(this, EventArgs.Empty);
                MouseClick?.Invoke(this, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
                break;
            case TrayNativeMethods.WM_LBUTTONDBLCLK:
                DoubleClick?.Invoke(this, EventArgs.Empty);
                MouseDoubleClick?.Invoke(this, new MouseEventArgs(MouseButtons.Left, 2, 0, 0, 0));
                break;
            case TrayNativeMethods.WM_RBUTTONUP:
            case TrayNativeMethods.WM_CONTEXTMENU:
                MouseClick?.Invoke(this, new MouseEventArgs(MouseButtons.Right, 1, 0, 0, 0));
                ShowContextMenu();
                break;
            case TrayNativeMethods.NIN_BALLOONSHOW:
                BalloonTipShown?.Invoke(this, EventArgs.Empty);
                break;
            case TrayNativeMethods.NIN_BALLOONHIDE:
            case TrayNativeMethods.NIN_BALLOONTIMEOUT:
                BalloonTipClosed?.Invoke(this, EventArgs.Empty);
                break;
            case TrayNativeMethods.NIN_BALLOONUSERCLICK:
                BalloonTipClicked?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    internal void HandleTaskbarRecreated()
    {
        // Explorer restart wipes our registration. Re-add silently.
        if (_visible)
        {
            _added = false;
            Add();
        }
    }

    private void ShowContextMenu()
    {
        if (ContextMenuStrip is null) return;
        // The classic tray-menu trick: hoist our message window to the foreground
        // first so clicking outside the menu dismisses it. Without this the menu
        // stays open until another window steals focus.
        TrayNativeMethods.SetForegroundWindow(_messageWindow.Handle);
        ContextMenuStrip.Show(Cursor.Position);
    }

    private static int LowWord(IntPtr value) => (int)((long)value & 0xFFFF);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Remove();
        _messageWindow.DestroyHandle();
    }

    private sealed class MessageWindow : NativeWindow
    {
        private readonly TrayIcon _owner;
        public MessageWindow(TrayIcon owner) => _owner = owner;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == CallbackMessageId)
            {
                _owner.HandleCallbackMessage(m.WParam, m.LParam);
                return;
            }
            if (m.Msg == WM_TASKBARCREATED)
            {
                _owner.HandleTaskbarRecreated();
                return;
            }
            base.WndProc(ref m);
        }
    }
}
