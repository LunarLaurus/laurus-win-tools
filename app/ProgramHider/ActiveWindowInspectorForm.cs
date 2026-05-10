using System.Windows.Forms;

namespace ProgramHider;

// Inspection dialog that surfaces the active window's title, process, and
// class, then lets the user turn that snapshot into a rule.
internal sealed class ActiveWindowInspectorForm : Form
{
    private readonly TextBox _ruleNameTextBox = new() { Width = 260 };
    private readonly CheckBox _matchProcessCheckBox = new() { Text = "Match process name", AutoSize = true, Checked = true };
    private readonly CheckBox _matchTitleCheckBox = new() { Text = "Match title substring", AutoSize = true };
    private readonly CheckBox _matchClassCheckBox = new() { Text = "Match window class", AutoSize = true };
    private readonly CheckBox _autoHideOnMinimizeCheckBox = new() { Text = "Auto-hide on minimize", AutoSize = true, Checked = true };
    private readonly CheckBox _requirePinOnRestoreCheckBox = new() { Text = "Require PIN/password on restore", AutoSize = true };
    private readonly CheckBox _suppressNotificationsCheckBox = new() { Text = "Suppress tray notifications", AutoSize = true };

    public ActiveWindowInspectorForm(NativeWindowSnapshot snapshot)
    {
        Snapshot = snapshot;
        Text = "Inspect Active Window";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(640, 420);

        _ruleNameTextBox.Text = $"{snapshot.ProcessName} rule";

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildDetailsGroup(snapshot), 0, 0);
        root.Controls.Add(BuildRuleGroup(), 0, 1);
        root.Controls.Add(BuildButtons(), 0, 2);

        Controls.Add(root);
    }

    public NativeWindowSnapshot Snapshot { get; }
    public WindowRule? CreatedRule { get; private set; }

    private Control BuildDetailsGroup(NativeWindowSnapshot snapshot)
    {
        var group = new GroupBox
        {
            Text = "Active window",
            Dock = DockStyle.Top,
            AutoSize = true
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            ColumnCount = 2,
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddDetail(layout, 0, "Title", snapshot.Title);
        AddDetail(layout, 1, "Process", snapshot.ProcessName);
        AddDetail(layout, 2, "Class", snapshot.ClassName);
        AddDetail(layout, 3, "Handle", $"0x{snapshot.Handle.ToInt64():X}");

        group.Controls.Add(layout);
        return group;
    }

    private Control BuildRuleGroup()
    {
        var group = new GroupBox
        {
            Text = "Create rule from this window",
            Dock = DockStyle.Fill
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            ColumnCount = 2,
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "Rule name", AutoSize = true, Margin = new Padding(0, 4, 8, 0) }, 0, 0);
        layout.Controls.Add(_ruleNameTextBox, 1, 0);

        var matchPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false
        };
        matchPanel.Controls.Add(_matchProcessCheckBox);
        matchPanel.Controls.Add(_matchTitleCheckBox);
        matchPanel.Controls.Add(_matchClassCheckBox);
        layout.Controls.Add(new Label { Text = "Match fields", AutoSize = true, Margin = new Padding(0, 12, 8, 0) }, 0, 1);
        layout.Controls.Add(matchPanel, 1, 1);

        var behaviorPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false
        };
        behaviorPanel.Controls.Add(_autoHideOnMinimizeCheckBox);
        behaviorPanel.Controls.Add(_requirePinOnRestoreCheckBox);
        behaviorPanel.Controls.Add(_suppressNotificationsCheckBox);
        layout.Controls.Add(new Label { Text = "Behavior", AutoSize = true, Margin = new Padding(0, 12, 8, 0) }, 0, 2);
        layout.Controls.Add(behaviorPanel, 1, 2);

        group.Controls.Add(layout);
        return group;
    }

    private Control BuildButtons()
    {
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };
        var createButton = new Button
        {
            Text = "Create Rule",
            AutoSize = true
        };
        createButton.Click += (_, _) => CreateRuleAndClose();
        var closeButton = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.Cancel,
            AutoSize = true
        };

        buttons.Controls.Add(createButton);
        buttons.Controls.Add(closeButton);
        AcceptButton = createButton;
        CancelButton = closeButton;
        return buttons;
    }

    private static void AddDetail(TableLayoutPanel layout, int row, string label, string value)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 4, 8, 0) }, 0, row);
        layout.Controls.Add(new TextBox { Text = value, ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, Width = 420 }, 1, row);
    }

    private void CreateRuleAndClose()
    {
        if (!_matchProcessCheckBox.Checked &&
            !_matchTitleCheckBox.Checked &&
            !_matchClassCheckBox.Checked)
        {
            MessageBox.Show(
                "Choose at least one field to match when creating a rule.",
                "Program Hider",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var rule = new WindowRule
        {
            RuleName = _ruleNameTextBox.Text,
            MatchProcessName = _matchProcessCheckBox.Checked ? Snapshot.ProcessName : string.Empty,
            MatchTitleContains = _matchTitleCheckBox.Checked ? Snapshot.Title : string.Empty,
            MatchClassName = _matchClassCheckBox.Checked ? Snapshot.ClassName : string.Empty,
            AutoHideOnMinimize = _autoHideOnMinimizeCheckBox.Checked,
            RequirePinOnRestore = _requirePinOnRestoreCheckBox.Checked,
            SuppressNotifications = _suppressNotificationsCheckBox.Checked
        };
        rule.Normalize();
        CreatedRule = rule;
        DialogResult = DialogResult.OK;
        Close();
    }
}
