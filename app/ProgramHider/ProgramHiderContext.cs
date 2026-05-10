using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ProgramHider;

internal sealed class ProgramHiderContext : ApplicationContext
{
    private const int HotkeyId = 0x1000;

    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _hideWindowMenu;
    private readonly HotkeyMessageWindow _messageWindow;
    private readonly SettingsStore _settingsStore;
    private readonly SynchronizationContext _uiContext;
    private readonly NativeMethods.WinEventProc _minimizeEventCallback;
    private readonly nint _minimizeEventHook;
    private readonly Dictionary<nint, HiddenWindow> _hiddenWindows = new();
    private readonly Icon _appIcon;

    private AppSettings _settings;
    private bool _disposed;

    public ProgramHiderContext()
    {
        _settingsStore = new SettingsStore();
        _settings = _settingsStore.Load();
        _settings.Normalize();

        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

        _menu = new ContextMenuStrip();
        _menu.Opening += OnMenuOpening;

        _hideWindowMenu = new ToolStripMenuItem("Hide window");

        _notifyIcon = new NotifyIcon
        {
            Text = BuildTrayText(),
            Icon = _appIcon,
            ContextMenuStrip = _menu,
            Visible = true
        };
        _notifyIcon.MouseClick += OnNotifyIconMouseClick;

        _messageWindow = new HotkeyMessageWindow(OnHotkeyPressed);
        RegisterConfiguredHotkey();

        _minimizeEventCallback = OnWindowEvent;
        _minimizeEventHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_MINIMIZESTART,
            NativeMethods.EVENT_SYSTEM_MINIMIZESTART,
            0,
            _minimizeEventCallback,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);
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

            var hideActiveItem = new ToolStripMenuItem($"Hide active window\t{_settings.Hotkey.ToDisplayString()}");
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

            var addRuleItem = new ToolStripMenuItem("Always auto-hide active app on minimize");
            addRuleItem.Click += (_, _) => AddActiveProcessRule();
            _menu.Items.Add(addRuleItem);

            var settingsItem = new ToolStripMenuItem("Settings...");
            settingsItem.Click += (_, _) => OpenSettings();
            _menu.Items.Add(settingsItem);

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
            .Where(window => IsManageableWindow(window))
            .OrderBy(window => window.Title, StringComparer.OrdinalIgnoreCase)
            .Select(window => new WindowSnapshot(
                window.Handle,
                $"{EscapeMenuLabel(TrimMenuLabel(window.Title))} ({window.ProcessName})",
                window.ProcessName))
            .ToArray();
    }

    private void OnHotkeyPressed(int hotkeyId)
    {
        if (hotkeyId == HotkeyId)
        {
            HideActiveWindow();
        }
    }

    private void RegisterConfiguredHotkey()
    {
        NativeMethods.UnregisterHotKey(_messageWindow.Handle, HotkeyId);

        if (!NativeMethods.RegisterHotKey(
                _messageWindow.Handle,
                HotkeyId,
                _settings.Hotkey.ToNativeModifiers(),
                (uint)_settings.Hotkey.Key))
        {
            throw new Win32Exception("Unable to register the configured hotkey.");
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
        if (snapshot is null || !IsManageableWindow(snapshot.Value))
        {
            return;
        }

        if (!NativeMethods.ShowWindow(handle, NativeMethods.SW_HIDE))
        {
            return;
        }

        _hiddenWindows[handle] = new HiddenWindow(
            handle,
            snapshot.Value.Title,
            snapshot.Value.ProcessName,
            snapshot.Value.IsMaximized);
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

    private void AddActiveProcessRule()
    {
        var activeWindow = NativeMethods.TryCreateWindowSnapshot(NativeMethods.GetForegroundWindow());
        if (activeWindow is null || string.IsNullOrWhiteSpace(activeWindow.Value.ProcessName))
        {
            MessageBox.Show(
                "No eligible active application window was detected.",
                "Program Hider",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (!TryAddAutoHideProcess(activeWindow.Value.ProcessName))
        {
            MessageBox.Show(
                $"The app '{activeWindow.Value.ProcessName}' is already in the auto-hide rules.",
                "Program Hider",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ShowStatusBalloon(
            "Auto-hide rule added",
            $"{activeWindow.Value.ProcessName} will now hide to Program Hider when minimized.");
    }

    private bool TryAddAutoHideProcess(string processName)
    {
        var normalized = processName.Trim();
        if (string.IsNullOrWhiteSpace(normalized) ||
            _settings.AutoHideProcessNames.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        _settings.AutoHideProcessNames.Add(normalized);
        _settings.Normalize();
        PersistSettings();
        return true;
    }

    private void OpenSettings()
    {
        using var settingsForm = new SettingsForm(
            _settings.Clone(),
            EnumerateCandidateWindows().Select(window => window.ProcessName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            _settingsStore.SettingsPath);

        if (settingsForm.ShowDialog() != DialogResult.OK || settingsForm.UpdatedSettings is null)
        {
            return;
        }

        var originalSettings = _settings.Clone();
        var requestedSettings = settingsForm.UpdatedSettings.Clone();
        var requestedHotkey = requestedSettings.Hotkey.ToDisplayString();
        try
        {
            _settings = requestedSettings;
            _settings.Normalize();
            RegisterConfiguredHotkey();
            PersistSettings();
            ShowStatusBalloon("Settings saved", $"Hotkey is now {_settings.Hotkey.ToDisplayString()}.");
        }
        catch (Win32Exception)
        {
            _settings = originalSettings;
            RegisterConfiguredHotkey();

            MessageBox.Show(
                $"Unable to register the requested hotkey {requestedHotkey}. Keep using {originalSettings.Hotkey.ToDisplayString()} or choose a different combination.",
                "Program Hider",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void PersistSettings()
    {
        _settingsStore.Save(_settings);
        _notifyIcon.Text = BuildTrayText();
    }

    private void OnWindowEvent(
        nint eventHookHandle,
        uint eventType,
        nint handle,
        int objectId,
        int childId,
        uint eventThreadId,
        uint eventTime)
    {
        if (eventType != NativeMethods.EVENT_SYSTEM_MINIMIZESTART || handle == 0)
        {
            return;
        }

        _uiContext.Post(
            static state =>
            {
                var payload = (MinimizeEventPayload)state!;
                payload.Context.TryAutoHideMinimizedWindow(payload.Handle);
            },
            new MinimizeEventPayload(this, handle));
    }

    private void TryAutoHideMinimizedWindow(nint handle)
    {
        if (_hiddenWindows.ContainsKey(handle))
        {
            return;
        }

        var snapshot = NativeMethods.TryCreateWindowSnapshot(handle);
        if (snapshot is null || !IsManageableWindow(snapshot.Value))
        {
            return;
        }

        if (!_settings.AutoHideProcessNames.Contains(snapshot.Value.ProcessName, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        HideWindow(handle);
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
        if (_minimizeEventHook != 0)
        {
            NativeMethods.UnhookWinEvent(_minimizeEventHook);
        }

        _messageWindow.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _appIcon.Dispose();
    }

    private void ShowStatusBalloon(string title, string message)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(2500);
    }

    private string BuildTrayText()
    {
        return $"Program Hider v{Application.ProductVersion}";
    }

    private static bool IsManageableWindow(NativeWindowSnapshot window)
    {
        return !string.IsNullOrWhiteSpace(window.Title) &&
               !string.IsNullOrWhiteSpace(window.ProcessName) &&
               window.Owner == 0 &&
               (window.ExtendedStyle & NativeMethods.WS_EX_TOOLWINDOW) == 0 &&
               !string.Equals(window.ClassName, "Shell_TrayWnd", StringComparison.Ordinal) &&
               !string.Equals(window.ClassName, "Progman", StringComparison.Ordinal);
    }

    private static string TrimMenuLabel(string title)
    {
        const int MaxLength = 52;
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

    private readonly record struct WindowSnapshot(nint Handle, string MenuLabel, string ProcessName);
    private readonly record struct MinimizeEventPayload(ProgramHiderContext Context, nint Handle);
}
