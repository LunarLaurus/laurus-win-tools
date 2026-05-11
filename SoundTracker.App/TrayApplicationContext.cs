using System.Diagnostics;
using System.Drawing;
using SoundTracker.App.Audio;

namespace SoundTracker.App;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AudioSessionPoller _audioSessionPoller = new();
    private readonly ToolStripMenuItem _statusItem;
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _refreshTimer;

    public TrayApplicationContext()
    {
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

        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000,
        };
        _refreshTimer.Tick += (_, _) => RefreshSessions();

        RefreshSessions();
        _refreshTimer.Start();
    }

    protected override void ExitThreadCore()
    {
        _refreshTimer.Stop();
        _refreshTimer.Dispose();

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        base.ExitThreadCore();
    }

    private void RefreshSessions()
    {
        try
        {
            var sessions = _audioSessionPoller.GetActiveSessionNames();
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
