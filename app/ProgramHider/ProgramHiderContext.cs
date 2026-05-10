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
        StartupRegistration.Apply(_settings.LaunchOnWindowsStartup);

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

            var quickRuleItem = new ToolStripMenuItem("Create process rule from active window");
            quickRuleItem.Click += (_, _) => AddActiveProcessRule();
            _menu.Items.Add(quickRuleItem);

            var inspectWindowItem = new ToolStripMenuItem("Inspect active window...");
            inspectWindowItem.Click += (_, _) => InspectActiveWindow();
            _menu.Items.Add(inspectWindowItem);

            var settingsItem = new ToolStripMenuItem("Settings...");
            settingsItem.Click += (_, _) => OpenSettings();
            _menu.Items.Add(settingsItem);

            _menu.Items.Add(new ToolStripSeparator());

            BuildHiddenWindowItems();

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

    private bool HideWindow(nint handle, WindowRuleMatchResult? existingMatch = null)
    {
        if (handle == 0 || handle == _messageWindow.Handle || _hiddenWindows.ContainsKey(handle))
        {
            return false;
        }

        var snapshot = NativeMethods.TryCreateWindowSnapshot(handle);
        if (snapshot is null || !IsManageableWindow(snapshot.Value))
        {
            return false;
        }

        var ruleMatch = existingMatch ?? WindowRuleMatchResult.Evaluate(_settings.WindowRules, snapshot.Value);
        if (!NativeMethods.ShowWindow(handle, NativeMethods.SW_HIDE))
        {
            return false;
        }

        _hiddenWindows[handle] = new HiddenWindow(
            handle,
            snapshot.Value.Title,
            snapshot.Value.ProcessName,
            snapshot.Value.ClassName,
            snapshot.Value.IsMaximized,
            DateTimeOffset.UtcNow,
            ruleMatch.RequirePinOnRestore,
            ruleMatch.SuppressNotifications);
        return true;
    }

    private void RestoreWindow(nint handle)
    {
        if (!_hiddenWindows.TryGetValue(handle, out var hiddenWindow))
        {
            return;
        }

        if (!EnsureRestoreAuthorized("restore the selected window", hiddenWindow.RequirePinOnRestore))
        {
            return;
        }

        RestoreWindowCore(handle);
    }

    private void RestoreWindowCore(nint handle)
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
        if (_hiddenWindows.Count == 0)
        {
            return;
        }

        var requiresPin = _hiddenWindows.Values.Any(window => window.RequirePinOnRestore);
        if (!EnsureRestoreAuthorized("restore all hidden windows", requiresPin))
        {
            return;
        }

        foreach (var handle in _hiddenWindows.Keys.ToArray())
        {
            RestoreWindowCore(handle);
        }
    }

    private void AddActiveProcessRule()
    {
        var activeWindow = GetActiveWindowSnapshot();
        if (activeWindow is null || string.IsNullOrWhiteSpace(activeWindow.Value.ProcessName))
        {
            MessageBox.Show(
                "No eligible active application window was detected.",
                "Program Hider",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var rule = new WindowRule
        {
            RuleName = $"{activeWindow.Value.ProcessName} auto-hide",
            MatchProcessName = activeWindow.Value.ProcessName,
            AutoHideOnMinimize = true
        };
        rule.Normalize();

        if (!TryAddWindowRule(rule))
        {
            MessageBox.Show(
                $"A matching rule for '{activeWindow.Value.ProcessName}' already exists.",
                "Program Hider",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ShowStatusBalloon(
            "Auto-hide rule added",
            $"{activeWindow.Value.ProcessName} will now hide to Program Hider when minimized.");
    }

    private void InspectActiveWindow()
    {
        var activeWindow = GetActiveWindowSnapshot();
        if (activeWindow is null)
        {
            MessageBox.Show(
                "No eligible active application window was detected.",
                "Program Hider",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var inspector = new ActiveWindowInspectorForm(activeWindow.Value);
        if (inspector.ShowDialog() != DialogResult.OK || inspector.CreatedRule is null)
        {
            return;
        }

        if (!TryAddWindowRule(inspector.CreatedRule))
        {
            MessageBox.Show(
                "A matching rule for the inspected window already exists.",
                "Program Hider",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ShowStatusBalloon(
            "Rule added",
            $"{inspector.CreatedRule.RuleName} is now active.");
    }

    private bool TryAddWindowRule(WindowRule rule)
    {
        rule.Normalize();
        if (!rule.HasAnyMatchField ||
            _settings.WindowRules.Any(existing => string.Equals(existing.GetIdentityKey(), rule.GetIdentityKey(), StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        _settings.WindowRules.Add(rule.Clone());
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
            StartupRegistration.Apply(_settings.LaunchOnWindowsStartup);
            PersistSettings();
            ShowStatusBalloon("Settings saved", $"Hotkey is now {_settings.Hotkey.ToDisplayString()}.");
        }
        catch (Win32Exception)
        {
            _settings = originalSettings;
            RegisterConfiguredHotkey();
            StartupRegistration.Apply(_settings.LaunchOnWindowsStartup);

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

    private bool EnsureRestoreAuthorized(string actionDescription, bool requirePinForSelection)
    {
        if ((!_settings.RequirePinToRestore && !requirePinForSelection) || string.IsNullOrWhiteSpace(_settings.PinHash))
        {
            return true;
        }

        using var prompt = new PinPromptForm(actionDescription);
        if (prompt.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(prompt.EnteredSecret))
        {
            return false;
        }

        if (PinSecurity.VerifySecret(prompt.EnteredSecret, _settings.PinHash))
        {
            return true;
        }

        MessageBox.Show(
            "The restore PIN/password was incorrect.",
            "Program Hider",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
    }

    private void BuildHiddenWindowItems()
    {
        if (_hiddenWindows.Count == 0)
        {
            _menu.Items.Add(new ToolStripMenuItem("No hidden windows") { Enabled = false });
            return;
        }

        foreach (var processGroup in _hiddenWindows.Values
                     .OrderByDescending(window => window.HiddenAtUtc)
                     .ThenBy(window => window.ProcessName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(window => window.Title, StringComparer.OrdinalIgnoreCase)
                     .GroupBy(window => window.ProcessName, StringComparer.OrdinalIgnoreCase))
        {
            var windows = processGroup.ToList();
            if (windows.Count == 1)
            {
                var onlyWindow = windows[0];
                var item = new ToolStripMenuItem(
                    $"Restore: {EscapeMenuLabel(onlyWindow.Title)} ({onlyWindow.ProcessName}){BuildProtectedSuffix(onlyWindow.RequirePinOnRestore)}");
                item.Click += (_, _) => RestoreWindow(onlyWindow.Handle);
                _menu.Items.Add(item);
                continue;
            }

            var groupItem = new ToolStripMenuItem(
                $"Restore: {processGroup.Key} ({windows.Count}){BuildProtectedSuffix(windows.Any(window => window.RequirePinOnRestore))}");

            var restoreAllForProcess = new ToolStripMenuItem("Restore all in this app");
            restoreAllForProcess.Click += (_, _) => RestoreWindowsForProcess(processGroup.Key);
            groupItem.DropDownItems.Add(restoreAllForProcess);
            groupItem.DropDownItems.Add(new ToolStripSeparator());

            foreach (var hiddenWindow in windows)
            {
                var childItem = new ToolStripMenuItem(
                    $"{EscapeMenuLabel(TrimMenuLabel(hiddenWindow.Title))}{BuildProtectedSuffix(hiddenWindow.RequirePinOnRestore)}");
                childItem.Click += (_, _) => RestoreWindow(hiddenWindow.Handle);
                groupItem.DropDownItems.Add(childItem);
            }

            _menu.Items.Add(groupItem);
        }
    }

    private void RestoreWindowsForProcess(string processName)
    {
        var handles = _hiddenWindows.Values
            .Where(window => string.Equals(window.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
            .Select(window => window.Handle)
            .ToArray();

        if (handles.Length == 0)
        {
            return;
        }

        var requiresPin = handles
            .Select(handle => _hiddenWindows.TryGetValue(handle, out var hiddenWindow) ? hiddenWindow.RequirePinOnRestore : false)
            .Any(value => value);
        if (!EnsureRestoreAuthorized($"restore all hidden windows for {processName}", requiresPin))
        {
            return;
        }

        foreach (var handle in handles)
        {
            RestoreWindowCore(handle);
        }
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

        var ruleMatch = WindowRuleMatchResult.Evaluate(_settings.WindowRules, snapshot.Value);
        if (!ruleMatch.AutoHideOnMinimize)
        {
            return;
        }

        if (HideWindow(handle, ruleMatch) && !ruleMatch.SuppressNotifications)
        {
            ShowStatusBalloon(
                "Window hidden",
                $"{snapshot.Value.Title} was hidden automatically.");
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
        var suffix = _settings.RequirePinToRestore || _settings.WindowRules.Any(rule => rule.RequirePinOnRestore)
            ? " [Locked]"
            : string.Empty;
        return $"Program Hider v{Application.ProductVersion}{suffix}";
    }

    private NativeWindowSnapshot? GetActiveWindowSnapshot()
    {
        var snapshot = NativeMethods.TryCreateWindowSnapshot(NativeMethods.GetForegroundWindow());
        if (snapshot is null || !IsManageableWindow(snapshot.Value))
        {
            return null;
        }

        return snapshot;
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

    private static string BuildProtectedSuffix(bool requiresPin)
    {
        return requiresPin ? " [PIN]" : string.Empty;
    }

    private readonly record struct WindowSnapshot(nint Handle, string MenuLabel, string ProcessName);
    private readonly record struct MinimizeEventPayload(ProgramHiderContext Context, nint Handle);
}
