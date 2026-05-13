using System.Diagnostics;
using System.Drawing;
using SoundTracker.App.Audio;
using SoundTracker.App.Diagnostics;
using SoundTracker.App.History;

namespace SoundTracker.App;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly IAudioSessionSource _audioSessionSource;
    private readonly AudioActivityTimeline _activityTimeline;
    private readonly bool _ownsAudioSessionSource;
    private readonly bool _ownsActivityTimeline;
    private readonly RecentActivityForm _recentActivityForm;
    private readonly ToolStripMenuItem _activeStatusItem;
    private readonly ToolStripMenuItem _recentStatusItem;
    private readonly NotifyIcon _notifyIcon;
    private readonly Control _uiDispatcher;

    public TrayApplicationContext()
        : this(
            new AudioSessionMonitor(),
            activityTimeline: null,
            ownsAudioSessionSource: true,
            ownsActivityTimeline: true,
            showNotifyIcon: true)
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
            showNotifyIcon)
    {
    }

    internal TrayApplicationContext(
        IAudioSessionSource audioSessionSource,
        AudioActivityTimeline? activityTimeline,
        bool ownsAudioSessionSource,
        bool ownsActivityTimeline,
        bool showNotifyIcon)
    {
        AppLog.Info($"tray context initializing ownsAudioSessionSource={ownsAudioSessionSource} showNotifyIcon={showNotifyIcon}");
        _audioSessionSource = audioSessionSource;
        _activityTimeline = activityTimeline ?? new AudioActivityTimeline(_audioSessionSource);
        _ownsAudioSessionSource = ownsAudioSessionSource;
        _ownsActivityTimeline = ownsActivityTimeline;
        _uiDispatcher = new Control();
        _ = _uiDispatcher.Handle;
        _recentActivityForm = new RecentActivityForm();
        AppLog.Info($"ui dispatcher handle created=0x{_uiDispatcher.Handle.ToInt64():X}");

        var menu = new ContextMenuStrip();
        _activeStatusItem = new ToolStripMenuItem("Checking audio sessions...")
        {
            Enabled = false,
        };
        _recentStatusItem = new ToolStripMenuItem("Checking recent activity...")
        {
            Enabled = false,
        };
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
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) =>
        {
            AppLog.Info("tray menu exit clicked");
            ExitThread();
        });

        menu.Items.Add(_activeStatusItem);
        menu.Items.Add(_recentStatusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(volumeMixerItem);
        menu.Items.Add(recentActivityItem);
        menu.Items.Add(refreshItem);
        menu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = SystemIcons.Information,
            Text = $"{AppMetadata.TooltipPrefix}: starting",
            Visible = showNotifyIcon,
        };
        _notifyIcon.MouseClick += (_, args) =>
            AppLog.Info($"tray icon mouse click button={args.Button} x={args.X} y={args.Y}");
        _notifyIcon.MouseDoubleClick += (_, args) =>
            AppLog.Info($"tray icon mouse double click button={args.Button} x={args.X} y={args.Y}");
        _notifyIcon.DoubleClick += (_, _) =>
        {
            AppLog.Info("tray icon double click handler entered");
            ShowRecentActivityWindow();
        };
        AppLog.Info("notify icon created");

        _audioSessionSource.SessionsChanged += HandleSessionsChanged;
        _activityTimeline.HistoryChanged += HandleHistoryChanged;
        AppLog.Info("audio session source subscribed");

        RefreshSessions();
        AppLog.Info("tray context initialized");
    }

    internal string CurrentTooltipText => _notifyIcon.Text;

    internal string CurrentStatusText => _activeStatusItem.Text ?? string.Empty;

    internal string CurrentRecentStatusText => _recentStatusItem.Text ?? string.Empty;

    internal void ShutdownForTests()
    {
        ExitThreadCore();
    }

    protected override void ExitThreadCore()
    {
        AppLog.Info("tray context exiting");
        _audioSessionSource.SessionsChanged -= HandleSessionsChanged;
        _activityTimeline.HistoryChanged -= HandleHistoryChanged;
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
        _uiDispatcher.Dispose();

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        base.ExitThreadCore();
        AppLog.Info("tray context exited");
    }

    private void HandleSessionsChanged(object? sender, EventArgs e)
    {
        AppLog.Info($"sessions changed callback invokeRequired={_uiDispatcher.InvokeRequired} disposed={_uiDispatcher.IsDisposed}");
        BeginRefreshOnUiThread();
    }

    private void HandleHistoryChanged(object? sender, EventArgs e)
    {
        AppLog.Info($"history changed callback invokeRequired={_uiDispatcher.InvokeRequired} disposed={_uiDispatcher.IsDisposed}");
        BeginRefreshOnUiThread();
    }

    private void BeginRefreshOnUiThread()
    {
        if (_uiDispatcher.IsDisposed)
        {
            return;
        }

        if (_uiDispatcher.InvokeRequired)
        {
            AppLog.Info("dispatching refresh to ui thread");
            _uiDispatcher.BeginInvoke(RefreshSessions);
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
            var sessions = _audioSessionSource.GetActiveSessionNames();
            var recentActivities = _activityTimeline.GetRecentEvents(100);
            _notifyIcon.Text = TooltipFormatter.Build(sessions, recentActivities);
            _activeStatusItem.Text = TooltipFormatter.BuildActiveMenuLabel(sessions);
            _recentStatusItem.Text = TooltipFormatter.BuildRecentMenuLabel(recentActivities);
            _recentActivityForm.RefreshEntries(sessions, recentActivities);
            AppLog.Info($"refresh sessions success count={sessions.Count} historyCount={recentActivities.Count} tooltip=\"{_notifyIcon.Text}\" elapsedMs={started.ElapsedMilliseconds}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            _notifyIcon.Text = "Sound Tracker: unavailable";
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
}
