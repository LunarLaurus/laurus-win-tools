using System.Windows.Forms;

namespace ProgramHider;

// Editor dialog for creating and modifying structured window rules.
internal sealed class RuleEditorForm : Form
{
    private readonly TextBox _ruleNameTextBox = new() { Width = 280 };
    private readonly TextBox _processNameTextBox = new() { Width = 280 };
    private readonly TextBox _titleContainsTextBox = new() { Width = 280 };
    private readonly TextBox _classNameTextBox = new() { Width = 280 };
    private readonly CheckBox _autoHideOnMinimizeCheckBox = new() { Text = "Auto-hide on minimize", AutoSize = true };
    private readonly CheckBox _requirePinOnRestoreCheckBox = new() { Text = "Require PIN/password on restore", AutoSize = true };
    private readonly CheckBox _suppressNotificationsCheckBox = new() { Text = "Suppress tray notifications for matching windows", AutoSize = true };

    public RuleEditorForm(WindowRule? rule = null)
    {
        EditedRule = rule?.Clone() ?? new WindowRule();

        Text = rule is null ? "Add Window Rule" : "Edit Window Rule";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 320);

        PopulateFromRule(EditedRule);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 8
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddField(root, 0, "Rule name", _ruleNameTextBox);
        AddField(root, 1, "Process name", _processNameTextBox);
        AddField(root, 2, "Title contains", _titleContainsTextBox);
        AddField(root, 3, "Class name", _classNameTextBox);

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
        root.Controls.Add(new Label { Text = "Behavior", AutoSize = true, Margin = new Padding(0, 8, 8, 0) }, 0, 4);
        root.Controls.Add(behaviorPanel, 1, 4);

        var hintLabel = new Label
        {
            AutoSize = true,
            Text = "Populate one or more match fields. All populated fields must match the window."
        };
        root.Controls.Add(hintLabel, 0, 5);
        root.SetColumnSpan(hintLabel, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };
        var saveButton = new Button
        {
            Text = "&Save Rule",
            AutoSize = true
        };
        saveButton.Click += (_, _) => SaveAndClose();
        var cancelButton = new Button
        {
            Text = "&Cancel",
            DialogResult = DialogResult.Cancel,
            AutoSize = true
        };
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(cancelButton);
        AcceptButton = saveButton;
        CancelButton = cancelButton;

        root.Controls.Add(buttons, 0, 7);
        root.SetColumnSpan(buttons, 2);

        Controls.Add(root);
    }

    public WindowRule EditedRule { get; private set; }

    private static void AddField(TableLayoutPanel root, int row, string labelText, Control control)
    {
        root.Controls.Add(new Label { Text = labelText, AutoSize = true, Margin = new Padding(0, 8, 8, 0) }, 0, row);
        root.Controls.Add(control, 1, row);
    }

    private void PopulateFromRule(WindowRule rule)
    {
        _ruleNameTextBox.Text = rule.RuleName;
        _processNameTextBox.Text = rule.MatchProcessName;
        _titleContainsTextBox.Text = rule.MatchTitleContains;
        _classNameTextBox.Text = rule.MatchClassName;
        _autoHideOnMinimizeCheckBox.Checked = rule.AutoHideOnMinimize;
        _requirePinOnRestoreCheckBox.Checked = rule.RequirePinOnRestore;
        _suppressNotificationsCheckBox.Checked = rule.SuppressNotifications;
    }

    private void SaveAndClose()
    {
        var rule = new WindowRule
        {
            RuleName = _ruleNameTextBox.Text,
            MatchProcessName = _processNameTextBox.Text,
            MatchTitleContains = _titleContainsTextBox.Text,
            MatchClassName = _classNameTextBox.Text,
            AutoHideOnMinimize = _autoHideOnMinimizeCheckBox.Checked,
            RequirePinOnRestore = _requirePinOnRestoreCheckBox.Checked,
            SuppressNotifications = _suppressNotificationsCheckBox.Checked
        };
        rule.Normalize();

        if (!rule.HasAnyMatchField)
        {
            MessageBox.Show(
                "Add at least one match field before saving the rule.",
                "Program Hider",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        EditedRule = rule;
        DialogResult = DialogResult.OK;
        Close();
    }
}
