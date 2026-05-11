using System.Diagnostics;
using System.Drawing;
using SoundTracker.App.Audio;

namespace SoundTracker.App;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AudioSessionMonitor _audioSessionMonitor;
    private readonly ToolStripMenuItem _statusItem;
    private readonly NotifyIcon _notifyIcon;
    private readonly Control _uiDispatcher;

    public TrayApplicationContext()
    {
        _uiDispatcher = new Control();
        _ = _uiDispatcher.Handle;

        var menu = new ContextMenuStrip();
        _statusItem = new ToolStripMenuItem("Checking audio sessions...")
        {
            Enabled = false,
        };
        var refreshItem = new ToolStripMenuItem("Refresh", null, (_, _) => RefreshSessions());
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => ExitThread());

        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(refreshItem);
        menu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = SystemIcons.Information,
            Text = "Sound Tracker: starting",
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => RefreshSessions();

        _audioSessionMonitor = new AudioSessionMonitor();
        _audioSessionMonitor.SessionsChanged += HandleSessionsChanged;

        RefreshSessions();
    }

    protected override void ExitThreadCore()
    {
        _audioSessionMonitor.SessionsChanged -= HandleSessionsChanged;
        _audioSessionMonitor.Dispose();

        _uiDispatcher.Dispose();

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        base.ExitThreadCore();
    }

    private void HandleSessionsChanged(object? sender, EventArgs e)
    {
        if (_uiDispatcher.IsDisposed)
        {
            return;
        }

        if (_uiDispatcher.InvokeRequired)
        {
            _uiDispatcher.BeginInvoke(RefreshSessions);
            return;
        }

        RefreshSessions();
    }

    private void RefreshSessions()
    {
        try
        {
            var sessions = _audioSessionMonitor.GetActiveSessionNames();
            _notifyIcon.Text = TooltipFormatter.Build(sessions);
            _statusItem.Text = TooltipFormatter.BuildMenuLabel(sessions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            _notifyIcon.Text = "Sound Tracker: unavailable";
            _statusItem.Text = "Audio session query failed";
        }
    }
}
