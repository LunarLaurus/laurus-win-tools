using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Windows.Forms;
using WindowsAppCore;
using WindowsTrayCore;

namespace ClipTray;

public sealed class ClipTrayContext : ApplicationContext
{
    private const int HOTKEY_ID_PICKER = 1;
    private const int HOTKEY_ID_PAUSE  = 2;

    private readonly AppSettings _settings;
    private readonly SingleInstanceActivation _activation;
    private readonly UiDispatcher _ui = new();
    private readonly RunKeyStartupRegistration _startup;

    private readonly TrayIcon _trayIcon;
    private readonly ClipboardListener _listener;
    private readonly SessionLockMonitor _sessionLock;
    private readonly HotkeyRegistration _hotkey;
    private readonly ClipboardHistory _history;
    private readonly ImageStore _images;
    private readonly CapturePipeline _capture;
    private readonly string _indexPath;

    private readonly UpdateChecker _updateChecker;
    private readonly HttpClient _updateHttpClient = new();
    private readonly CancellationTokenSource _updateCts = new();

    private SettingsForm? _settingsForm;
    private PickerForm? _pickerForm;
    private ToolStripMenuItem? _pauseMenuItem;

    public ClipTrayContext(AppSettings settings, SingleInstanceActivation activation)
    {
        _settings = settings;
        _activation = activation;
        _startup = new RunKeyStartupRegistration("ClipTray",
            Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty,
            "ClipTray - clipboard history with hotkey picker");
        _updateChecker = new UpdateChecker(_updateHttpClient,
            Application.ProductVersion, RepoInfo.Owner, RepoInfo.Name);

        var dataDir = AppPaths.HistoryDir("ClipTray");
        Directory.CreateDirectory(dataDir);
        _indexPath = Path.Combine(dataDir, "index.json");
        _images = new ImageStore(Path.Combine(dataDir, "items"));
        _history = ClipboardHistory.Load(_indexPath, _settings.TextHistoryCap, _settings.ImageHistoryCap);
        _images.SweepOrphans(_history.Items
            .Where(i => i.Kind == HistoryKind.Image)
            .Select(i => i.Hash));

        _sessionLock = new SessionLockMonitor();
        _capture = new CapturePipeline(_settings, _history, _images, _sessionLock, _indexPath);

        _listener = new ClipboardListener();
        _listener.ClipboardChanged += (_, _) => _ui.Post(() => _capture.OnClipboardChanged());

        _hotkey = new HotkeyRegistration();
        _hotkey.Pressed += (_, id) => _ui.Post(() => OnHotkeyPressed(id));
        RegisterHotkeys();

        _trayIcon = TrayIcon.ForApp("ClipTray");
        _trayIcon.TooltipText = $"ClipTray v{VersionFormatter.TrimSemverSuffix(Application.ProductVersion)}";
        _trayIcon.ContextMenuStrip = BuildMenu();
        _trayIcon.Visible = true;
        _trayIcon.DoubleClick += (_, _) => _ui.Post(OpenSettings);

        _activation.ActivationRequested += (_, _) => _ui.Post(OpenSettings);

        _updateChecker.StartPeriodicChecks(TimeSpan.FromHours(24), r =>
            _ui.Post(() => _trayIcon.ShowBalloonTip(5000, "ClipTray update available",
                $"Version {r.LatestVersion} is available. Visit GitHub to download.", ToolTipIcon.Info)),
            _updateCts.Token);

        ShowFirstRunBalloonIfNeeded();
    }

    private void RegisterHotkeys()
    {
        if (_settings.PickerHotkeyKey != Keys.None)
        {
            _hotkey.Register(HOTKEY_ID_PICKER,
                _settings.PickerHotkeyModifiers | HotkeyModifiers.NoRepeat,
                _settings.PickerHotkeyKey);
        }
        if (_settings.PauseHotkeyKey != Keys.None)
        {
            _hotkey.Register(HOTKEY_ID_PAUSE,
                _settings.PauseHotkeyModifiers | HotkeyModifiers.NoRepeat,
                _settings.PauseHotkeyKey);
        }
    }

    private void OnHotkeyPressed(int id)
    {
        switch (id)
        {
            case HOTKEY_ID_PICKER: OpenPicker(); break;
            case HOTKEY_ID_PAUSE:  TogglePause(); break;
        }
    }

    private void OpenPicker()
    {
        var targetWindow = ForegroundProcessProbe.GetForegroundHwnd();
        if (_pickerForm is null || _pickerForm.IsDisposed)
            _pickerForm = new PickerForm(_history, _settings, _images);
        _pickerForm.ShowAtCursor(targetWindow);
    }

    private void TogglePause()
    {
        _settings.PauseCapture = !_settings.PauseCapture;
        _settings.Save();
        if (_pauseMenuItem is not null)
            _pauseMenuItem.Checked = _settings.PauseCapture;
        _trayIcon.ShowBalloonTip(2000, "ClipTray",
            _settings.PauseCapture ? "Capture paused" : "Capture resumed",
            ToolTipIcon.Info);
    }

    private void OpenSettings()
    {
        if (_settingsForm is null || _settingsForm.IsDisposed)
        {
            _settingsForm = new SettingsForm(_settings);
            _settingsForm.FormClosed += (_, _) =>
            {
                // Re-register hotkeys in case the user changed them.
                _hotkey.Unregister(HOTKEY_ID_PICKER);
                _hotkey.Unregister(HOTKEY_ID_PAUSE);
                RegisterHotkeys();
                ApplyStartupRegistration();
            };
        }
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    private void ApplyStartupRegistration()
    {
        if (_settings.RunAtStartup)
            _startup.Register();
        else
            _startup.Unregister();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show picker", null, (_, _) => OpenPicker());
        _pauseMenuItem = new ToolStripMenuItem("Pause capture")
        {
            CheckOnClick = true,
            Checked = _settings.PauseCapture,
        };
        _pauseMenuItem.Click += (_, _) => TogglePause();
        menu.Items.Add(_pauseMenuItem);
        menu.Items.Add("Clear history…", null, (_, _) => ClearHistory());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings…", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(StandardMenuItems.CreateAbout("ClipTray", null, _updateChecker, _trayIcon.Icon));
        menu.Items.Add(StandardMenuItems.CreateCheckForUpdates(_updateChecker, _trayIcon, "ClipTray"));
        menu.Items.Add(StandardMenuItems.CreateOpenLogs("ClipTray"));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => ExitThread());
        return menu;
    }

    private void ClearHistory()
    {
        var result = MessageBox.Show(
            "Clear all clipboard history? Pinned items are preserved.",
            "ClipTray",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning);
        if (result != DialogResult.OK) return;

        _history.Clear(preservePinned: true);
        _history.Save(_indexPath);
        _images.SweepOrphans(_history.Items
            .Where(i => i.Kind == HistoryKind.Image)
            .Select(i => i.Hash));
    }

    private void ShowFirstRunBalloonIfNeeded()
    {
        if (_settings.ShownFirstRunWelcome) return;
        _settings.ShownFirstRunWelcome = true;
        _settings.Save();

        var hk = HotkeyDisplay(_settings.PickerHotkeyModifiers, _settings.PickerHotkeyKey);
        _trayIcon.ShowBalloonTip(8000,
            "ClipTray is running",
            $"Press {hk} anywhere to open the clipboard picker. Right-click the tray icon for settings.",
            ToolTipIcon.Info);
    }

    private static string HotkeyDisplay(HotkeyModifiers mods, Keys key)
    {
        var parts = new List<string>();
        if (mods.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(HotkeyModifiers.Alt))     parts.Add("Alt");
        if (mods.HasFlag(HotkeyModifiers.Shift))   parts.Add("Shift");
        if (mods.HasFlag(HotkeyModifiers.Win))     parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _updateCts.Cancel();
            _updateHttpClient.Dispose();
            _hotkey.Dispose();
            _listener.Dispose();
            _sessionLock.Dispose();
            _trayIcon.Dispose();
            _ui.Dispose();
        }
        base.Dispose(disposing);
    }
}
