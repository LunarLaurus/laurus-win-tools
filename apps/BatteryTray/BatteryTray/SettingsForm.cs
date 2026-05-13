using System.Windows.Forms;

namespace BatteryTray;

public sealed class SettingsForm : Form
{
    public event EventHandler<AppSettings>? SettingsSaved;

    private readonly AppSettings _settings;

    // General
    private NumericUpDown _intervalNud  = null!;
    private NumericUpDown _lowNud       = null!;
    private NumericUpDown _criticalNud  = null!;

    // Notifications
    private CheckBox      _notifyLow         = null!;
    private CheckBox      _notifyCrit        = null!;
    private CheckBox      _notifyFull        = null!;
    private CheckBox      _notifyChargeLimit = null!;
    private NumericUpDown _chargeLimitMins   = null!;
    private CheckBox      _suppressLowDuringSaver = null!;
    private CheckBox      _showTime          = null!;

    // Appearance
    private ComboBox _styleCombo = null!;
    private ComboBox _themeCombo = null!;
    private CheckBox _smoothColors = null!;
    private CheckBox _dpiAware = null!;
    private CheckBox _showSaverIndicator = null!;
    private TextBox _colorCharging = null!;
    private TextBox _colorNormal   = null!;
    private TextBox _colorLow      = null!;
    private TextBox _colorCrit     = null!;
    private TextBox _colorText     = null!;

    // Power
    private CheckBox _powerSwitchEnabled = null!;
    private ComboBox _planOnAc            = null!;
    private ComboBox _planOnBattery       = null!;

    // System
    private CheckBox _runAtStartup = null!;

    private IReadOnlyList<PowerPlan> _powerPlans = Array.Empty<PowerPlan>();

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;

        Text = "BatteryTray Settings";
        ClientSize = new System.Drawing.Size(520, 620);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;

        BuildLayout();
        LoadValues();
    }

    private void BuildLayout()
    {
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new System.Drawing.Point(12, 8),
        };

        var tabGeneral       = new TabPage("General");
        var tabNotifications = new TabPage("Notifications");
        var tabAppearance    = new TabPage("Appearance");
        var tabPower         = new TabPage("Power");
        var tabSystem        = new TabPage("System");

        tabGeneral.Controls.Add(BuildGeneralPanel());
        tabNotifications.Controls.Add(BuildNotificationsPanel());
        tabAppearance.Controls.Add(BuildAppearancePanel());
        tabPower.Controls.Add(BuildPowerPanel());
        tabSystem.Controls.Add(BuildSystemPanel());

        tabs.TabPages.AddRange(new[] { tabGeneral, tabNotifications, tabAppearance, tabPower, tabSystem });

        Controls.Add(tabs);
        Controls.Add(BuildButtonRow());
    }

    private Control BuildGeneralPanel()
    {
        var grid = NewGrid();
        AddRow(grid, "Refresh interval (seconds, 5–300):", _intervalNud = NewNumeric(5, 300));
        AddRow(grid, "Low threshold (%):",                 _lowNud      = NewNumeric(1, 99));
        AddRow(grid, "Critical threshold (%):",            _criticalNud = NewNumeric(1, 99));

        var hint = new Label
        {
            Text = "Updates are event-driven; this interval is a safety net for when\n"
                 + "Windows doesn't fire change events. Higher values = lower CPU.",
            AutoSize = true,
            ForeColor = System.Drawing.SystemColors.GrayText,
            Margin = new Padding(0, 8, 0, 0),
        };
        grid.RowCount += 1;
        grid.Controls.Add(hint, 0, grid.RowCount - 1);
        grid.SetColumnSpan(hint, 2);

        return WrapInPadding(grid);
    }

    private Control BuildNotificationsPanel()
    {
        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(16),
        };

        _notifyLow         = new CheckBox { Text = "Notify on low battery",          AutoSize = true };
        _notifyCrit        = new CheckBox { Text = "Notify on critical battery",     AutoSize = true };
        _notifyFull        = new CheckBox { Text = "Notify when fully charged",      AutoSize = true };
        _notifyChargeLimit = new CheckBox { Text = "Remind to unplug after extended full charge", AutoSize = true };
        _suppressLowDuringSaver = new CheckBox
        {
            Text = "Suppress low-battery alert when Battery Saver is already on",
            AutoSize = true,
        };
        _showTime          = new CheckBox { Text = "Show time remaining / time to full in tooltip", AutoSize = true };

        var chargeLimitRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Margin = new Padding(20, 0, 0, 0),
        };
        chargeLimitRow.Controls.Add(new Label { Text = "after", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        _chargeLimitMins = NewNumeric(15, 480);
        chargeLimitRow.Controls.Add(_chargeLimitMins);
        chargeLimitRow.Controls.Add(new Label { Text = "minutes at 100%", AutoSize = true, Margin = new Padding(4, 6, 0, 0) });

        stack.Controls.Add(_notifyLow);
        stack.Controls.Add(_notifyCrit);
        stack.Controls.Add(_notifyFull);
        stack.Controls.Add(_notifyChargeLimit);
        stack.Controls.Add(chargeLimitRow);
        stack.Controls.Add(new Label { Text = "", Height = 8 });
        stack.Controls.Add(_suppressLowDuringSaver);
        stack.Controls.Add(new Label { Text = "", Height = 8 });
        stack.Controls.Add(_showTime);

        return stack;
    }

    private Control BuildAppearancePanel()
    {
        var grid = NewGrid();

        _styleCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
        _styleCombo.Items.AddRange(new object[] { "Numeric", "Bar", "Both" });

        _themeCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
        _themeCombo.Items.AddRange(new object[] { "Auto (follow Windows)", "Light", "Dark" });

        AddRow(grid, "Icon style:", _styleCombo);
        AddRow(grid, "Theme:",      _themeCombo);
        AddRow(grid, "",            _smoothColors       = new CheckBox { Text = "Smooth color transitions between thresholds", AutoSize = true });
        AddRow(grid, "",            _dpiAware           = new CheckBox { Text = "DPI-aware icon (recommended on hi-DPI)",     AutoSize = true });
        AddRow(grid, "",            _showSaverIndicator = new CheckBox { Text = "Show leaf overlay when Battery Saver is on", AutoSize = true });
        AddRow(grid, "Charging color:", _colorCharging = NewHexBox());
        AddRow(grid, "Normal color:",   _colorNormal   = NewHexBox());
        AddRow(grid, "Low color:",      _colorLow      = NewHexBox());
        AddRow(grid, "Critical color:", _colorCrit     = NewHexBox());
        AddRow(grid, "Text color:",     _colorText     = NewHexBox());

        return WrapInPadding(grid);
    }

    private Control BuildPowerPanel()
    {
        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(16),
        };

        _powerSwitchEnabled = new CheckBox
        {
            Text = "Auto-switch power plan on AC/battery change",
            AutoSize = true,
        };
        _powerSwitchEnabled.CheckedChanged += (_, _) => UpdatePlanComboState();

        var grid = new TableLayoutPanel
        {
            ColumnCount = 2,
            ColumnStyles =
            {
                new ColumnStyle(SizeType.Absolute, 180),
                new ColumnStyle(SizeType.Percent,  100),
            },
            AutoSize = true,
            Margin = new Padding(20, 8, 0, 0),
        };

        _powerPlans = PowerPlanController.List();
        _planOnAc      = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
        _planOnBattery = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
        PopulatePlanCombo(_planOnAc);
        PopulatePlanCombo(_planOnBattery);

        AddRow(grid, "Plan when on AC:",      _planOnAc);
        AddRow(grid, "Plan when on battery:", _planOnBattery);

        var planHint = new Label
        {
            AutoSize = true,
            ForeColor = System.Drawing.SystemColors.GrayText,
            Margin = new Padding(0, 16, 0, 0),
            Text = _powerPlans.Count == 0
                ? "No power plans detected. On Windows 11, run\n"
                  + "  powercfg -duplicatescheme 381b4222-f694-41f0-9685-ff5bb260df2e\n"
                  + "in an elevated prompt to expose the legacy plans."
                : "Plan switching uses powercfg.exe. Set both fields to (no change)\n"
                  + "to leave a direction untouched.",
        };

        // Battery Saver section — explicitly observation-only, with a link to the
        // Settings page rather than a fake "enable" toggle.
        var saverHeader = new Label
        {
            Text = "BATTERY SAVER",
            AutoSize = true,
            ForeColor = System.Drawing.SystemColors.GrayText,
            Font = new System.Drawing.Font(SystemFonts.DefaultFont.FontFamily, 8.5f, System.Drawing.FontStyle.Bold),
            Margin = new Padding(0, 24, 0, 4),
        };

        var saverHint = new Label
        {
            AutoSize = true,
            ForeColor = System.Drawing.SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 8),
            Text = "Windows controls Battery Saver activation directly. BatteryTray\n"
                 + "observes its state (to suppress redundant alerts and show the leaf\n"
                 + "indicator) but cannot enable or disable it — that's by design.",
        };

        var openSaverBtn = new LinkLabel
        {
            Text = "Open Windows Battery Saver settings…",
            AutoSize = true,
        };
        openSaverBtn.LinkClicked += (_, _) => BatterySaverController.OpenBatterySaverSettings();

        stack.Controls.Add(_powerSwitchEnabled);
        stack.Controls.Add(grid);
        stack.Controls.Add(planHint);
        stack.Controls.Add(saverHeader);
        stack.Controls.Add(saverHint);
        stack.Controls.Add(openSaverBtn);

        return stack;
    }

    private Control BuildSystemPanel()
    {
        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(16),
        };

        _runAtStartup = new CheckBox
        {
            Text = "Run at Windows startup (elevated)",
            AutoSize = true,
        };

        var hint = new Label
        {
            Text = "Toggling this creates a Scheduled Task with highest privileges.\n"
                 + "Windows will prompt for UAC consent once when you save. After that,\n"
                 + "BatteryTray launches silently and elevated at every login.",
            AutoSize = true,
            ForeColor = System.Drawing.SystemColors.GrayText,
            Margin = new Padding(20, 4, 0, 12),
        };

        var crashLogLink = new LinkLabel
        {
            Text = "Open crash log folder…",
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0),
        };
        crashLogLink.LinkClicked += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{CrashLogger.GetLogPath()}\"",
                    UseShellExecute = true,
                });
            }
            catch { /* swallow */ }
        };

        stack.Controls.Add(_runAtStartup);
        stack.Controls.Add(hint);
        stack.Controls.Add(crashLogLink);

        return stack;
    }

    private FlowLayoutPanel BuildButtonRow()
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 44,
            Padding = new Padding(8),
        };
        var save = new Button { Text = "Save", Width = 90 };
        save.Click += (_, _) => SaveAndClose();
        var cancel = new Button { Text = "Cancel", Width = 90 };
        cancel.Click += (_, _) => Close();
        row.Controls.Add(save);
        row.Controls.Add(cancel);
        AcceptButton = save;
        CancelButton = cancel;
        return row;
    }

    private void PopulatePlanCombo(ComboBox combo)
    {
        combo.Items.Clear();
        combo.Items.Add(new PlanItem(null, "(no change)"));
        foreach (var plan in _powerPlans)
        {
            combo.Items.Add(new PlanItem(plan.Guid, plan.Name + (plan.IsActive ? " — active" : "")));
        }
        combo.DisplayMember = nameof(PlanItem.Display);
        combo.SelectedIndex = 0;
    }

    private void UpdatePlanComboState()
    {
        _planOnAc.Enabled = _powerSwitchEnabled.Checked;
        _planOnBattery.Enabled = _powerSwitchEnabled.Checked;
    }

    private static TableLayoutPanel NewGrid() => new()
    {
        Dock = DockStyle.Fill,
        ColumnCount = 2,
        ColumnStyles =
        {
            new ColumnStyle(SizeType.Absolute, 220),
            new ColumnStyle(SizeType.Percent,  100),
        },
        AutoSize = true,
    };

    private static Control WrapInPadding(Control inner)
    {
        var outer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
        inner.Dock = DockStyle.Top;
        outer.Controls.Add(inner);
        return outer;
    }

    private static void AddRow(TableLayoutPanel grid, string label, Control control)
    {
        var idx = grid.RowCount;
        grid.RowCount = idx + 1;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Margin = new Padding(0, 6, 6, 6),
        }, 0, idx);
        control.Margin = new Padding(0, 3, 0, 3);
        grid.Controls.Add(control, 1, idx);
    }

    private static NumericUpDown NewNumeric(int min, int max) => new()
    {
        Minimum = min,
        Maximum = max,
        Width = 90,
    };

    private static TextBox NewHexBox() => new() { Width = 100, MaxLength = 9 };

    private void LoadValues()
    {
        _intervalNud.Value     = Math.Clamp(_settings.UpdateIntervalSeconds, 5, 300);
        _lowNud.Value          = _settings.LowBatteryThreshold;
        _criticalNud.Value     = _settings.CriticalBatteryThreshold;

        _notifyLow.Checked            = _settings.NotifyOnLow;
        _notifyCrit.Checked           = _settings.NotifyOnCritical;
        _notifyFull.Checked           = _settings.NotifyOnFullyCharged;
        _notifyChargeLimit.Checked    = _settings.NotifyOnChargeLimitReached;
        _chargeLimitMins.Value        = Math.Clamp(_settings.ChargeLimitMinutesAtFull, 15, 480);
        _suppressLowDuringSaver.Checked = _settings.SuppressLowAlertWhenBatterySaverActive;
        _showTime.Checked             = _settings.ShowTimeRemainingInTooltip;

        _styleCombo.SelectedIndex   = (int)_settings.Style;
        _themeCombo.SelectedIndex   = (int)_settings.Theme;
        _smoothColors.Checked       = _settings.SmoothColorTransitions;
        _dpiAware.Checked           = _settings.DpiAwareIcon;
        _showSaverIndicator.Checked = _settings.ShowBatterySaverIndicator;
        _colorCharging.Text         = _settings.ColorCharging;
        _colorNormal.Text           = _settings.ColorNormal;
        _colorLow.Text              = _settings.ColorLow;
        _colorCrit.Text             = _settings.ColorCritical;
        _colorText.Text             = _settings.ColorText;

        _powerSwitchEnabled.Checked = _settings.PowerPlanAutoSwitchEnabled;
        SelectPlan(_planOnAc,      _settings.PowerPlanOnAcGuid);
        SelectPlan(_planOnBattery, _settings.PowerPlanOnBatteryGuid);
        UpdatePlanComboState();

        _runAtStartup.Checked = StartupManager.GetRunAtStartup();
    }

    private static void SelectPlan(ComboBox combo, string? guidStr)
    {
        if (!Guid.TryParse(guidStr, out var g)) { combo.SelectedIndex = 0; return; }
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is PlanItem p && p.Guid == g)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }

    private void SaveAndClose()
    {
        if (_criticalNud.Value > _lowNud.Value)
        {
            MessageBox.Show(
                "Critical threshold must be less than or equal to low threshold.",
                "Invalid settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _settings.UpdateIntervalSeconds        = (int)_intervalNud.Value;
        _settings.LowBatteryThreshold          = (int)_lowNud.Value;
        _settings.CriticalBatteryThreshold     = (int)_criticalNud.Value;

        _settings.NotifyOnLow                  = _notifyLow.Checked;
        _settings.NotifyOnCritical             = _notifyCrit.Checked;
        _settings.NotifyOnFullyCharged         = _notifyFull.Checked;
        _settings.NotifyOnChargeLimitReached   = _notifyChargeLimit.Checked;
        _settings.ChargeLimitMinutesAtFull     = (int)_chargeLimitMins.Value;
        _settings.SuppressLowAlertWhenBatterySaverActive = _suppressLowDuringSaver.Checked;
        _settings.ShowTimeRemainingInTooltip   = _showTime.Checked;

        _settings.Style                        = (IconStyle)_styleCombo.SelectedIndex;
        _settings.Theme                        = (IconTheme)_themeCombo.SelectedIndex;
        _settings.SmoothColorTransitions       = _smoothColors.Checked;
        _settings.DpiAwareIcon                 = _dpiAware.Checked;
        _settings.ShowBatterySaverIndicator    = _showSaverIndicator.Checked;
        _settings.ColorCharging                = _colorCharging.Text.Trim();
        _settings.ColorNormal                  = _colorNormal.Text.Trim();
        _settings.ColorLow                     = _colorLow.Text.Trim();
        _settings.ColorCritical                = _colorCrit.Text.Trim();
        _settings.ColorText                    = _colorText.Text.Trim();

        _settings.PowerPlanAutoSwitchEnabled   = _powerSwitchEnabled.Checked;
        _settings.PowerPlanOnAcGuid            = (_planOnAc.SelectedItem      as PlanItem)?.Guid?.ToString();
        _settings.PowerPlanOnBatteryGuid       = (_planOnBattery.SelectedItem as PlanItem)?.Guid?.ToString();

        _settings.RunAtStartup                 = _runAtStartup.Checked;

        try
        {
            _settings.Save();
        }
        catch (Exception ex)
        {
            CrashLogger.Write("SaveAndClose", ex);
            MessageBox.Show(
                $"Failed to save settings: {ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        SettingsSaved?.Invoke(this, _settings);
        Close();
    }

    private sealed record PlanItem(Guid? Guid, string Display)
    {
        public override string ToString() => Display;
    }
}
