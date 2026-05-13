using Microsoft.Win32;
using System.ComponentModel;
using System.Drawing;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows.Forms;
using WindowsAppCore;
using WindowsTrayCore;

namespace ProgramHider;

// Owns the tray icon, global hooks, and the user-facing window-management
// flows. Most application behavior is coordinated from this context.
internal sealed class ProgramHiderContext : ApplicationContext
{
    private const int HotkeyId = 0x1000;

    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _hideWindowMenu;
    private readonly HotkeyMessageWindow _messageWindow;
    private readonly JsonSettingsStore<AppSettings> _settingsStore;
    private readonly UiDispatcher _ui;
    private readonly NativeMethods.WinEventProc _minimizeEventCallback;
    private readonly NativeMethods.WinEventProc _foregroundEventCallback;
    private readonly nint _minimizeEventHook;
    private readonly nint _foregroundEventHook;
    private readonly Dictionary<nint, HiddenWindow> _hiddenWindows = new();
    private readonly IWindowPlatform _windowPlatform;
    private readonly WindowHideService _windowHideService;
    private readonly ActiveWindowTracker _activeWindowTracker;
    private readonly Icon _appIcon;
    private readonly AppLogger _logger;
    private readonly StartupOptions _startupOptions;
    private readonly System.Windows.Forms.Timer _maintenanceTimer;

    private AppSettings _settings;
    private DateTimeOffset _restoreUnlockedUntilUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _bulkRestoreUnlockedUntilUtc = DateTimeOffset.MinValue;
    private readonly SingleInstanceActivation _activation;
    private readonly bool _isElevated;
    private bool _safeModeEnabled;
    private bool _disposed;
    private readonly CancellationTokenSource _updateCts = new();
    private readonly HttpClient _updateHttpClient = new();
    private readonly UpdateChecker _updateChecker;

    public ProgramHiderContext(StartupOptions startupOptions, SingleInstanceActivation activation)
    {
        _startupOptions = startupOptions;
        _activation = activation;
        _logger = new AppLogger();
        _settingsStore = new JsonSettingsStore<AppSettings>(
            "ProgramHider",
            normalize: s => { s.Normalize(); return s; },
            options: new System.Text.Json.JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            });
        _windowPlatform = new Win32WindowPlatform();
        _windowHideService = new WindowHideService(_windowPlatform);
        _activeWindowTracker = new ActiveWindowTracker(_windowPlatform);
        _settings = _settingsStore.Load();
        _isElevated = ElevationService.IsCurrentProcessElevated();
        _safeModeEnabled = startupOptions.SafeMode;

        _ui = new UiDispatcher();
        _updateChecker = new UpdateChecker(_updateHttpClient, Application.ProductVersion, RepoInfo.Owner, RepoInfo.Name);
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

        _activation.ActivationRequested += (_, _) =>
            _ui.Post(() => ShowStatusBalloon("Program Hider", "Program Hider is already running."));

        _messageWindow = new HotkeyMessageWindow(OnHotkeyPressed);
        RegisterConfiguredHotkey();
        ApplyStartupRegistration(_settings.LaunchOnWindowsStartup, _settings.StartupDelaySeconds);

        // Listen for foreground changes and minimize starts so active-window
        // targeting and auto-hide rules can react without polling.
        _minimizeEventCallback = OnWindowEvent;
        _foregroundEventCallback = OnForegroundWindowEvent;
        _minimizeEventHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_MINIMIZESTART,
            NativeMethods.EVENT_SYSTEM_MINIMIZESTART,
            0,
            _minimizeEventCallback,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);
        _foregroundEventHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            0,
            _foregroundEventCallback,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

        _maintenanceTimer = new System.Windows.Forms.Timer
        {
            Interval = 15000
        };
        _maintenanceTimer.Tick += OnMaintenanceTick;
        _maintenanceTimer.Start();

        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        _logger.Write(
            "app.started",
            new
            {
                startup = _startupOptions.IsStartupLaunch,
                delaySeconds = _startupOptions.DelaySeconds,
                safeMode = _safeModeEnabled,
                elevated = _isElevated,
                pendingHideHandle = _startupOptions.PendingHideHandle == 0 ? null : $"0x{_startupOptions.PendingHideHandle.ToInt64():X}",
                settingsPath = _settingsStore.SettingsPath
            });

        if (_safeModeEnabled)
        {
            ShowStatusBalloon("Safe mode enabled", "Auto-hide automation is suspended until you turn safe mode off.");
        }

        if (_startupOptions.PendingHideHandle != 0)
            _ui.Post(TryHidePendingStartupWindow);

        _updateChecker.StartPeriodicChecks(TimeSpan.FromHours(24), r =>
            _ui.Post(() => _notifyIcon.ShowBalloonTip(5000, "ProgramHider update available",
                $"Version {r.LatestVersion} is available — visit GitHub to download.", ToolTipIcon.Info)),
            _updateCts.Token);
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

        CaptureCurrentActiveWindow();
        RebuildMenu();
        _menu.Show(Cursor.Position);
    }

    private void OnMenuOpening(object? sender, CancelEventArgs eventArgs)
    {
        CaptureCurrentActiveWindow();
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
                item.Click += (_, _) =>
                {
                    if (!HideWindow(candidate.Handle))
                    {
                        var snapshot = _windowPlatform.TryCreateWindowSnapshot(candidate.Handle);
                        if (snapshot is not null)
                        {
                            ReportHideFailure(snapshot.Value, "Hide window");
                        }
                    }
                };
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

            var safeModeItem = new ToolStripMenuItem("Safe mode (disable auto-hide)")
            {
                Checked = _safeModeEnabled,
                CheckOnClick = false
            };
            safeModeItem.Click += (_, _) => ToggleSafeMode();
            _menu.Items.Add(safeModeItem);

            var elevateItem = new ToolStripMenuItem(_isElevated ? "Running as administrator" : "Restart as administrator...");
            elevateItem.Enabled = !_isElevated;
            elevateItem.Click += (_, _) => RestartAsAdministrator();
            _menu.Items.Add(elevateItem);

            _menu.Items.Add(new ToolStripSeparator());

            var restoreBrowserItem = new ToolStripMenuItem("Restore browser...")
            {
                Enabled = _hiddenWindows.Count > 0
            };
            restoreBrowserItem.Click += (_, _) => OpenRestoreBrowser();
            _menu.Items.Add(restoreBrowserItem);

            BuildHiddenWindowItems();

            _menu.Items.Add(new ToolStripSeparator());

            var restoreAllItem = new ToolStripMenuItem("Restore all")
            {
                Enabled = _hiddenWindows.Count > 0
            };
            restoreAllItem.Click += (_, _) => RestoreAllWindows();
            _menu.Items.Add(restoreAllItem);

            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(StandardMenuItems.CreateAbout("ProgramHider", updateChecker: _updateChecker));
            _menu.Items.Add(StandardMenuItems.CreateCheckForUpdates(_updateChecker, _notifyIcon, "ProgramHider"));
            _menu.Items.Add(StandardMenuItems.CreateOpenLogs("ProgramHider"));
            _menu.Items.Add(new ToolStripSeparator());

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
        return _windowPlatform.EnumerateTopLevelWindows()
            .Where(window => window.Handle != _messageWindow.Handle)
            .Where(window => !_hiddenWindows.ContainsKey(window.Handle))
            .Where(window => WindowCatalog.IsManageableWindow(window))
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
            _logger.Write("hotkey.pressed", new { hotkey = _settings.Hotkey.ToDisplayString() });
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
        // Resolve through the tracker rather than only GetForegroundWindow so
        // tray interactions and hotkey timing races do not lose the user's
        // original target window.
        var activeWindow = GetActiveWindowSnapshot();
        if (activeWindow is null)
        {
            _logger.Write("window.hide_failed", new { operation = "Hide active window", reason = "no_active_window" });
            ShowStatusBalloon("No active window", "Program Hider could not resolve a hideable foreground window.");
            return;
        }

        if (!HideWindow(activeWindow.Value.Handle))
        {
            ReportHideFailure(activeWindow.Value, "Hide active window");
        }
    }

    private bool HideWindow(nint handle, WindowRuleMatchResult? existingMatch = null, bool wasAutomatic = false)
    {
        var snapshot = _windowPlatform.TryCreateWindowSnapshot(handle);
        if (snapshot is null)
        {
            return false;
        }

        var ruleMatch = existingMatch ?? WindowRuleMatchResult.Evaluate(_settings.WindowRules, snapshot.Value);
        if (!_windowHideService.TryHideWindow(handle, _messageWindow.Handle, _hiddenWindows, ruleMatch, out var hiddenWindow) ||
            hiddenWindow is null)
        {
            return false;
        }

        _logger.Write(
            "window.hidden",
            new
            {
                title = hiddenWindow.Title,
                process = hiddenWindow.ProcessName,
                className = hiddenWindow.ClassName,
                automatic = wasAutomatic,
                requirePin = hiddenWindow.RequirePinOnRestore,
                quiet = hiddenWindow.SuppressNotifications,
                matchedRules = ruleMatch.MatchingRules.Select(rule => rule.RuleName).ToArray()
            });
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
        if (!_hiddenWindows.TryGetValue(handle, out var hiddenWindow))
        {
            return;
        }

        if (!_windowHideService.TryRestoreWindow(handle, _hiddenWindows, _settings.RestoreWithoutFocus, out var restoredWindow) ||
            restoredWindow is null)
        {
            _logger.Write(
                "window.restore_skipped",
                new
                {
                    reason = "invalid_handle",
                    title = hiddenWindow.Title,
                    process = hiddenWindow.ProcessName
                });
            return;
        }

        hiddenWindow = restoredWindow;
        var restoredMonitor = _windowPlatform.TryGetMonitorDeviceNameForWindow(handle);
        var monitorMismatch =
            !string.IsNullOrWhiteSpace(hiddenWindow.MonitorDeviceName) &&
            !string.IsNullOrWhiteSpace(restoredMonitor) &&
            !string.Equals(hiddenWindow.MonitorDeviceName, restoredMonitor, StringComparison.OrdinalIgnoreCase);

        _logger.Write(
            "window.restored",
            new
            {
                title = hiddenWindow.Title,
                process = hiddenWindow.ProcessName,
                className = hiddenWindow.ClassName,
                focusRestored = !_settings.RestoreWithoutFocus,
                requirePin = hiddenWindow.RequirePinOnRestore,
                originalMonitor = hiddenWindow.MonitorDeviceName,
                restoredMonitor,
                monitorMismatch
            });
    }

    private void RestoreAllWindows()
    {
        RestoreSelectedWindows(_hiddenWindows.Keys.ToArray(), "restore all hidden windows", isBulkRestore: true);
    }

    private void RestoreAllWindowsWithoutPrompt(string reason)
    {
        if (_hiddenWindows.Count == 0)
        {
            return;
        }

        var handles = _hiddenWindows.Keys.ToArray();
        foreach (var handle in handles)
        {
            RestoreWindowCore(handle);
        }

        _logger.Write("window.restore_all_automatic", new { reason, count = handles.Length });
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
        _logger.Write("rule.added", new { rule = rule.RuleName, match = rule.DescribeMatch(), behavior = rule.DescribeBehavior() });
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
            ApplyStartupRegistration(_settings.LaunchOnWindowsStartup, _settings.StartupDelaySeconds);
            ClearUnlockCache();
            PersistSettings();

            var statusParts = new List<string>
            {
                $"Hotkey is now {_settings.Hotkey.ToDisplayString()}."
            };
            if (originalSettings.LaunchOnWindowsStartup != _settings.LaunchOnWindowsStartup ||
                originalSettings.StartupDelaySeconds != _settings.StartupDelaySeconds)
            {
                statusParts.Add(
                    _settings.LaunchOnWindowsStartup
                        ? $"Startup enabled with {_settings.StartupDelaySeconds}s delay."
                        : "Startup disabled.");
            }

            if (originalSettings.RestoreWithoutFocus != _settings.RestoreWithoutFocus)
            {
                statusParts.Add(
                    _settings.RestoreWithoutFocus
                        ? "Restores will no longer steal focus."
                        : "Restores will now bring windows to the foreground.");
            }

            if (originalSettings.RequirePinToRestore != _settings.RequirePinToRestore ||
                !string.Equals(originalSettings.PinHash, _settings.PinHash, StringComparison.Ordinal) ||
                !string.Equals(originalSettings.RestoreAllPinHash, _settings.RestoreAllPinHash, StringComparison.Ordinal) ||
                originalSettings.UnlockTimeoutMinutes != _settings.UnlockTimeoutMinutes)
            {
                statusParts.Add("Security settings updated.");
            }

            ShowStatusBalloon("Settings saved", string.Join(" ", statusParts));
            _logger.Write(
                "settings.saved",
                new
                {
                    hotkey = _settings.Hotkey.ToDisplayString(),
                    startup = _settings.LaunchOnWindowsStartup,
                    startupDelaySeconds = _settings.StartupDelaySeconds,
                    restoreWithoutFocus = _settings.RestoreWithoutFocus,
                    globalPin = _settings.RequirePinToRestore,
                    bulkPin = !string.IsNullOrWhiteSpace(_settings.RestoreAllPinHash),
                    unlockTimeoutMinutes = _settings.UnlockTimeoutMinutes,
                    rules = _settings.WindowRules.Count
                });
        }
        catch (Win32Exception)
        {
            _settings = originalSettings;
            RegisterConfiguredHotkey();
            ApplyStartupRegistration(_settings.LaunchOnWindowsStartup, _settings.StartupDelaySeconds);

            MessageBox.Show(
                $"Unable to register the requested hotkey {requestedHotkey}. Keep using {originalSettings.Hotkey.ToDisplayString()} or choose a different combination.",
                "Program Hider",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void OpenRestoreBrowser()
    {
        if (_hiddenWindows.Count == 0)
        {
            return;
        }

        using var restoreBrowser = new RestoreBrowserForm(_hiddenWindows.Values.ToArray(), _settings.RestoreWithoutFocus);
        if (restoreBrowser.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        if (restoreBrowser.RestoreAllRequested)
        {
            RestoreAllWindows();
            return;
        }

        RestoreSelectedWindows(
            restoreBrowser.SelectedHandles,
            restoreBrowser.SelectedHandles.Count > 1 ? "restore the selected windows" : "restore the selected window",
            isBulkRestore: restoreBrowser.SelectedHandles.Count > 1);
    }

    private void ToggleSafeMode()
    {
        _safeModeEnabled = !_safeModeEnabled;
        _notifyIcon.Text = BuildTrayText();
        ShowStatusBalloon(
            _safeModeEnabled ? "Safe mode enabled" : "Safe mode disabled",
            _safeModeEnabled
                ? "Auto-hide automation is suspended."
                : "Auto-hide automation is active again.");
        _logger.Write("safe_mode.toggled", new { enabled = _safeModeEnabled });
    }

    private void PersistSettings()
    {
        _settingsStore.Save(_settings);
        _notifyIcon.Text = BuildTrayText();
    }

    private bool EnsureRestoreAuthorized(string actionDescription, bool requirePinForSelection, bool isBulkRestore = false)
    {
        var useBulkSecret = isBulkRestore && !string.IsNullOrWhiteSpace(_settings.RestoreAllPinHash);
        var expectedHash = useBulkSecret ? _settings.RestoreAllPinHash : _settings.PinHash;
        var requiresPin = useBulkSecret || _settings.RequirePinToRestore || requirePinForSelection;
        if (!requiresPin || string.IsNullOrWhiteSpace(expectedHash))
        {
            return true;
        }

        if (IsUnlockStillValid(useBulkSecret))
        {
            return true;
        }

        using var prompt = new PinPromptForm(actionDescription);
        if (prompt.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(prompt.EnteredSecret))
        {
            _logger.Write("auth.cancelled", new { action = actionDescription, bulk = useBulkSecret });
            return false;
        }

        if (PinSecurity.VerifySecret(prompt.EnteredSecret, expectedHash))
        {
            RecordSuccessfulUnlock(useBulkSecret);
            _logger.Write("auth.success", new { action = actionDescription, bulk = useBulkSecret });
            return true;
        }

        _logger.Write("auth.failure", new { action = actionDescription, bulk = useBulkSecret });
        MessageBox.Show(
            "The restore PIN/password was incorrect.",
            "Program Hider",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
    }

    private bool IsUnlockStillValid(bool useBulkSecret)
    {
        var unlockUntil = useBulkSecret ? _bulkRestoreUnlockedUntilUtc : _restoreUnlockedUntilUtc;
        return unlockUntil > DateTimeOffset.UtcNow;
    }

    private void RecordSuccessfulUnlock(bool useBulkSecret)
    {
        var unlockUntil = _settings.UnlockTimeoutMinutes <= 0
            ? DateTimeOffset.MinValue
            : DateTimeOffset.UtcNow.AddMinutes(_settings.UnlockTimeoutMinutes);

        if (useBulkSecret)
        {
            _bulkRestoreUnlockedUntilUtc = unlockUntil;
        }
        else
        {
            _restoreUnlockedUntilUtc = unlockUntil;
        }
    }

    private void ClearUnlockCache()
    {
        _restoreUnlockedUntilUtc = DateTimeOffset.MinValue;
        _bulkRestoreUnlockedUntilUtc = DateTimeOffset.MinValue;
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
        RestoreSelectedWindows(handles, $"restore all hidden windows for {processName}", isBulkRestore: handles.Length > 1);
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

        _ui.Post(() => TryAutoHideMinimizedWindow(handle));
    }

    private void OnForegroundWindowEvent(
        nint eventHookHandle,
        uint eventType,
        nint handle,
        int objectId,
        int childId,
        uint eventThreadId,
        uint eventTime)
    {
        if (eventType != NativeMethods.EVENT_SYSTEM_FOREGROUND ||
            handle == 0 ||
            objectId != NativeMethods.OBJID_WINDOW ||
            childId != 0)
        {
            return;
        }

        _ui.Post(() => TrackForegroundWindow(handle));
    }

    private void TryAutoHideMinimizedWindow(nint handle)
    {
        if (_safeModeEnabled || _hiddenWindows.ContainsKey(handle))
        {
            return;
        }

        var snapshot = _windowPlatform.TryCreateWindowSnapshot(handle);
        if (snapshot is null || !WindowCatalog.IsManageableWindow(snapshot.Value))
        {
            return;
        }

        var ruleMatch = WindowRuleMatchResult.Evaluate(_settings.WindowRules, snapshot.Value);
        if (!ruleMatch.AutoHideOnMinimize)
        {
            return;
        }

        if (HideWindow(handle, ruleMatch, wasAutomatic: true) && !ruleMatch.SuppressNotifications)
        {
            ShowStatusBalloon(
                "Window hidden",
                $"{snapshot.Value.Title} was hidden automatically.");
        }
    }

    private void RestoreSelectedWindows(
        IReadOnlyList<nint> handles,
        string actionDescription = "restore the selected windows",
        bool isBulkRestore = false)
    {
        if (handles.Count == 0)
        {
            return;
        }

        var distinctHandles = handles.Distinct().ToArray();
        var requiresPin = distinctHandles
            .Select(handle => _hiddenWindows.TryGetValue(handle, out var hiddenWindow) ? hiddenWindow.RequirePinOnRestore : false)
            .Any(value => value);
        if (!EnsureRestoreAuthorized(actionDescription, requiresPin, isBulkRestore))
        {
            return;
        }

        foreach (var handle in distinctHandles)
        {
            RestoreWindowCore(handle);
        }
    }

    private void OnMaintenanceTick(object? sender, EventArgs eventArgs)
    {
        PruneDeadHiddenWindows();
    }

    private void PruneDeadHiddenWindows()
    {
        var removed = _windowHideService.PruneDeadWindows(_hiddenWindows);
        if (removed == 0)
        {
            return;
        }

        _logger.Write("maintenance.pruned_handles", new { count = removed });
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs eventArgs)
    {
        if (eventArgs.Reason != SessionSwitchReason.SessionLock || !_settings.RestoreHiddenWindowsOnSessionLock)
        {
            return;
        }

        RestoreAllWindowsWithoutPrompt("session-lock");
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs eventArgs)
    {
        if (eventArgs.Mode != PowerModes.Suspend || !_settings.RestoreHiddenWindowsOnSuspend)
        {
            return;
        }

        RestoreAllWindowsWithoutPrompt("suspend");
    }

    private void DisposeManagedState()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _maintenanceTimer.Stop();
        _maintenanceTimer.Dispose();
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        RestoreAllWindowsWithoutPrompt("app-exit");
        NativeMethods.UnregisterHotKey(_messageWindow.Handle, HotkeyId);
        if (_minimizeEventHook != 0)
        {
            NativeMethods.UnhookWinEvent(_minimizeEventHook);
        }
        if (_foregroundEventHook != 0)
        {
            NativeMethods.UnhookWinEvent(_foregroundEventHook);
        }

        _logger.Write("app.stopped", new { remainingHiddenWindows = _hiddenWindows.Count });

        _updateCts.Cancel();
        _updateCts.Dispose();
        _updateHttpClient.Dispose();
        _activation.Dispose();
        _messageWindow.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _appIcon.Dispose();
        _ui.Dispose();
    }

    private void ShowStatusBalloon(string title, string message)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(2500);
    }

    private string BuildTrayText()
    {
        var suffixParts = new List<string>();
        if (_isElevated)
        {
            suffixParts.Add("Admin");
        }

        if (_settings.RequirePinToRestore ||
            _settings.WindowRules.Any(rule => rule.RequirePinOnRestore) ||
            !string.IsNullOrWhiteSpace(_settings.RestoreAllPinHash))
        {
            suffixParts.Add("Locked");
        }

        if (_safeModeEnabled)
        {
            suffixParts.Add("Safe");
        }

        var suffix = suffixParts.Count == 0 ? string.Empty : $" [{string.Join(", ", suffixParts)}]";
        return $"Program Hider v{Application.ProductVersion}{suffix}";
    }

    private NativeWindowSnapshot? GetActiveWindowSnapshot()
    {
        return _activeWindowTracker.ResolveSnapshot(CanTrackActiveWindow);
    }

    private void CaptureCurrentActiveWindow()
    {
        _activeWindowTracker.CaptureCurrentSnapshot(CanTrackActiveWindow);
    }

    private void TrackForegroundWindow(nint handle)
    {
        _activeWindowTracker.CaptureSnapshotForHandle(handle, CanTrackActiveWindow);
    }

    private bool CanTrackActiveWindow(NativeWindowSnapshot snapshot)
    {
        return snapshot.Handle != _messageWindow.Handle &&
               !_hiddenWindows.ContainsKey(snapshot.Handle) &&
               WindowCatalog.IsManageableWindow(snapshot);
    }

    private void ReportHideFailure(NativeWindowSnapshot snapshot, string operation)
    {
        bool? targetElevated = snapshot.ProcessId == 0 ? null : ElevationService.IsProcessElevated(snapshot.ProcessId);
        // A false return from ShowWindow is not enough to prove elevation was
        // the cause, but an elevated target is the most actionable recovery
        // path we can offer from the tray UI.
        var shouldOfferElevation = !_isElevated && (targetElevated is null || targetElevated.Value);
        var message = shouldOfferElevation
            ? $"Could not hide '{snapshot.Title}' ({snapshot.ProcessName}). Program Hider can relaunch as administrator and retry."
            : $"Could not hide '{snapshot.Title}' ({snapshot.ProcessName}).";
        _logger.Write(
            "window.hide_failed",
            new
            {
                operation,
                snapshot.Title,
                snapshot.ProcessName,
                snapshot.ClassName,
                snapshot.ProcessId,
                elevated = _isElevated,
                targetElevated
            });

        if (shouldOfferElevation)
        {
            ShowStatusBalloon(operation, message);
            PromptForElevationRetry(snapshot, operation);
            return;
        }

        ShowStatusBalloon(operation, message);
    }

    private void PromptForElevationRetry(NativeWindowSnapshot snapshot, string operation)
    {
        var result = MessageBox.Show(
            $"Program Hider could not hide '{snapshot.Title}' ({snapshot.ProcessName}).{Environment.NewLine}{Environment.NewLine}Restart Program Hider as administrator and retry this window?",
            operation,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (result != DialogResult.Yes)
        {
            _logger.Write("elevation.declined", new { operation, snapshot.Title, snapshot.ProcessName, snapshot.ProcessId });
            return;
        }

        RestartAsAdministrator(snapshot.Handle);
    }

    private void RestartAsAdministrator(nint? pendingHideHandle = null)
    {
        var elevationResult = ElevationService.TryRestartElevated(_startupOptions, pendingHideHandle);
        _logger.Write(
            "elevation.attempt",
            new
            {
                pendingHideHandle = pendingHideHandle.HasValue ? $"0x{pendingHideHandle.Value.ToInt64():X}" : null,
                result = elevationResult.ToString()
            });

        switch (elevationResult)
        {
            case ElevationAttemptResult.NotNeeded:
                ShowStatusBalloon("Already elevated", "Program Hider is already running as administrator.");
                return;
            case ElevationAttemptResult.Relaunched:
                ExitThread();
                return;
            case ElevationAttemptResult.Cancelled:
                ShowStatusBalloon("Elevation cancelled", "Program Hider stayed in the current session.");
                return;
            default:
                MessageBox.Show(
                    "Program Hider could not relaunch as administrator.",
                    "Program Hider",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
        }
    }

    private void TryHidePendingStartupWindow()
    {
        var pendingHandle = _startupOptions.PendingHideHandle;
        if (pendingHandle == 0)
        {
            return;
        }

        // The target window may still be recreating or finishing activation
        // while the elevated retry instance starts.
        for (var attempt = 0; attempt < 8; attempt++)
        {
            Application.DoEvents();
            if (HideWindow(pendingHandle))
            {
                ShowStatusBalloon("Elevated retry succeeded", "Program Hider relaunched as administrator and hid the requested window.");
                _logger.Write("elevation.retry_succeeded", new { handle = $"0x{pendingHandle.ToInt64():X}" });
                return;
            }

            Thread.Sleep(250);
        }

        _logger.Write("elevation.retry_failed", new { handle = $"0x{pendingHandle.ToInt64():X}" });
        ShowStatusBalloon("Elevated retry failed", "The requested window could not be hidden after relaunch.");
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

    private static void ApplyStartupRegistration(bool enabled, int delaySeconds)
    {
        var arguments = $"--startup --delay={Math.Clamp(delaySeconds, 0, 300)}";
        var reg = new RunKeyStartupRegistration("ProgramHider", Application.ExecutablePath, arguments);
        if (enabled) reg.Register();
        else reg.Unregister();
    }

    private readonly record struct WindowSnapshot(nint Handle, string MenuLabel, string ProcessName);
}
