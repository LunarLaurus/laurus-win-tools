using System.Drawing;
using SoundTracker.App.Audio;
using WindowsTrayCore;

namespace SoundTracker.App;

internal sealed class RecentActivityForm : Form
{
    private readonly ListView _activeListView;
    private readonly ListView _activityListView;
    private readonly GroupBox _activeGroup;
    private readonly GroupBox _recentGroup;

    public RecentActivityForm()
    {
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = TrayTheme.Current.Background;
        TrayTheme.Current.Changed += OnThemeChanged;
        ClientSize = new Size(1100, 680);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        MinimumSize = new Size(900, 560);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "SoundTracker Recent Activity";

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 1,
            RowCount = 2,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 190F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _activeGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Text = "Active Now",
        };
        _recentGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Text = "Recent Activity",
        };

        _activeListView = new ListView
        {
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            HideSelection = false,
            MultiSelect = false,
            UseCompatibleStateImageBehavior = false,
            View = View.Details,
        };
        _activeListView.Columns.Add("Source", 420);
        _activeListView.Columns.Add("State", 160);

        _activityListView = new ListView
        {
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            HideSelection = false,
            MultiSelect = false,
            UseCompatibleStateImageBehavior = false,
            View = View.Details,
        };
        _activityListView.Columns.Add("Time", 165);
        _activityListView.Columns.Add("Age", 85);
        _activityListView.Columns.Add("Event", 120);
        _activityListView.Columns.Add("Source", 250);
        _activityListView.Columns.Add("Duration", 90);
        _activityListView.Columns.Add("Device", 280);

        _activeGroup.Controls.Add(_activeListView);
        _recentGroup.Controls.Add(_activityListView);
        layout.Controls.Add(_activeGroup, 0, 0);
        layout.Controls.Add(_recentGroup, 0, 1);
        Controls.Add(layout);
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

            _activeGroup.Text = $"Active Now ({activeSessions.Count})";

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
                _recentGroup.Text = "Recent Activity (0)";
                ResizeColumns();
                return;
            }

            var nowUtc = DateTimeOffset.UtcNow;
            foreach (var activity in activities.OrderByDescending(activity => activity.TimestampUtc))
            {
                var item = new ListViewItem(activity.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                item.SubItems.Add(ActivityLabelFormatter.BuildRelativeAge(activity.TimestampUtc, nowUtc));
                item.SubItems.Add(ActivityLabelFormatter.BuildHistoryRow(activity));
                item.SubItems.Add(activity.Description);
                item.SubItems.Add(ActivityLabelFormatter.BuildDuration(activity.Duration));
                item.SubItems.Add(activity.DeviceId ?? string.Empty);
                _activityListView.Items.Add(item);
            }

            _recentGroup.Text = $"Recent Activity ({activities.Count})";
            ResizeColumns();
        }
        finally
        {
            _activeListView.EndUpdate();
            _activityListView.EndUpdate();
        }
    }

    private void ResizeColumns()
    {
        ResizeColumn(_activeListView, 0, 380);
        ResizeColumn(_activeListView, 1, 140);
        ResizeColumn(_activityListView, 0, 150);
        ResizeColumn(_activityListView, 1, 80);
        ResizeColumn(_activityListView, 2, 110);
        ResizeColumn(_activityListView, 3, 220);
        ResizeColumn(_activityListView, 4, 90);
        ResizeColumn(_activityListView, 5, 240);
    }

    private static void ResizeColumn(ListView listView, int columnIndex, int minimumWidth)
    {
        if (listView.Columns.Count <= columnIndex)
        {
            return;
        }

        listView.AutoResizeColumn(columnIndex, ColumnHeaderAutoResizeStyle.HeaderSize);
        var headerWidth = listView.Columns[columnIndex].Width;
        listView.AutoResizeColumn(columnIndex, ColumnHeaderAutoResizeStyle.ColumnContent);
        listView.Columns[columnIndex].Width = Math.Max(minimumWidth, Math.Max(headerWidth, listView.Columns[columnIndex].Width));
    }

    private void OnThemeChanged(object? sender, EventArgs e) =>
        BackColor = TrayTheme.Current.Background;

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        TrayTheme.Current.Changed -= OnThemeChanged;
        base.OnFormClosed(e);
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
