using System.Drawing;
using SoundTracker.App.Audio;

namespace SoundTracker.App;

internal sealed class RecentActivityForm : Form
{
    private readonly ListView _activeListView;
    private readonly ListView _activityListView;

    public RecentActivityForm()
    {
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        ClientSize = new Size(980, 520);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        MinimumSize = new Size(760, 420);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "SoundTracker Recent Activity";

        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 150,
        };

        var activePanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 12, 12, 6),
        };
        var recentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 6, 12, 12),
        };

        var activeLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = new Font(Font, FontStyle.Bold),
            Text = "Active Now",
        };
        var recentLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = new Font(Font, FontStyle.Bold),
            Text = "Recent Activity",
        };

        _activeListView = new ListView
        {
            Dock = DockStyle.Fill,
            FullRowSelect = true,
            GridLines = true,
            HideSelection = false,
            MultiSelect = false,
            UseCompatibleStateImageBehavior = false,
            View = View.Details,
        };
        _activeListView.Columns.Add("Source", 320);
        _activeListView.Columns.Add("State", 180);

        _activityListView = new ListView
        {
            Dock = DockStyle.Fill,
            FullRowSelect = true,
            GridLines = true,
            HideSelection = false,
            MultiSelect = false,
            UseCompatibleStateImageBehavior = false,
            View = View.Details,
        };
        _activityListView.Columns.Add("Time", 170);
        _activityListView.Columns.Add("Age", 90);
        _activityListView.Columns.Add("Event", 130);
        _activityListView.Columns.Add("Source", 240);
        _activityListView.Columns.Add("Duration", 90);
        _activityListView.Columns.Add("Device", 220);

        activePanel.Controls.Add(_activeListView);
        activePanel.Controls.Add(activeLabel);
        recentPanel.Controls.Add(_activityListView);
        recentPanel.Controls.Add(recentLabel);
        splitContainer.Panel1.Controls.Add(activePanel);
        splitContainer.Panel2.Controls.Add(recentPanel);
        Controls.Add(splitContainer);
    }

    internal IReadOnlyList<string> SnapshotRows()
    {
        var activeRows = _activeListView.Items
            .Cast<ListViewItem>()
            .Select(item => "ACTIVE | " + string.Join(" | ", item.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(subItem => subItem.Text)));
        var recentRows = _activityListView.Items
            .Cast<ListViewItem>()
            .Select(item => "RECENT | " + string.Join(" | ", item.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(subItem => subItem.Text)));

        return activeRows.Concat(recentRows).ToList();
    }

    internal void RefreshEntries(
        IReadOnlyList<string> activeSessions,
        IReadOnlyList<AudioActivityEvent> activities)
    {
        _activeListView.BeginUpdate();
        _activityListView.BeginUpdate();
        try
        {
            _activeListView.Items.Clear();
            _activityListView.Items.Clear();

            if (activeSessions.Count == 0)
            {
                _activeListView.Items.Add(new ListViewItem(new[]
                {
                    "No active sessions",
                    "Idle",
                }));
            }
            else
            {
                foreach (var activeSession in activeSessions)
                {
                    _activeListView.Items.Add(new ListViewItem(new[]
                    {
                        activeSession,
                        "Playing now",
                    }));
                }
            }

            if (activities.Count == 0)
            {
                _activityListView.Items.Add(new ListViewItem(new[]
                {
                    "No events yet",
                    string.Empty,
                    string.Empty,
                    "Recent audio activity will appear here.",
                    string.Empty,
                    string.Empty,
                }));
                return;
            }

            var nowUtc = DateTimeOffset.UtcNow;
            foreach (var activity in activities.OrderByDescending(activity => activity.TimestampUtc))
            {
                var item = new ListViewItem(activity.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                item.SubItems.Add(TooltipFormatter.BuildRelativeAge(activity.TimestampUtc, nowUtc));
                item.SubItems.Add(TooltipFormatter.BuildHistoryRow(activity));
                item.SubItems.Add(activity.Description);
                item.SubItems.Add(TooltipFormatter.BuildDuration(activity.Duration));
                item.SubItems.Add(activity.DeviceId ?? string.Empty);
                _activityListView.Items.Add(item);
            }
        }
        finally
        {
            _activeListView.EndUpdate();
            _activityListView.EndUpdate();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }
}
