using System.Windows.Forms;

namespace ProgramHider;

internal sealed class SettingsForm : Form
{
    private readonly CheckBox _controlCheckBox = new() { Text = "Ctrl", AutoSize = true };
    private readonly CheckBox _shiftCheckBox = new() { Text = "Shift", AutoSize = true };
    private readonly CheckBox _altCheckBox = new() { Text = "Alt", AutoSize = true };
    private readonly CheckBox _windowsCheckBox = new() { Text = "Win", AutoSize = true };
    private readonly CheckBox _launchOnStartupCheckBox = new() { Text = "Launch Program Hider when Windows starts", AutoSize = true };
    private readonly CheckBox _requirePinCheckBox = new() { Text = "Require PIN/password to restore hidden windows", AutoSize = true };
    private readonly ComboBox _keyComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly ComboBox _candidateProcessComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly ListBox _rulesListBox = new() { Height = 180 };
    private readonly TextBox _manualProcessTextBox = new() { Width = 220 };
    private readonly TextBox _pinTextBox = new() { UseSystemPasswordChar = true, Width = 220 };
    private readonly TextBox _confirmPinTextBox = new() { UseSystemPasswordChar = true, Width = 220 };
    private readonly Label _pinHintLabel = new()
    {
        AutoSize = true,
        Text = "Leave blank to keep the existing PIN/password. Disable the option to remove it."
    };

    public SettingsForm(AppSettings currentSettings, IReadOnlyCollection<string> candidateProcessNames, string settingsPath)
    {
        UpdatedSettings = currentSettings.Clone();
        Text = "Program Hider Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(620, 560);

        PopulateHotkeyControls();
        PopulateProcessCandidates(candidateProcessNames);
        PopulateFromSettings(currentSettings);
        _requirePinCheckBox.CheckedChanged += (_, _) => UpdatePinControls();
        UpdatePinControls();

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill
        };

        tabs.TabPages.Add(BuildGeneralTab(settingsPath));
        tabs.TabPages.Add(BuildRulesTab());
        tabs.TabPages.Add(BuildSecurityTab());

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(tabs, 0, 0);
        root.Controls.Add(BuildButtonPanel(), 0, 1);

        Controls.Add(root);
    }

    public AppSettings? UpdatedSettings { get; private set; }

    private TabPage BuildGeneralTab(string settingsPath)
    {
        var tab = new TabPage("General");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 4
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

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

        var startupGroup = new GroupBox
        {
            Text = "Startup",
            Dock = DockStyle.Top,
            AutoSize = true
        };
        var startupLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(10)
        };
        startupLayout.Controls.Add(_launchOnStartupCheckBox);
        startupGroup.Controls.Add(startupLayout);

        var settingsPathLabel = new Label
        {
            Text = $"Settings file: {settingsPath}",
            AutoSize = true
        };

        panel.Controls.Add(hotkeyGroup, 0, 0);
        panel.Controls.Add(startupGroup, 0, 1);
        panel.Controls.Add(settingsPathLabel, 0, 2);
        tab.Controls.Add(panel);
        return tab;
    }

    private TabPage BuildRulesTab()
    {
        var tab = new TabPage("Auto-hide Rules");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 6
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        panel.Controls.Add(
            new Label
            {
                Text = "Apps listed here will be hidden automatically when they are minimized.",
                AutoSize = true
            },
            0,
            0);
        panel.SetColumnSpan(panel.GetControlFromPosition(0, 0)!, 2);

        panel.Controls.Add(new Label { Text = "Open app process", AutoSize = true, Margin = new Padding(0, 14, 0, 4) }, 0, 1);
        panel.Controls.Add(_candidateProcessComboBox, 0, 2);

        var addOpenProcessButton = new Button
        {
            Text = "Add",
            AutoSize = true
        };
        addOpenProcessButton.Click += (_, _) => AddSelectedRule();
        panel.Controls.Add(addOpenProcessButton, 1, 2);

        panel.Controls.Add(new Label { Text = "Manual process name", AutoSize = true, Margin = new Padding(0, 14, 0, 4) }, 0, 3);
        panel.Controls.Add(_manualProcessTextBox, 0, 4);

        var addManualProcessButton = new Button
        {
            Text = "Add Manual",
            AutoSize = true
        };
        addManualProcessButton.Click += (_, _) => AddManualRule();
        panel.Controls.Add(addManualProcessButton, 1, 4);

        panel.Controls.Add(_rulesListBox, 0, 5);
        panel.SetColumnSpan(_rulesListBox, 2);

        var removeRuleButton = new Button
        {
            Text = "Remove selected",
            AutoSize = true
        };
        removeRuleButton.Click += (_, _) => RemoveSelectedRule();
        panel.Controls.Add(removeRuleButton, 1, 6);

        tab.Controls.Add(panel);
        return tab;
    }

    private TabPage BuildSecurityTab()
    {
        var tab = new TabPage("Security");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 5
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(_requirePinCheckBox, 0, 0);
        panel.SetColumnSpan(_requirePinCheckBox, 2);

        panel.Controls.Add(new Label { Text = "New PIN/password", AutoSize = true, Margin = new Padding(0, 16, 8, 0) }, 0, 1);
        panel.Controls.Add(_pinTextBox, 1, 1);

        panel.Controls.Add(new Label { Text = "Confirm PIN/password", AutoSize = true, Margin = new Padding(0, 16, 8, 0) }, 0, 2);
        panel.Controls.Add(_confirmPinTextBox, 1, 2);

        panel.Controls.Add(_pinHintLabel, 0, 3);
        panel.SetColumnSpan(_pinHintLabel, 2);

        tab.Controls.Add(panel);
        return tab;
    }

    private FlowLayoutPanel BuildButtonPanel()
    {
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
        AcceptButton = saveButton;
        CancelButton = cancelButton;
        return buttonPanel;
    }

    private void UpdatePinControls()
    {
        var enabled = _requirePinCheckBox.Checked;
        _pinTextBox.Enabled = enabled;
        _confirmPinTextBox.Enabled = enabled;
        _pinHintLabel.Enabled = enabled;
    }

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
        _launchOnStartupCheckBox.Checked = settings.LaunchOnWindowsStartup;
        _requirePinCheckBox.Checked = settings.RequirePinToRestore;

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

        AddRule(processName);
    }

    private void AddManualRule()
    {
        AddRule(_manualProcessTextBox.Text);
        _manualProcessTextBox.Clear();
    }

    private void AddRule(string processName)
    {
        var normalized = processName.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (_rulesListBox.Items.Cast<string>().Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _rulesListBox.Items.Add(normalized);
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

        var existingPinHash = UpdatedSettings?.PinHash ?? string.Empty;
        var requirePin = _requirePinCheckBox.Checked;
        var pinHash = existingPinHash;
        if (requirePin)
        {
            var newPin = _pinTextBox.Text.Trim();
            var confirmPin = _confirmPinTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(newPin) || !string.IsNullOrWhiteSpace(confirmPin))
            {
                if (newPin.Length < 4)
                {
                    MessageBox.Show(
                        "Use at least 4 characters for the restore PIN/password.",
                        "Program Hider",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                if (!string.Equals(newPin, confirmPin, StringComparison.Ordinal))
                {
                    MessageBox.Show(
                        "PIN/password confirmation does not match.",
                        "Program Hider",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                pinHash = PinSecurity.HashSecret(newPin);
            }
            else if (string.IsNullOrWhiteSpace(existingPinHash))
            {
                MessageBox.Show(
                    "Enter and confirm a PIN/password before enabling restore protection.",
                    "Program Hider",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
        }
        else
        {
            pinHash = string.Empty;
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
            LaunchOnWindowsStartup = _launchOnStartupCheckBox.Checked,
            RequirePinToRestore = requirePin,
            PinHash = pinHash,
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
