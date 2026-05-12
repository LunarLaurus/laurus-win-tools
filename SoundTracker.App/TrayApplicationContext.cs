using System.Diagnostics;
using System.Drawing;
using SoundTracker.App.Audio;
using SoundTracker.App.Diagnostics;

namespace SoundTracker.App;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly IAudioSessionSource _audioSessionSource;
    private readonly bool _ownsAudioSessionSource;
    private readonly ToolStripMenuItem _statusItem;
    private readonly NotifyIcon _notifyIcon;
    private readonly Control _uiDispatcher;

    public TrayApplicationContext()
        : this(new AudioSessionMonitor(), ownsAudioSessionSource: true, showNotifyIcon: true)
    {
    }

    internal TrayApplicationContext(
        IAudioSessionSource audioSessionSource,
        bool ownsAudioSessionSource,
        bool showNotifyIcon)
    {
        AppLog.Info($"tray context initializing ownsAudioSessionSource={ownsAudioSessionSource} showNotifyIcon={showNotifyIcon}");
        _audioSessionSource = audioSessionSource;
        _ownsAudioSessionSource = ownsAudioSessionSource;
        _uiDispatcher = new Control();
        _ = _uiDispatcher.Handle;
        AppLog.Info($"ui dispatcher handle created=0x{_uiDispatcher.Handle.ToInt64():X}");

        var menu = new ContextMenuStrip();
        _statusItem = new ToolStripMenuItem("Checking audio sessions...")
        {
            Enabled = false,
        };
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

        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(refreshItem);
        menu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = SystemIcons.Information,
            Text = "Sound Tracker: starting",
            Visible = showNotifyIcon,
        };
        _notifyIcon.MouseClick += (_, args) =>
            AppLog.Info($"tray icon mouse click button={args.Button} x={args.X} y={args.Y}");
        _notifyIcon.MouseDoubleClick += (_, args) =>
            AppLog.Info($"tray icon mouse double click button={args.Button} x={args.X} y={args.Y}");
        _notifyIcon.DoubleClick += (_, _) =>
        {
            AppLog.Info("tray icon double click handler entered");
            RefreshSessions();
        };
        AppLog.Info("notify icon created");

        _audioSessionSource.SessionsChanged += HandleSessionsChanged;
        AppLog.Info("audio session source subscribed");

        RefreshSessions();
        AppLog.Info("tray context initialized");
    }

    internal string CurrentTooltipText => _notifyIcon.Text;

    internal string CurrentStatusText => _statusItem.Text ?? string.Empty;

    internal void ShutdownForTests()
    {
        ExitThreadCore();
    }

    protected override void ExitThreadCore()
    {
        AppLog.Info("tray context exiting");
        _audioSessionSource.SessionsChanged -= HandleSessionsChanged;
        if (_ownsAudioSessionSource)
        {
            AppLog.Info("disposing owned audio session source");
            _audioSessionSource.Dispose();
        }

        _uiDispatcher.Dispose();

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        base.ExitThreadCore();
        AppLog.Info("tray context exited");
    }

    private void HandleSessionsChanged(object? sender, EventArgs e)
    {
        AppLog.Info($"sessions changed callback invokeRequired={_uiDispatcher.InvokeRequired} disposed={_uiDispatcher.IsDisposed}");
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
            _notifyIcon.Text = TooltipFormatter.Build(sessions);
            _statusItem.Text = TooltipFormatter.BuildMenuLabel(sessions);
            AppLog.Info($"refresh sessions success count={sessions.Count} tooltip=\"{_notifyIcon.Text}\" elapsedMs={started.ElapsedMilliseconds}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            _notifyIcon.Text = "Sound Tracker: unavailable";
            _statusItem.Text = "Audio session query failed";
            AppLog.Error($"refresh sessions failed elapsedMs={started.ElapsedMilliseconds}", ex);
        }
    }
}
