using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ProgramHider;

internal sealed class ProgramHiderContext : ApplicationContext
{
    private const int HotkeyId = 0x1000;

    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _hideWindowMenu;
    private readonly HotkeyMessageWindow _messageWindow;
    private readonly Dictionary<nint, HiddenWindow> _hiddenWindows = new();
    private bool _disposed;

    public ProgramHiderContext()
    {
        _menu = new ContextMenuStrip();
        _menu.Opening += OnMenuOpening;

        _hideWindowMenu = new ToolStripMenuItem("Hide window");

        _notifyIcon = new NotifyIcon
        {
            Text = "Program Hider v0.0.2",
            Icon = SystemIcons.Application,
            ContextMenuStrip = _menu,
            Visible = true
        };
        _notifyIcon.MouseClick += OnNotifyIconMouseClick;

        _messageWindow = new HotkeyMessageWindow(OnHotkeyPressed);

        if (!NativeMethods.RegisterHotKey(
                _messageWindow.Handle,
                HotkeyId,
                NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT,
                (uint)Keys.H))
        {
            throw new Win32Exception("Unable to register the Ctrl+Shift+H hotkey.");
        }
    }

    protected override void ExitThreadCore()
    {
        DisposeManagedState();
        base.ExitThreadCore();
    }

    private void OnNotifyIconMouseClick(object? sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button != MouseButtons.Left)
        {
            return;
        }

        RebuildMenu();
        _menu.Show(Cursor.Position);
    }

    private void OnMenuOpening(object? sender, CancelEventArgs eventArgs)
    {
        RebuildMenu();
    }

    private void RebuildMenu()
    {
        _menu.SuspendLayout();
        try
        {
            _menu.Items.Clear();

            var hideActiveItem = new ToolStripMenuItem("Hide active window\tCtrl+Shift+H");
            hideActiveItem.Click += (_, _) => HideActiveWindow();
            _menu.Items.Add(hideActiveItem);

            _hideWindowMenu.DropDownItems.Clear();
            foreach (var candidate in EnumerateCandidateWindows())
            {
                var item = new ToolStripMenuItem(candidate.MenuLabel);
                item.Click += (_, _) => HideWindow(candidate.Handle);
                _hideWindowMenu.DropDownItems.Add(item);
            }

            if (_hideWindowMenu.DropDownItems.Count == 0)
            {
                _hideWindowMenu.DropDownItems.Add(new ToolStripMenuItem("No eligible windows") { Enabled = false });
            }

            _menu.Items.Add(_hideWindowMenu);
            _menu.Items.Add(new ToolStripSeparator());

            if (_hiddenWindows.Count == 0)
            {
                _menu.Items.Add(new ToolStripMenuItem("No hidden windows") { Enabled = false });
            }
            else
            {
                foreach (var hiddenWindow in _hiddenWindows.Values.OrderBy(window => window.Title, StringComparer.OrdinalIgnoreCase))
                {
                    var restoreItem = new ToolStripMenuItem($"Restore: {EscapeMenuLabel(hiddenWindow.Title)}");
                    restoreItem.Click += (_, _) => RestoreWindow(hiddenWindow.Handle);
                    _menu.Items.Add(restoreItem);
                }
            }

            _menu.Items.Add(new ToolStripSeparator());

            var restoreAllItem = new ToolStripMenuItem("Restore all")
            {
                Enabled = _hiddenWindows.Count > 0
            };
            restoreAllItem.Click += (_, _) => RestoreAllWindows();
            _menu.Items.Add(restoreAllItem);

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (_, _) => ExitThread();
            _menu.Items.Add(exitItem);
        }
        finally
        {
            _menu.ResumeLayout();
        }
    }

    private IReadOnlyList<WindowSnapshot> EnumerateCandidateWindows()
    {
        return NativeMethods.EnumerateTopLevelWindows()
            .Where(window => window.Handle != _messageWindow.Handle)
            .Where(window => !_hiddenWindows.ContainsKey(window.Handle))
            .Where(window => !string.IsNullOrWhiteSpace(window.Title))
            .Where(window => window.Owner == 0)
            .Where(window => (window.ExtendedStyle & NativeMethods.WS_EX_TOOLWINDOW) == 0)
            .Where(window => !string.Equals(window.ClassName, "Shell_TrayWnd", StringComparison.Ordinal))
            .Where(window => !string.Equals(window.ClassName, "Progman", StringComparison.Ordinal))
            .OrderBy(window => window.Title, StringComparer.OrdinalIgnoreCase)
            .Select(window => new WindowSnapshot(window.Handle, EscapeMenuLabel(TrimMenuLabel(window.Title))))
            .ToArray();
    }

    private void OnHotkeyPressed(int hotkeyId)
    {
        if (hotkeyId == HotkeyId)
        {
            HideActiveWindow();
        }
    }

    private void HideActiveWindow()
    {
        HideWindow(NativeMethods.GetForegroundWindow());
    }

    private void HideWindow(nint handle)
    {
        if (handle == 0 || handle == _messageWindow.Handle || _hiddenWindows.ContainsKey(handle))
        {
            return;
        }

        var snapshot = NativeMethods.TryCreateWindowSnapshot(handle);
        if (snapshot is null)
        {
            return;
        }

        var window = snapshot.Value;
        if (window.Owner != 0 ||
            string.IsNullOrWhiteSpace(window.Title) ||
            (window.ExtendedStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0 ||
            string.Equals(window.ClassName, "Shell_TrayWnd", StringComparison.Ordinal) ||
            string.Equals(window.ClassName, "Progman", StringComparison.Ordinal))
        {
            return;
        }

        if (!NativeMethods.ShowWindow(handle, NativeMethods.SW_HIDE))
        {
            return;
        }

        _hiddenWindows[handle] = new HiddenWindow(handle, window.Title, window.IsMaximized);
    }

    private void RestoreWindow(nint handle)
    {
        if (!_hiddenWindows.Remove(handle, out var hiddenWindow))
        {
            return;
        }

        if (!NativeMethods.IsWindow(handle))
        {
            return;
        }

        NativeMethods.ShowWindow(
            handle,
            hiddenWindow.WasMaximized ? NativeMethods.SW_SHOWMAXIMIZED : NativeMethods.SW_RESTORE);
        NativeMethods.SetForegroundWindow(handle);
    }

    private void RestoreAllWindows()
    {
        foreach (var handle in _hiddenWindows.Keys.ToArray())
        {
            RestoreWindow(handle);
        }
    }

    private void DisposeManagedState()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        RestoreAllWindows();
        NativeMethods.UnregisterHotKey(_messageWindow.Handle, HotkeyId);
        _messageWindow.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
    }

    private static string TrimMenuLabel(string title)
    {
        const int MaxLength = 60;
        if (title.Length <= MaxLength)
        {
            return title;
        }

        return $"{title[..MaxLength]}...";
    }

    private static string EscapeMenuLabel(string title)
    {
        return title.Replace("&", "&&", StringComparison.Ordinal);
    }

    private readonly record struct WindowSnapshot(nint Handle, string MenuLabel);
}
