using System.Windows.Forms;

namespace ProgramHider;

// Searchable restore UI for selecting one or more hidden windows.
internal sealed class RestoreBrowserForm : Form
{
    private readonly List<HiddenWindow> _hiddenWindows;
    private readonly TextBox _searchTextBox = new() { Width = 280 };
    private readonly ListView _recentListView = CreateListView();
    private readonly ListView _allWindowsListView = CreateListView();

    public RestoreBrowserForm(IEnumerable<HiddenWindow> hiddenWindows, bool restoreWithoutFocus)
    {
        _hiddenWindows = hiddenWindows
            .OrderByDescending(window => window.HiddenAtUtc)
            .ThenBy(window => window.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(window => window.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Text = "Restore Hidden Windows";
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MinimumSize = new Size(760, 520);
        ClientSize = new Size(860, 620);

        _searchTextBox.TextChanged += (_, _) => RefreshAllWindowsList();
        _recentListView.DoubleClick += (_, _) => RestoreFromListView(_recentListView);
        _allWindowsListView.DoubleClick += (_, _) => RestoreFromListView(_allWindowsListView);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 5
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildHeader(restoreWithoutFocus), 0, 0);
        root.Controls.Add(BuildRecentGroup(), 0, 1);
        root.Controls.Add(BuildSearchRow(), 0, 2);
        root.Controls.Add(BuildAllWindowsGroup(), 0, 3);
        root.Controls.Add(BuildButtons(), 0, 4);

        Controls.Add(root);

        RefreshRecentList();
        RefreshAllWindowsList();
    }

    public bool RestoreAllRequested { get; private set; }
    public IReadOnlyList<nint> SelectedHandles { get; private set; } = Array.Empty<nint>();

    private Control BuildHeader(bool restoreWithoutFocus)
    {
        var header = new Label
        {
            AutoSize = true,
            Text = restoreWithoutFocus
                ? "Restore actions are currently configured to avoid stealing focus."
                : "Restore actions will bring windows back and focus them."
        };
        return header;
    }

    private Control BuildRecentGroup()
    {
        var group = new GroupBox
        {
            Text = "Recently hidden",
            Dock = DockStyle.Fill
        };
        group.Controls.Add(_recentListView);
        return group;
    }

    private Control BuildSearchRow()
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Margin = new Padding(0, 12, 0, 8)
        };
        row.Controls.Add(new Label { Text = "Search hidden windows", AutoSize = true, Margin = new Padding(0, 8, 8, 0) });
        row.Controls.Add(_searchTextBox);
        return row;
    }

    private Control BuildAllWindowsGroup()
    {
        var group = new GroupBox
        {
            Text = "All hidden windows",
            Dock = DockStyle.Fill
        };
        group.Controls.Add(_allWindowsListView);
        return group;
    }

    private Control BuildButtons()
    {
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft
        };

        var restoreAllButton = new Button
        {
            Text = "Restore All",
            AutoSize = true
        };
        restoreAllButton.Click += (_, _) =>
        {
            RestoreAllRequested = true;
            SelectedHandles = Array.Empty<nint>();
            DialogResult = DialogResult.OK;
            Close();
        };

        var restoreSelectedButton = new Button
        {
            Text = "Restore Selected",
            AutoSize = true
        };
        restoreSelectedButton.Click += (_, _) => RestoreFromListView(_allWindowsListView);

        var closeButton = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.Cancel,
            AutoSize = true
        };

        buttons.Controls.Add(restoreAllButton);
        buttons.Controls.Add(restoreSelectedButton);
        buttons.Controls.Add(closeButton);
        AcceptButton = restoreSelectedButton;
        CancelButton = closeButton;
        return buttons;
    }

    private void RefreshRecentList()
    {
        PopulateListView(_recentListView, _hiddenWindows.Take(5));
    }

    private void RefreshAllWindowsList()
    {
        var filter = _searchTextBox.Text.Trim();
        var filtered = _hiddenWindows.Where(
            window => string.IsNullOrWhiteSpace(filter) ||
                      window.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                      window.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                      window.ClassName.Contains(filter, StringComparison.OrdinalIgnoreCase));
        PopulateListView(_allWindowsListView, filtered);
    }

    private void PopulateListView(ListView listView, IEnumerable<HiddenWindow> windows)
    {
        listView.BeginUpdate();
        try
        {
            listView.Items.Clear();
            foreach (var window in windows)
            {
                var item = new ListViewItem(window.Title);
                item.SubItems.Add(window.ProcessName);
                item.SubItems.Add(window.HiddenAtUtc.ToLocalTime().ToString("HH:mm:ss"));
                item.SubItems.Add(window.RequirePinOnRestore ? "PIN" : string.Empty);
                item.Tag = window;
                listView.Items.Add(item);
            }
        }
        finally
        {
            listView.EndUpdate();
        }
    }

    private void RestoreFromListView(ListView listView)
    {
        if (listView.SelectedItems.Count == 0)
        {
            return;
        }

        SelectedHandles = listView.SelectedItems
            .Cast<ListViewItem>()
            .Select(item => ((HiddenWindow)item.Tag!).Handle)
            .Distinct()
            .ToArray();
        RestoreAllRequested = false;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static ListView CreateListView()
    {
        var listView = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = true,
            HideSelection = false,
            Dock = DockStyle.Fill
        };
        listView.Columns.Add("Title", 360);
        listView.Columns.Add("Process", 160);
        listView.Columns.Add("Hidden", 110);
        listView.Columns.Add("Flags", 80);
        return listView;
    }
}
