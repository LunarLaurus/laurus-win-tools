using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using WindowsAppCore;

namespace BatteryTray;

public sealed class BatteryInfoForm : Form
{
    private readonly BatteryMonitor _monitor;
    private readonly RateHistory _history;
    private readonly ProcessPowerCoordinator _powerCoordinator;
    private System.Windows.Forms.Timer? _refreshTimer;

    // Status tab labels
    private Label _vStatus       = null!;
    private Label _vCharge       = null!;
    private Label _vPowerFlow    = null!;
    private Label _vCapacity     = null!;
    private Label _vTime         = null!;
    private Label _vTimeLabel    = null!;
    private Label _vDevice       = null!;
    private Label _vManufacturer = null!;
    private Label _vSerial       = null!;
    private Label _vChemistry    = null!;
    private Label _vChemistrySource = null!;
    private Label _vDesignCap    = null!;
    private Label _vFullCap      = null!;
    private Label _vHealth       = null!;
    private Label _vCycles       = null!;

    private SparklinePanel _sparkline = null!;

    // Process tab
    private ListView _processList = null!;
    private Label    _processSourceLabel = null!;
    private Label    _processSourceDetailsLabel = null!;

    // Diagnostics tab
    private ListView _diagSamplersList = null!;
    private Label    _diagBatteryLabel = null!;
    private Label    _diagSaverLabel = null!;
    private Label    _diagToastLabel = null!;
    private Label    _diagElevationLabel = null!;
    private Label    _diagPowerPlansLabel = null!;
    private Label    _diagWmiClassesLabel = null!;

    public BatteryInfoForm(BatteryMonitor monitor, RateHistory history, ProcessPowerCoordinator powerCoordinator)
    {
        _monitor = monitor;
        _history = history;
        _powerCoordinator = powerCoordinator;

        Text = "Battery Info";
        ClientSize = new Size(680, 760);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(580, 600);
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        DoubleBuffered = true;

        BuildLayout();
        Reload();

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _refreshTimer.Tick += (_, _) => Reload();
        _refreshTimer.Start();

        FormClosed += (_, _) =>
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            _refreshTimer = null;
        };
    }

    private void BuildLayout()
    {
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(12, 8),
        };

        var tabStatus = new TabPage("Status");
        tabStatus.Controls.Add(BuildStatusPanel());

        var tabProcesses = new TabPage("Processes");
        tabProcesses.Controls.Add(BuildProcessesPanel());

        var tabDiagnostics = new TabPage("Diagnostics");
        tabDiagnostics.Controls.Add(BuildDiagnosticsPanel());

        tabs.TabPages.AddRange(new[] { tabStatus, tabProcesses, tabDiagnostics });

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 44,
            Padding = new Padding(8),
        };
        var refresh = new Button { Text = "Refresh", Width = 90 };
        refresh.Click += (_, _) =>
        {
            _monitor.InvalidateCache();
            Reload();
        };
        var close = new Button { Text = "&Close", Width = 90 };
        close.Click += (_, _) => Close();
        buttons.Controls.Add(close);
        buttons.Controls.Add(refresh);
        AcceptButton = close;
        CancelButton = close;

        Controls.Add(tabs);
        Controls.Add(buttons);
    }

    private Control BuildStatusPanel()
    {
        var outer = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Padding = new Padding(16),
            ColumnCount = 2,
            ColumnStyles =
            {
                new ColumnStyle(SizeType.Absolute, 200),
                new ColumnStyle(SizeType.Percent,  100),
            },
            AutoSize = true,
        };

        AddSectionHeader(grid, "Current state");
        _vStatus    = AddRow(grid, "Status:");
        _vCharge    = AddRow(grid, "Charge:");
        _vPowerFlow = AddRow(grid, "Power flow:");
        _vCapacity  = AddRow(grid, "Capacity:");
        _vTimeLabel = AddSwingLabel(grid);
        _vTime      = (Label)grid.GetControlFromPosition(1, grid.RowCount - 1)!;

        AddSpacer(grid);
        AddSectionHeader(grid, "Hardware");
        _vDevice          = AddRow(grid, "Device:");
        _vManufacturer    = AddRow(grid, "Manufacturer:");
        _vSerial          = AddRow(grid, "Serial:");
        _vChemistry       = AddRow(grid, "Chemistry:");
        _vChemistrySource = AddRow(grid, "Chemistry source:");

        AddSpacer(grid);
        AddSectionHeader(grid, "Health");
        _vDesignCap = AddRow(grid, "Design capacity:");
        _vFullCap   = AddRow(grid, "Full charge capacity:");
        _vHealth    = AddRow(grid, "Health:");
        _vCycles    = AddRow(grid, "Cycle count:");

        AddSpacer(grid);
        AddSectionHeader(grid, "Recent power flow (last hour)");

        _sparkline = new SparklinePanel(_history)
        {
            Dock = DockStyle.Top,
            Height = 110,
            Margin = new Padding(0, 8, 0, 0),
        };

        outer.Controls.Add(_sparkline);
        outer.Controls.Add(grid);
        return outer;
    }

    private Control BuildProcessesPanel()
    {
        var outer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            Padding = new Padding(8, 8, 8, 4),
        };

        _processSourceLabel = new Label
        {
            Text = "Power source: …",
            AutoSize = true,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
        };
        _processSourceDetailsLabel = new Label
        {
            Text = "",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 4, 0, 6),
        };
        var caveat = new Label
        {
            Text = "Estimates only. For authoritative per-app energy use, see Task Manager → Power usage,\n"
                 + "or run `powercfg /batteryreport` from an admin prompt.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 4),
        };

        header.Controls.Add(_processSourceLabel);
        header.Controls.Add(_processSourceDetailsLabel);
        header.Controls.Add(caveat);

        _processList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            HideSelection = false,
        };
        _processList.Columns.Add("Process",  220, HorizontalAlignment.Left);
        _processList.Columns.Add("PID",       60, HorizontalAlignment.Right);
        _processList.Columns.Add("CPU %",     70, HorizontalAlignment.Right);
        _processList.Columns.Add("Power (W)", 90, HorizontalAlignment.Right);

        _processList.ColumnClick += OnProcessColumnClick;

        outer.Controls.Add(_processList);
        outer.Controls.Add(header);
        return outer;
    }

    private Control BuildDiagnosticsPanel()
    {
        var outer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8), AutoScroll = true };

        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            Padding = new Padding(8),
        };

        // ---- Power samplers section ----
        AddDiagHeader(stack, "Power sampler tiers");

        var samplersHint = new Label
        {
            Text = "BatteryTray tries three data sources for per-process power. The highest-tier\n"
                 + "available source is used; lower tiers act as fallbacks. Status updates live.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 6),
        };
        stack.Controls.Add(samplersHint);

        _diagSamplersList = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            HideSelection = true,
            Height = 110,
            Width = 620,
        };
        _diagSamplersList.Columns.Add("Tier",   60,  HorizontalAlignment.Left);
        _diagSamplersList.Columns.Add("Source", 200, HorizontalAlignment.Left);
        _diagSamplersList.Columns.Add("State",  120, HorizontalAlignment.Left);
        _diagSamplersList.Columns.Add("Detail", 220, HorizontalAlignment.Left);
        stack.Controls.Add(_diagSamplersList);

        AddDiagSpacer(stack);

        // ---- System capabilities section ----
        AddDiagHeader(stack, "System capabilities");
        _diagBatteryLabel    = AddDiagLine(stack, "Battery presence:");
        _diagSaverLabel      = AddDiagLine(stack, "Battery Saver state:");
        _diagToastLabel      = AddDiagLine(stack, "Toast notifications:");
        _diagElevationLabel  = AddDiagLine(stack, "Process elevation:");
        _diagPowerPlansLabel = AddDiagLine(stack, "Power plan API:");
        _diagWmiClassesLabel = AddDiagLine(stack, "Battery WMI classes:");

        AddDiagSpacer(stack);

        // ---- Useful commands section ----
        AddDiagHeader(stack, "Helpful diagnostics");

        var openCrashLog = new LinkLabel
        {
            Text = "Open crash log folder…",
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 4),
        };
        openCrashLog.LinkClicked += (_, _) =>
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
            catch { }
        };
        stack.Controls.Add(openCrashLog);

        var batteryReportLink = new LinkLabel
        {
            Text = "Generate Windows battery report (powercfg /batteryreport)…",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4),
        };
        batteryReportLink.LinkClicked += (_, _) => GenerateBatteryReport();
        stack.Controls.Add(batteryReportLink);

        var taskManagerLink = new LinkLabel
        {
            Text = "Open Task Manager (for authoritative per-app power usage)…",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4),
        };
        taskManagerLink.LinkClicked += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "taskmgr.exe",
                    UseShellExecute = true,
                });
            }
            catch { }
        };
        stack.Controls.Add(taskManagerLink);

        outer.Controls.Add(stack);
        return outer;
    }

    private static void GenerateBatteryReport()
    {
        try
        {
            var output = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "BatteryTray-batteryreport.html");

            // /batteryreport requires no admin and writes HTML by default.
            using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = $"/batteryreport /output \"{output}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            });
            p?.WaitForExit(5000);

            if (p?.ExitCode == 0 && System.IO.File.Exists(output))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = output,
                    UseShellExecute = true,
                });
            }
            else
            {
                MessageBox.Show("Battery report generation failed.",
                    "Battery report", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            CrashLogger.Write("GenerateBatteryReport", ex);
            MessageBox.Show($"Couldn't generate battery report: {ex.Message}",
                "Battery report", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private int _sortColumn = 3;
    private bool _sortAscending = false;

    private void OnProcessColumnClick(object? sender, ColumnClickEventArgs e)
    {
        if (_sortColumn == e.Column) _sortAscending = !_sortAscending;
        else { _sortColumn = e.Column; _sortAscending = e.Column == 0; }
        Reload();
    }

    private void Reload()
    {
        var state = _monitor.Read();
        var health = BatteryHealthReader.Read();

        // ---- Status tab ----
        _vStatus.Text = !state.HasBattery ? "No battery detected"
                      : state.IsCharging  ? "Charging"
                      : state.IsOnAcPower ? "Plugged in (idle / fully charged)"
                                          : "On battery";

        _vCharge.Text = state.HasBattery ? $"{state.Percent}%" : "—";

        if (state.ChargeRateMilliwatts is int rate)
        {
            if (rate == 0) _vPowerFlow.Text = "Idle";
            else
            {
                var watts = Math.Abs(rate) / 1000.0;
                var direction = rate > 0 ? "↑ in (charging)" : "↓ out (discharging)";
                _vPowerFlow.Text = $"{watts:F2} W  {direction}";
            }
        }
        else _vPowerFlow.Text = "—";

        if (state.RemainingMilliwattHours is int rem
            && state.FullChargeMilliwattHours is int max)
        {
            _vCapacity.Text = $"{rem:N0} mWh / {max:N0} mWh  ({rem / 1000.0:F1} / {max / 1000.0:F1} Wh)";
        }
        else _vCapacity.Text = "—";

        if (state.IsCharging && state.SecondsToFull is int toFull && toFull > 0)
        {
            _vTimeLabel.Text = "Time to full:";
            _vTime.Text = TimeFormat.Duration(toFull);
        }
        else if (!state.IsCharging && state.HasBattery
                 && state.SecondsRemaining is int toEmpty && toEmpty > 0)
        {
            _vTimeLabel.Text = "Time remaining:";
            _vTime.Text = TimeFormat.Duration(toEmpty);
        }
        else
        {
            _vTimeLabel.Text = "Time estimate:";
            _vTime.Text = state.IsCharging ? "Calculating…" : "—";
        }

        _vDevice.Text          = health?.DeviceName       ?? "—";
        _vManufacturer.Text    = health?.Manufacturer     ?? "—";
        _vSerial.Text          = health?.SerialNumber     ?? "—";
        _vChemistry.Text       = health?.Chemistry        ?? "—";
        _vChemistrySource.Text = health?.ChemistrySource  ?? "—";
        _vDesignCap.Text       = FormatMilliwattHours(health?.DesignCapacityMilliwattHours);
        _vFullCap.Text         = FormatMilliwattHours(health?.FullChargedCapacityMilliwattHours);

        if (health?.HealthPercent is double healthPct)
        {
            var label = healthPct.ToString("F1", CultureInfo.InvariantCulture) + "%";
            label += healthPct >= 80 ? "  (good)"
                   : healthPct >= 60 ? "  (worn)"
                                     : "  (degraded)";
            _vHealth.Text = label;
        }
        else _vHealth.Text = "—";

        _vCycles.Text = health?.CycleCount?.ToString(CultureInfo.InvariantCulture)
                        ?? "Not exposed by this device";

        _sparkline.Invalidate();

        // ---- Processes tab ----
        UpdateProcessList();

        // ---- Diagnostics tab ----
        UpdateDiagnostics(state, health);
    }

    private void UpdateProcessList()
    {
        var (source, status, samples) = _powerCoordinator.GetCurrent();

        _processSourceLabel.Text = "Power source: " + source switch
        {
            PowerSamplerSource.EtwEnergyEstimation => "ETW EnergyEstimation",
            PowerSamplerSource.EnergyMeterWmi      => "Hardware EnergyMeter",
            _                                      => "CPU + IO counters",
        };
        _processSourceDetailsLabel.Text = status;

        var sorted = SortSamples(samples);
        const int maxRows = 30;
        if (sorted.Length > maxRows) sorted = sorted[..maxRows];

        _processList.BeginUpdate();
        try
        {
            _processList.Items.Clear();
            if (sorted.Length == 0)
            {
                var item = new ListViewItem("(no data yet)") { ForeColor = SystemColors.GrayText };
                item.SubItems.Add("");
                item.SubItems.Add("");
                item.SubItems.Add("");
                _processList.Items.Add(item);
                return;
            }

            foreach (var s in sorted)
            {
                var item = new ListViewItem(s.ProcessName);
                item.SubItems.Add(s.ProcessId.ToString(CultureInfo.InvariantCulture));
                item.SubItems.Add(s.CpuPercent > 0 ? s.CpuPercent.ToString("F1", CultureInfo.InvariantCulture) : "—");
                item.SubItems.Add(s.EstimatedWatts.ToString("F2", CultureInfo.InvariantCulture));
                _processList.Items.Add(item);
            }
        }
        finally
        {
            _processList.EndUpdate();
        }
    }

    private void UpdateDiagnostics(BatteryState state, BatteryHealthInfo? health)
    {
        // ---- Sampler tier list ----
        var tiers = _powerCoordinator.GetSamplerStates();

        _diagSamplersList.BeginUpdate();
        try
        {
            _diagSamplersList.Items.Clear();
            foreach (var (src, healthy, hasData, status) in tiers)
            {
                var tier = src switch
                {
                    PowerSamplerSource.EtwEnergyEstimation => "Tier 3",
                    PowerSamplerSource.EnergyMeterWmi      => "Tier 2",
                    _                                      => "Tier 1",
                };
                var sourceName = src switch
                {
                    PowerSamplerSource.EtwEnergyEstimation => "ETW EnergyEstimation",
                    PowerSamplerSource.EnergyMeterWmi      => "Hardware EnergyMeter",
                    _                                      => "CPU + IO performance counters",
                };
                var stateText = (healthy, hasData) switch
                {
                    (true,  true)  => "✓ Active",
                    (true,  false) => "↻ Warming up",
                    (false, _)     => "✗ Unavailable",
                };

                var item = new ListViewItem(tier);
                item.SubItems.Add(sourceName);
                item.SubItems.Add(stateText);
                item.SubItems.Add(status);

                item.ForeColor = (healthy, hasData) switch
                {
                    (true,  true)  => Color.FromArgb(46, 125, 50),    // green
                    (true,  false) => Color.FromArgb(245, 124, 0),    // amber
                    (false, _)     => Color.FromArgb(120, 120, 120),  // gray
                };
                _diagSamplersList.Items.Add(item);
            }

            // If no entries at all (which would be a bug), surface that visibly.
            if (_diagSamplersList.Items.Count == 0)
            {
                var item = new ListViewItem("—");
                item.SubItems.Add("No samplers registered");
                item.SubItems.Add("");
                item.SubItems.Add("");
                _diagSamplersList.Items.Add(item);
            }
        }
        finally { _diagSamplersList.EndUpdate(); }

        // ---- System capabilities ----
        _diagBatteryLabel.Text = state.HasBattery
            ? $"Detected ({state.Percent}%, {(state.IsOnAcPower ? "AC" : "battery")})"
            : "Not detected (desktop or AC-only system)";

        _diagSaverLabel.Text = state.BatterySaverActive ? "Active" : "Inactive";

        _diagToastLabel.Text = "Probe deferred to runtime — see About dialog";

        _diagElevationLabel.Text = ElevationHelper.IsElevated() ? "Elevated (admin)" : "Standard user";

        var plans = TryListPowerPlans();
        _diagPowerPlansLabel.Text = plans is null
            ? "powercfg.exe unavailable"
            : $"{plans.Count} plans detected";

        // Probe each WMI class for diagnostic visibility.
        var wmiResults = new List<string>();
        wmiResults.Add($"Win32_Battery: {(health is not null ? "ok" : "n/a")}");
        wmiResults.Add($"BatteryStaticData: {(health?.DesignCapacityMilliwattHours is not null ? "ok" : "missing")}");
        wmiResults.Add($"BatteryFullChargedCapacity: {(health?.FullChargedCapacityMilliwattHours is not null ? "ok" : "missing")}");
        wmiResults.Add($"BatteryCycleCount: {(health?.CycleCount is not null ? "ok" : "missing")}");
        _diagWmiClassesLabel.Text = string.Join("  ·  ", wmiResults);
    }

    private static IReadOnlyList<PowerPlan>? TryListPowerPlans()
    {
        try { return PowerPlanController.List(); }
        catch { return null; }
    }

    private ProcessPowerSample[] SortSamples(ProcessPowerSample[] samples)
    {
        if (samples.Length == 0) return samples;

        IEnumerable<ProcessPowerSample> ordered = _sortColumn switch
        {
            0 => _sortAscending
                ? samples.OrderBy(s => s.ProcessName, StringComparer.OrdinalIgnoreCase)
                : samples.OrderByDescending(s => s.ProcessName, StringComparer.OrdinalIgnoreCase),
            1 => _sortAscending
                ? samples.OrderBy(s => s.ProcessId)
                : samples.OrderByDescending(s => s.ProcessId),
            2 => _sortAscending
                ? samples.OrderBy(s => s.CpuPercent)
                : samples.OrderByDescending(s => s.CpuPercent),
            _ => _sortAscending
                ? samples.OrderBy(s => s.EstimatedWatts)
                : samples.OrderByDescending(s => s.EstimatedWatts),
        };
        return ordered.ToArray();
    }

    // ---- Layout helpers ----

    private static Label AddRow(TableLayoutPanel grid, string label)
    {
        var idx = grid.RowCount;
        grid.RowCount = idx + 1;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        grid.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
            Margin = new Padding(0, 4, 8, 4),
        }, 0, idx);

        var value = new Label
        {
            Text = "—",
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Margin = new Padding(0, 4, 0, 4),
        };
        grid.Controls.Add(value, 1, idx);
        return value;
    }

    private static Label AddSwingLabel(TableLayoutPanel grid)
    {
        var idx = grid.RowCount;
        grid.RowCount = idx + 1;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label = new Label
        {
            Text = "Time:",
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
            Margin = new Padding(0, 4, 8, 4),
        };
        grid.Controls.Add(label, 0, idx);

        var value = new Label
        {
            Text = "—",
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Margin = new Padding(0, 4, 0, 4),
        };
        grid.Controls.Add(value, 1, idx);
        return label;
    }

    private static void AddSectionHeader(TableLayoutPanel grid, string text)
    {
        var idx = grid.RowCount;
        grid.RowCount = idx + 1;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new Label
        {
            Text = text.ToUpperInvariant(),
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 8.5f, FontStyle.Bold),
            Margin = new Padding(0, 4, 0, 2),
        };
        grid.Controls.Add(header, 0, idx);
        grid.SetColumnSpan(header, 2);
    }

    private static void AddSpacer(TableLayoutPanel grid)
    {
        var idx = grid.RowCount;
        grid.RowCount = idx + 1;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));
        grid.Controls.Add(new Label { Text = "", AutoSize = true }, 0, idx);
    }

    // Diagnostics-tab helpers (single-column TableLayoutPanel, so simpler).
    private static void AddDiagHeader(TableLayoutPanel stack, string text)
    {
        stack.Controls.Add(new Label
        {
            Text = text.ToUpperInvariant(),
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 8.5f, FontStyle.Bold),
            Margin = new Padding(0, 8, 0, 4),
        });
    }

    private static Label AddDiagLine(TableLayoutPanel stack, string label)
    {
        var row = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 2),
        };
        row.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
            Margin = new Padding(0, 0, 8, 0),
        });
        var value = new Label
        {
            Text = "…",
            AutoSize = true,
        };
        row.Controls.Add(value);
        stack.Controls.Add(row);
        return value;
    }

    private static void AddDiagSpacer(TableLayoutPanel stack)
    {
        stack.Controls.Add(new Label { Text = "", AutoSize = true, Height = 8 });
    }

    private static string FormatMilliwattHours(uint? mWh)
    {
        if (mWh is null) return "—";
        var wh = mWh.Value / 1000.0;
        return $"{mWh.Value:N0} mWh  ({wh:F1} Wh)";
    }

    private sealed class SparklinePanel : Panel
    {
        private readonly RateHistory _history;

        public SparklinePanel(RateHistory history)
        {
            _history = history;
            DoubleBuffered = true;
            BackColor = SystemColors.Window;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(8, 8, ClientSize.Width - 16, ClientSize.Height - 16);
            using (var border = new Pen(SystemColors.ControlDark)) g.DrawRectangle(border, rect);

            var samples = _history.Snapshot();
            if (samples.Length < 2)
            {
                using var brush = new SolidBrush(SystemColors.GrayText);
                using var f = SystemFonts.DefaultFont;
                var msg = "Collecting data… (graph appears after a few minutes)";
                var size = g.MeasureString(msg, f);
                g.DrawString(msg, f, brush, rect.Left + (rect.Width - size.Width) / 2, rect.Top + (rect.Height - size.Height) / 2);
                return;
            }

            int maxAbs = 1000;
            foreach (var s in samples) maxAbs = Math.Max(maxAbs, Math.Abs(s.RateMilliwatts));

            var midY = rect.Top + rect.Height / 2;
            using (var zero = new Pen(SystemColors.ControlDark) { DashStyle = DashStyle.Dot })
            {
                g.DrawLine(zero, rect.Left + 1, midY, rect.Right - 1, midY);
            }

            var first = samples[0].At;
            var last  = samples[^1].At;
            var span  = (last - first).TotalSeconds;
            if (span < 1) span = 1;

            using var charging    = new Pen(Color.FromArgb(67, 160, 71),  2f);
            using var discharging = new Pen(Color.FromArgb(251, 140, 0), 2f);

            for (int i = 1; i < samples.Length; i++)
            {
                var a = samples[i - 1];
                var b = samples[i];

                int xa = rect.Left + (int)((a.At - first).TotalSeconds / span * rect.Width);
                int xb = rect.Left + (int)((b.At - first).TotalSeconds / span * rect.Width);
                int ya = midY - (int)((double)a.RateMilliwatts / maxAbs * (rect.Height / 2 - 4));
                int yb = midY - (int)((double)b.RateMilliwatts / maxAbs * (rect.Height / 2 - 4));

                var pen = b.RateMilliwatts >= 0 ? charging : discharging;
                g.DrawLine(pen, xa, ya, xb, yb);
            }

            using var labelBrush = new SolidBrush(SystemColors.GrayText);
            using var labelFont  = new Font(SystemFonts.DefaultFont.FontFamily, 7.5f);
            g.DrawString($"±{maxAbs / 1000.0:F1} W", labelFont, labelBrush, rect.Left + 4, rect.Top + 2);
        }
    }
}
