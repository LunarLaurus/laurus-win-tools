using System.Text.Json;
using System.Windows.Forms;

namespace ProgramHider;

internal sealed class SettingsForm : Form
{
    private static readonly JsonSerializerOptions RuleJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly CheckBox _controlCheckBox = new() { Text = "Ctrl", AutoSize = true };
    private readonly CheckBox _shiftCheckBox = new() { Text = "Shift", AutoSize = true };
    private readonly CheckBox _altCheckBox = new() { Text = "Alt", AutoSize = true };
    private readonly CheckBox _windowsCheckBox = new() { Text = "Win", AutoSize = true };
    private readonly CheckBox _launchOnStartupCheckBox = new() { Text = "Launch Program Hider when Windows starts", AutoSize = true };
    private readonly CheckBox _requirePinCheckBox = new() { Text = "Require PIN/password to restore hidden windows", AutoSize = true };
    private readonly ComboBox _keyComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly ComboBox _candidateProcessComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly ListView _rulesListView = new()
    {
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = false,
        HideSelection = false,
        Dock = DockStyle.Fill
    };
    private readonly TextBox _pinTextBox = new() { UseSystemPasswordChar = true, Width = 220 };
    private readonly TextBox _confirmPinTextBox = new() { UseSystemPasswordChar = true, Width = 220 };
    private readonly Label _pinHintLabel = new()
    {
        AutoSize = true,
        Text = "Leave blank to keep the existing PIN/password. Disable the option to remove it."
    };
    private readonly List<WindowRule> _workingRules;

    public SettingsForm(AppSettings currentSettings, IReadOnlyCollection<string> candidateProcessNames, string settingsPath)
    {
        UpdatedSettings = currentSettings.Clone();
        _workingRules = currentSettings.WindowRules.Select(rule => rule.Clone()).ToList();

        Text = "Program Hider Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(860, 640);

        _rulesListView.Columns.Add("Rule", 180);
        _rulesListView.Columns.Add("Match", 360);
        _rulesListView.Columns.Add("Behavior", 220);

        PopulateHotkeyControls();
        PopulateProcessCandidates(candidateProcessNames);
        PopulateFromSettings(currentSettings);
        _requirePinCheckBox.CheckedChanged += (_, _) => UpdatePinControls();
        UpdatePinControls();
        RefreshRulesListView();

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
        var tab = new TabPage("Rules");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 4
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        panel.Controls.Add(
            new Label
            {
                Text = "Rules can match process names, title substrings, and class names. Matching rules can auto-hide, require PIN restore, and suppress notifications.",
                AutoSize = true
            },
            0,
            0);

        var quickAddPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Margin = new Padding(0, 12, 0, 12)
        };
        quickAddPanel.Controls.Add(new Label { Text = "Quick add process rule", AutoSize = true, Margin = new Padding(0, 8, 8, 0) });
        quickAddPanel.Controls.Add(_candidateProcessComboBox);
        var quickAddButton = new Button
        {
            Text = "Add Process Rule",
            AutoSize = true
        };
        quickAddButton.Click += (_, _) => AddQuickProcessRule();
        quickAddPanel.Controls.Add(quickAddButton);
        panel.Controls.Add(quickAddPanel, 0, 1);

        panel.Controls.Add(_rulesListView, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true
        };
        buttons.Controls.Add(CreateButton("Add...", (_, _) => AddRule()));
        buttons.Controls.Add(CreateButton("Edit...", (_, _) => EditSelectedRule()));
        buttons.Controls.Add(CreateButton("Remove", (_, _) => RemoveSelectedRule()));
        buttons.Controls.Add(CreateButton("Import...", (_, _) => ImportRules()));
        buttons.Controls.Add(CreateButton("Export...", (_, _) => ExportRules()));
        panel.Controls.Add(buttons, 0, 3);

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

    private static Button CreateButton(string text, EventHandler onClick)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true
        };
        button.Click += onClick;
        return button;
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
    }

    private void AddQuickProcessRule()
    {
        if (_candidateProcessComboBox.SelectedItem is not string processName)
        {
            return;
        }

        var rule = new WindowRule
        {
            RuleName = $"{processName} auto-hide",
            MatchProcessName = processName,
            AutoHideOnMinimize = true
        };
        rule.Normalize();
        UpsertRule(rule, null);
    }

    private void AddRule()
    {
        using var editor = new RuleEditorForm();
        if (editor.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        UpsertRule(editor.EditedRule, null);
    }

    private void EditSelectedRule()
    {
        var index = GetSelectedRuleIndex();
        if (index is null)
        {
            return;
        }

        using var editor = new RuleEditorForm(_workingRules[index.Value]);
        if (editor.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        UpsertRule(editor.EditedRule, index.Value);
    }

    private void UpsertRule(WindowRule rule, int? replaceIndex)
    {
        var duplicateIndex = _workingRules.FindIndex(
            existing => string.Equals(existing.GetIdentityKey(), rule.GetIdentityKey(), StringComparison.OrdinalIgnoreCase));

        if (duplicateIndex >= 0 && duplicateIndex != replaceIndex)
        {
            MessageBox.Show(
                "A rule with the same match fields and behavior already exists.",
                "Program Hider",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (replaceIndex is int index)
        {
            _workingRules[index] = rule.Clone();
        }
        else
        {
            _workingRules.Add(rule.Clone());
        }

        SortRules();
        RefreshRulesListView();
    }

    private void RemoveSelectedRule()
    {
        var index = GetSelectedRuleIndex();
        if (index is null)
        {
            return;
        }

        _workingRules.RemoveAt(index.Value);
        RefreshRulesListView();
    }

    private void ImportRules()
    {
        using var openDialog = new OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Import Program Hider Rules"
        };

        if (openDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var importedRules = JsonSerializer.Deserialize<List<WindowRule>>(File.ReadAllText(openDialog.FileName), RuleJsonOptions)
                                ?? new List<WindowRule>();

            foreach (var rule in importedRules)
            {
                rule.Normalize();
            }

            importedRules = importedRules.Where(rule => rule.HasAnyMatchField).ToList();
            if (importedRules.Count == 0)
            {
                MessageBox.Show(
                    "The selected file did not contain any usable rules.",
                    "Program Hider",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var mergeMode = MessageBox.Show(
                "Choose Yes to replace current rules, No to merge, or Cancel to abort.",
                "Import Rules",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (mergeMode == DialogResult.Cancel)
            {
                return;
            }

            if (mergeMode == DialogResult.Yes)
            {
                _workingRules.Clear();
            }

            foreach (var rule in importedRules)
            {
                if (_workingRules.Any(existing => string.Equals(existing.GetIdentityKey(), rule.GetIdentityKey(), StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                _workingRules.Add(rule.Clone());
            }

            SortRules();
            RefreshRulesListView();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Unable to import rules.{Environment.NewLine}{Environment.NewLine}{exception.Message}",
                "Program Hider",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void ExportRules()
    {
        using var saveDialog = new SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Export Program Hider Rules",
            FileName = "program-hider-rules.json"
        };

        if (saveDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(_workingRules, RuleJsonOptions);
            File.WriteAllText(saveDialog.FileName, json);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Unable to export rules.{Environment.NewLine}{Environment.NewLine}{exception.Message}",
                "Program Hider",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void SortRules()
    {
        _workingRules.Sort((left, right) => string.Compare(left.RuleName, right.RuleName, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshRulesListView()
    {
        _rulesListView.BeginUpdate();
        try
        {
            _rulesListView.Items.Clear();
            foreach (var rule in _workingRules)
            {
                var item = new ListViewItem(rule.RuleName);
                item.SubItems.Add(rule.DescribeMatch());
                item.SubItems.Add(rule.DescribeBehavior());
                _rulesListView.Items.Add(item);
            }
        }
        finally
        {
            _rulesListView.EndUpdate();
        }
    }

    private int? GetSelectedRuleIndex()
    {
        if (_rulesListView.SelectedIndices.Count == 0)
        {
            return null;
        }

        return _rulesListView.SelectedIndices[0];
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
            WindowRules = _workingRules.Select(rule => rule.Clone()).ToList(),
            AutoHideProcessNames = new List<string>()
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
