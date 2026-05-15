using System.Diagnostics;
using System.Drawing;
using Microsoft.Win32;
using SoundTracker.App.Audio;
using SoundTracker.App.Diagnostics;
using SoundTracker.App.History;
using WindowsTrayCore;
using RunKeyStartupRegistration = WindowsAppCore.RunKeyStartupRegistration;
using SingleInstanceActivation = WindowsAppCore.SingleInstanceActivation;
using StartupRegistrationResult = WindowsAppCore.StartupRegistrationResult;
using UpdateChecker = WindowsAppCore.UpdateChecker;

namespace SoundTracker.App;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly IAudioSessionSource _audioSessionSource;
    private readonly AudioActivityTimeline _activityTimeline;
    private readonly bool _ownsAudioSessionSource;
    private readonly bool _ownsActivityTimeline;
    private readonly RecentActivityForm _recentActivityForm;
    private readonly ToolStripMenuItem _volumeStatusItem;
    private readonly ToolStripMenuItem _activeStatusItem;
    private readonly ToolStripMenuItem _recentStatusItem;
    private readonly ToolStripMenuItem _runAtStartupItem;
    private readonly TrayIcon _notifyIcon;
    private readonly UiDispatcher _ui;
    private readonly System.Windows.Forms.Timer _leftClickTimer;
    private readonly SingleInstanceActivation? _activation;
    private readonly RunKeyStartupRegistration _startup;
    private readonly AudioIconProvider _iconProvider;
    private readonly TrayIconManager _iconManager;
    private bool _shuttingDown;
    private readonly CancellationTokenSource _updateCts = new();
    private readonly HttpClient _updateHttpClient = new();
    private readonly UpdateChecker _updateChecker;

    public TrayApplicationContext(SingleInstanceActivation? activation = null)
        : this(
            new AudioSessionMonitor(),
            activityTimeline: null,
            ownsAudioSessionSource: true,
            ownsActivityTimeline: true,
            showNotifyIcon: true,
            activation)
    {
    }

    internal TrayApplicationContext(
        IAudioSessionSource audioSessionSource,
        bool ownsAudioSessionSource,
        bool showNotifyIcon)
        : this(
            audioSessionSource,
            activityTimeline: null,
            ownsAudioSessionSource,
            ownsActivityTimeline: true,
            showNotifyIcon,
            activation: null)
    {
    }

    internal TrayApplicationContext(
        IAudioSessionSource audioSessionSource,
        AudioActivityTimeline? activityTimeline,
        bool ownsAudioSessionSource,
        bool ownsActivityTimeline,
        bool showNotifyIcon,
        SingleInstanceActivation? activation = null)
    {
        AppLog.Info($"tray context initializing ownsAudioSessionSource={ownsAudioSessionSource} showNotifyIcon={showNotifyIcon}");
        _audioSessionSource = audioSessionSource;
        _activityTimeline = activityTimeline ?? new AudioActivityTimeline(_audioSessionSource);
        _ownsAudioSessionSource = ownsAudioSessionSource;
        _ownsActivityTimeline = ownsActivityTimeline;
        _activation = activation;
        _startup = new RunKeyStartupRegistration(
            "SoundTracker",
            Environment.ProcessPath ?? Application.ExecutablePath);
        _ui = new UiDispatcher();
        _updateChecker = new UpdateChecker(_updateHttpClient, Application.ProductVersion, RepoInfo.Owner, RepoInfo.Name);
        _recentActivityForm = new RecentActivityForm();
        _leftClickTimer = new System.Windows.Forms.Timer
        {
            Interval = Math.Max(200, SystemInformation.DoubleClickTime),
        };
        _leftClickTimer.Tick += HandleLeftClickTimerTick;

        var menu = new ContextMenuStrip();
        _volumeStatusItem = new ToolStripMenuItem("Checking volume...")
        {
            Enabled = false,
        };
        _activeStatusItem = new ToolStripMenuItem("Checking audio sessions...")
        {
            Enabled = false,
        };
        _recentStatusItem = new ToolStripMenuItem("Checking recent activity...")
        {
            Enabled = false,
        };
        _runAtStartupItem = new ToolStripMenuItem("Run at startup")
        {
            Checked = _startup.IsRegistered,
            CheckOnClick = false,
        };
        _runAtStartupItem.Click += HandleRunAtStartupClick;
        var recentActivityItem = new ToolStripMenuItem("Recent Activity", null, (_, _) =>
        {
            AppLog.Info("tray menu recent activity clicked");
            ShowRecentActivityWindow();
        });
        var volumeMixerItem = new ToolStripMenuItem("Open Volume Mixer", null, (_, _) =>
        {
            AppLog.Info("tray menu open volume mixer clicked");
            OpenVolumeMixer();
        });
        var refreshItem = new ToolStripMenuItem("Refresh", null, (_, _) =>
        {
            AppLog.Info("tray menu refresh clicked");
            RefreshSessions();
        });
        var settingsItem = new ToolStripMenuItem("Settings…", null, (_, _) =>
        {
            AppLog.Info("tray menu settings clicked");
            OpenSettings();
        });
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) =>
        {
            AppLog.Info("tray menu exit clicked");
            ExitThread();
        });

        menu.Items.Add(_volumeStatusItem);
        menu.Items.Add(_activeStatusItem);
        menu.Items.Add(_recentStatusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(volumeMixerItem);
        menu.Items.Add(recentActivityItem);
        menu.Items.Add(_runAtStartupItem);
        menu.Items.Add(refreshItem);
        menu.Items.Add(settingsItem);

        _notifyIcon = TrayIcon.ForApp("SoundTracker");
        _notifyIcon.Icon = SystemIcons.Information;
        _currentTooltipText = $"{AppMetadata.TooltipPrefix}: starting";
        _notifyIcon.TooltipText = _currentTooltipText;
        _notifyIcon.Visible = showNotifyIcon;

        _iconProvider = new AudioIconProvider();
        _iconManager = new TrayIconManager(_notifyIcon, _iconProvider, TrayTheme.Current);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(StandardMenuItems.CreateAbout("SoundTracker", updateChecker: _updateChecker, appIcon: _notifyIcon.Icon));
        menu.Items.Add(StandardMenuItems.CreateCheckForUpdates(_updateChecker, _notifyIcon, "SoundTracker"));
        menu.Items.Add(StandardMenuItems.CreateOpenLogs("SoundTracker"));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.MouseClick += HandleNotifyIconMouseClick;
        _notifyIcon.MouseDoubleClick += HandleNotifyIconMouseDoubleClick;
        _notifyIcon.DoubleClick += (_, _) =>
        {
            AppLog.Info("tray icon double click handler entered");
            _leftClickTimer.Stop();
            ShowRecentActivityWindow();
        };
        AppLog.Info("notify icon created");

        _audioSessionSource.SessionsChanged += HandleSessionsChanged;
        _audioSessionSource.VolumeStateChanged += HandleVolumeStateChanged;
        _activityTimeline.HistoryChanged += HandleHistoryChanged;
        SystemEvents.UserPreferenceChanged += HandleUserPreferenceChanged;
        AppLog.Info("audio session source subscribed");

        if (_activation is not null)
            _activation.ActivationRequested += (_, _) => _ui.Post(ShowRecentActivityWindow);

        RefreshSessions();
        AppLog.Info("tray context initialized");

        _updateChecker.StartPeriodicChecks(TimeSpan.FromHours(24), r =>
            _ui.Post(() =>
            {
                if (!_shuttingDown)
                    _notifyIcon.ShowBalloonTip(5000, "SoundTracker update available",
                        $"Version {r.LatestVersion} is available — visit GitHub to download.", ToolTipIcon.Info);
            }),
            _updateCts.Token);

        var firstRunConfig = SoundTrackerConfig.Load();
        FirstRunBalloon.ShowIfNeeded(_notifyIcon, firstRunConfig.ShownFirstRunWelcome,
            () => { firstRunConfig.ShownFirstRunWelcome = true; firstRunConfig.Save(); },
            "SoundTracker",
            "Right-click the tray icon to open settings, view recent activity, or toggle startup.");
    }

    private string _currentTooltipText = string.Empty;

    internal string CurrentTooltipText => _currentTooltipText;

    internal string CurrentVolumeStatusText => _volumeStatusItem.Text ?? string.Empty;

    internal string CurrentStatusText => _activeStatusItem.Text ?? string.Empty;

    internal string CurrentRecentStatusText => _recentStatusItem.Text ?? string.Empty;

    internal void ShutdownForTests()
    {
        ExitThreadCore();
    }

    protected override void ExitThreadCore()
    {
        AppLog.Info("tray context exiting");
        _activation?.Dispose();
        _audioSessionSource.SessionsChanged -= HandleSessionsChanged;
        _audioSessionSource.VolumeStateChanged -= HandleVolumeStateChanged;
        _activityTimeline.HistoryChanged -= HandleHistoryChanged;
        SystemEvents.UserPreferenceChanged -= HandleUserPreferenceChanged;
        _leftClickTimer.Stop();
        _leftClickTimer.Dispose();
        if (_ownsActivityTimeline)
        {
            _activityTimeline.Dispose();
        }
        if (_ownsAudioSessionSource)
        {
            AppLog.Info("disposing owned audio session source");
            _audioSessionSource.Dispose();
        }

        _recentActivityForm.Close();
        _recentActivityForm.Dispose();
        _shuttingDown = true;
        _updateCts.Cancel();
        _updateCts.Dispose();
        _updateHttpClient.Dispose();
        _ui.Dispose();

        _iconManager.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        base.ExitThreadCore();
        AppLog.Info("tray context exited");
    }

    private void OpenSettings()
    {
        var config = SoundTrackerConfig.Load();
        using var dlg = new SettingsForm(config, _startup);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _runAtStartupItem.Checked = _startup.IsRegistered;
        }
    }

    private void HandleRunAtStartupClick(object? sender, EventArgs e)
    {
        bool enable = !_runAtStartupItem.Checked;
        var result = enable ? _startup.Register() : _startup.Unregister();
        if (result == StartupRegistrationResult.Success)
        {
            _runAtStartupItem.Checked = enable;
            var config = SoundTrackerConfig.Load();
            config.RunAtStartup = enable;
            config.Save();
            AppLog.Info($"startup.registration.changed enabled={enable}");
        }
        else
        {
            AppLog.Info($"startup.registration.failed wanted={enable} result={result}");
        }
    }

    private void HandleSessionsChanged(object? sender, EventArgs e)
    {
        AppLog.Info($"sessions changed callback onUiThread={_ui.IsUiThread} shuttingDown={_shuttingDown}");
        BeginRefreshOnUiThread();
    }

    private void HandleHistoryChanged(object? sender, EventArgs e)
    {
        AppLog.Info($"history changed callback onUiThread={_ui.IsUiThread} shuttingDown={_shuttingDown}");
        BeginRefreshOnUiThread();
    }

    private void HandleVolumeStateChanged(object? sender, EventArgs e)
    {
        AppLog.Info($"volume changed callback onUiThread={_ui.IsUiThread} shuttingDown={_shuttingDown}");
        BeginRefreshOnUiThread();
    }

    private void HandleNotifyIconMouseClick(object? sender, MouseEventArgs args)
    {
        AppLog.Info($"tray icon mouse click button={args.Button} x={args.X} y={args.Y}");
        if (args.Button != MouseButtons.Left)
        {
            return;
        }

        _leftClickTimer.Stop();
        _leftClickTimer.Start();
    }

    private void HandleNotifyIconMouseDoubleClick(object? sender, MouseEventArgs args)
    {
        AppLog.Info($"tray icon mouse double click button={args.Button} x={args.X} y={args.Y}");
        if (args.Button != MouseButtons.Left)
        {
            return;
        }

        _leftClickTimer.Stop();
    }

    private void HandleLeftClickTimerTick(object? sender, EventArgs e)
    {
        _leftClickTimer.Stop();
        AppLog.Info("tray icon single left click resolved to volume mixer");
        OpenVolumeMixer();
    }

    private void HandleUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        AppLog.Info($"user preference changed category={e.Category}");
        BeginRefreshOnUiThread();
    }

    private void BeginRefreshOnUiThread()
    {
        if (_shuttingDown)
            return;

        if (!_ui.IsUiThread)
        {
            AppLog.Info("dispatching refresh to ui thread");
            _ui.Post(RefreshSessions);
            return;
        }

        RefreshSessions();
    }

    private void RefreshSessions()
    {
        var started = Stopwatch.StartNew();
        try
        {
            AppLog.Info("refresh sessions start");
            var volumeSnapshot = _audioSessionSource.GetEndpointVolume();
            var sessions = _audioSessionSource.GetActiveSessionNames();
            var recentActivities = _activityTimeline.GetRecentEvents(100);
            UpdateTrayIcon(volumeSnapshot);
            var tb = new TrayTooltipBuilder()
                .AddRequired(AppMetadata.TooltipPrefix)
                .AddRequired(BuildVolumeSummaryInline(volumeSnapshot));
            if (sessions.Count > 0)
            {
                tb.AddOptional(BuildActiveTooltipSummaryInline(sessions));
            }
            else
            {
                var recent = BuildRecentTooltipSummaryInline(recentActivities, DateTimeOffset.UtcNow);
                if (recent is not null) tb.AddOptional(recent);
            }
            var tipText = tb.Build();
            _currentTooltipText = tipText;
            _notifyIcon.Tooltip = tb;
            _volumeStatusItem.Text = ActivityLabelFormatter.BuildVolumeMenuLabel(volumeSnapshot);
            _activeStatusItem.Text = ActivityLabelFormatter.BuildActiveMenuLabel(sessions);
            _recentStatusItem.Text = ActivityLabelFormatter.BuildRecentMenuLabel(recentActivities);
            _recentActivityForm.RefreshEntries(sessions, recentActivities);
            AppLog.Info($"refresh sessions success volume={volumeSnapshot.Percent} muted={volumeSnapshot.IsMuted} count={sessions.Count} historyCount={recentActivities.Count} tooltip=\"{tipText}\" elapsedMs={started.ElapsedMilliseconds}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            _currentTooltipText = "Sound Tracker: unavailable";
            _notifyIcon.TooltipText = _currentTooltipText;
            _volumeStatusItem.Text = "Volume: unavailable";
            _activeStatusItem.Text = "Audio session query failed";
            _recentStatusItem.Text = "Recent activity unavailable";
            AppLog.Error($"refresh sessions failed elapsedMs={started.ElapsedMilliseconds}", ex);
        }
    }

    private void ShowRecentActivityWindow()
    {
        AppLog.Info("show recent activity window");
        var sessions = _audioSessionSource.GetActiveSessionNames();
        _recentActivityForm.RefreshEntries(sessions, _activityTimeline.GetRecentEvents(100));
        if (!_recentActivityForm.Visible)
        {
            _recentActivityForm.Show();
        }

        if (_recentActivityForm.WindowState == FormWindowState.Minimized)
        {
            _recentActivityForm.WindowState = FormWindowState.Normal;
        }

        _recentActivityForm.BringToFront();
        _recentActivityForm.Activate();
    }

    private static void OpenVolumeMixer()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "sndvol.exe",
                UseShellExecute = true,
            });
            AppLog.Info("volume mixer launch requested");
        }
        catch (Exception ex)
        {
            AppLog.Error("failed to launch volume mixer", ex);
        }
    }

    private static string BuildVolumeSummaryInline(EndpointVolumeSnapshot snapshot)
    {
        if (!snapshot.IsAvailable) return "Volume unavailable";
        return snapshot.IsMuted
            ? $"Muted {snapshot.Percent}%"
            : $"Volume {snapshot.Percent}%";
    }

    private static string BuildActiveTooltipSummaryInline(IReadOnlyList<string> activeSessions)
    {
        if (activeSessions.Count == 1)
        {
            return $"Active: {CompactSourceInline(activeSessions[0])}";
        }
        return $"Active: {activeSessions.Count} apps";
    }

    private static string? BuildRecentTooltipSummaryInline(
        IReadOnlyList<AudioActivityEvent> recentActivities,
        DateTimeOffset nowUtc)
    {
        var latest = recentActivities
            .OrderByDescending(a => a.TimestampUtc)
            .FirstOrDefault();
        if (latest is null) return null;

        var age = BuildCompactAgeInline(latest.TimestampUtc, nowUtc);
        var source = CompactSourceInline(latest.Description);

        return latest.Kind switch
        {
            AudioActivityKind.ObservedActive       => $"Recent: heard {source} {age}",
            AudioActivityKind.Started              => $"Recent: start {source} {age}",
            AudioActivityKind.Stopped              => $"Recent: stop {source} {age}",
            AudioActivityKind.DefaultDeviceChanged => $"Recent: device {age}",
            _                                      => $"Recent: {source} {age}",
        };
    }

    private static string CompactSourceInline(string description)
    {
        var value = description.Trim();
        if (value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            value = value[..^4];
        if (value.Length <= 16) return value;
        return value[..13] + "...";
    }

    private static string BuildCompactAgeInline(DateTimeOffset timestampUtc, DateTimeOffset nowUtc)
    {
        var age = nowUtc - timestampUtc;
        if (age < TimeSpan.Zero) age = TimeSpan.Zero;
        if (age < TimeSpan.FromMinutes(1)) return $"{Math.Max(0, (int)age.TotalSeconds)}s";
        if (age < TimeSpan.FromHours(1))   return $"{(int)age.TotalMinutes}m";
        if (age < TimeSpan.FromDays(1))    return $"{(int)age.TotalHours}h";
        return $"{(int)age.TotalDays}d";
    }

    private void UpdateTrayIcon(EndpointVolumeSnapshot volumeSnapshot)
    {
        _iconProvider.Update(volumeSnapshot);
        _iconManager.RequestRefresh();
    }
}
