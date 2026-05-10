using System.Windows.Forms;

namespace ProgramHider;

internal sealed class SettingsForm : Form
{
    private readonly CheckBox _controlCheckBox = new() { Text = "Ctrl", AutoSize = true };
    private readonly CheckBox _shiftCheckBox = new() { Text = "Shift", AutoSize = true };
    private readonly CheckBox _altCheckBox = new() { Text = "Alt", AutoSize = true };
    private readonly CheckBox _windowsCheckBox = new() { Text = "Win", AutoSize = true };
    private readonly ComboBox _keyComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _candidateProcessComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ListBox _rulesListBox = new();

    public SettingsForm(AppSettings currentSettings, IReadOnlyCollection<string> candidateProcessNames, string settingsPath)
    {
        UpdatedSettings = currentSettings.Clone();
        Text = "Program Hider Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, 420);

        PopulateHotkeyControls();
        PopulateProcessCandidates(candidateProcessNames);
        PopulateFromSettings(currentSettings);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 5
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var hotkeyGroup = new GroupBox
        {
            Text = "Global hotkey",
            Dock = DockStyle.Top,
            AutoSize = true
        };
        var hotkeyLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(10)
        };
        hotkeyLayout.Controls.AddRange(
            new Control[]
            {
                _controlCheckBox,
                _shiftCheckBox,
                _altCheckBox,
                _windowsCheckBox,
                new Label { Text = "Key", AutoSize = true, Margin = new Padding(16, 6, 4, 0) },
                _keyComboBox
            });
        hotkeyGroup.Controls.Add(hotkeyLayout);

        var rulesGroup = new GroupBox
        {
            Text = "Auto-hide on minimize rules",
            Dock = DockStyle.Fill
        };
        var rulesLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            ColumnCount = 2,
            RowCount = 3
        };
        rulesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rulesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rulesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rulesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rulesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var processHint = new Label
        {
            Text = "Choose an open app process to auto-hide whenever it is minimized.",
            AutoSize = true,
            Dock = DockStyle.Fill
        };

        var addRuleButton = new Button
        {
            Text = "Add",
            AutoSize = true
        };
        addRuleButton.Click += (_, _) => AddSelectedRule();

        var removeRuleButton = new Button
        {
            Text = "Remove selected",
            AutoSize = true
        };
        removeRuleButton.Click += (_, _) => RemoveSelectedRule();

        rulesLayout.Controls.Add(processHint, 0, 0);
        rulesLayout.Controls.Add(_candidateProcessComboBox, 0, 1);
        rulesLayout.Controls.Add(addRuleButton, 1, 1);
        rulesLayout.Controls.Add(_rulesListBox, 0, 2);
        rulesLayout.SetColumnSpan(_rulesListBox, 2);
        rulesGroup.Controls.Add(rulesLayout);

        _rulesListBox.Height = 180;

        var settingsPathLabel = new Label
        {
            Text = $"Settings file: {settingsPath}",
            AutoSize = true
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };
        var saveButton = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.None,
            AutoSize = true
        };
        saveButton.Click += (_, _) => SaveAndClose();

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            AutoSize = true
        };

        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(removeRuleButton);

        root.Controls.Add(hotkeyGroup, 0, 0);
        root.Controls.Add(rulesGroup, 0, 2);
        root.Controls.Add(settingsPathLabel, 0, 3);
        root.Controls.Add(buttonPanel, 0, 4);

        Controls.Add(root);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    public AppSettings? UpdatedSettings { get; private set; }

    private void PopulateHotkeyControls()
    {
        foreach (var key in EnumerateSupportedHotkeys())
        {
            _keyComboBox.Items.Add(key);
        }
    }

    private void PopulateProcessCandidates(IReadOnlyCollection<string> candidateProcessNames)
    {
        foreach (var processName in candidateProcessNames
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            _candidateProcessComboBox.Items.Add(processName);
        }

        if (_candidateProcessComboBox.Items.Count > 0)
        {
            _candidateProcessComboBox.SelectedIndex = 0;
        }
    }

    private void PopulateFromSettings(AppSettings settings)
    {
        _controlCheckBox.Checked = settings.Hotkey.Control;
        _shiftCheckBox.Checked = settings.Hotkey.Shift;
        _altCheckBox.Checked = settings.Hotkey.Alt;
        _windowsCheckBox.Checked = settings.Hotkey.Windows;
        _keyComboBox.SelectedItem = settings.Hotkey.Key;

        foreach (var rule in settings.AutoHideProcessNames)
        {
            _rulesListBox.Items.Add(rule);
        }
    }

    private void AddSelectedRule()
    {
        if (_candidateProcessComboBox.SelectedItem is not string processName)
        {
            return;
        }

        if (_rulesListBox.Items.Contains(processName))
        {
            return;
        }

        _rulesListBox.Items.Add(processName);
    }

    private void RemoveSelectedRule()
    {
        if (_rulesListBox.SelectedItem is null)
        {
            return;
        }

        _rulesListBox.Items.Remove(_rulesListBox.SelectedItem);
    }

    private void SaveAndClose()
    {
        if (_keyComboBox.SelectedItem is not Keys key)
        {
            MessageBox.Show(
                "Select a hotkey key before saving.",
                "Program Hider",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!_controlCheckBox.Checked &&
            !_shiftCheckBox.Checked &&
            !_altCheckBox.Checked &&
            !_windowsCheckBox.Checked)
        {
            MessageBox.Show(
                "Choose at least one hotkey modifier.",
                "Program Hider",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        UpdatedSettings = new AppSettings
        {
            Hotkey = new HotkeySettings
            {
                Control = _controlCheckBox.Checked,
                Shift = _shiftCheckBox.Checked,
                Alt = _altCheckBox.Checked,
                Windows = _windowsCheckBox.Checked,
                Key = key
            },
            AutoHideProcessNames = _rulesListBox.Items.Cast<string>().ToList()
        };
        UpdatedSettings.Normalize();

        DialogResult = DialogResult.OK;
        Close();
    }

    private static IEnumerable<Keys> EnumerateSupportedHotkeys()
    {
        foreach (var key in Enumerable.Range((int)Keys.A, 26).Select(value => (Keys)value))
        {
            yield return key;
        }

        foreach (var key in Enumerable.Range((int)Keys.D0, 10).Select(value => (Keys)value))
        {
            yield return key;
        }

        foreach (var key in Enumerable.Range((int)Keys.F1, 24).Select(value => (Keys)value))
        {
            yield return key;
        }

        yield return Keys.Insert;
        yield return Keys.Delete;
        yield return Keys.Home;
        yield return Keys.End;
        yield return Keys.PageUp;
        yield return Keys.PageDown;
    }
}
