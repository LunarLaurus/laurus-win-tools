using System.Drawing;
using SoundTracker.App.Audio;

namespace SoundTracker.App;

internal sealed class RecentActivityForm : Form
{
    private readonly ListView _activityListView;

    public RecentActivityForm()
    {
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        ClientSize = new Size(980, 420);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        MinimumSize = new Size(760, 320);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "SoundTracker Recent Activity";

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

        Controls.Add(_activityListView);
    }

    internal IReadOnlyList<string> SnapshotRows()
    {
        return _activityListView.Items
            .Cast<ListViewItem>()
            .Select(item => string.Join(" | ", item.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(subItem => subItem.Text)))
            .ToList();
    }

    internal void RefreshEntries(IReadOnlyList<AudioActivityEvent> activities)
    {
        _activityListView.BeginUpdate();
        try
        {
            _activityListView.Items.Clear();

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
