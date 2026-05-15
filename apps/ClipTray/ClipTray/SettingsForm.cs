using System.Diagnostics;
using System.Windows.Forms;
using WindowsTrayCore;

namespace ClipTray;

internal sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;

    // General tab
    private HotkeyCaptureBox _pickerHotkey = null!;
    private NumericUpDown    _textCapNud   = null!;
    private NumericUpDown    _imageCapNud  = null!;
    private NumericUpDown    _diskQuotaNud = null!;

    // Privacy tab
    private HotkeyCaptureBox _pauseHotkey         = null!;
    private CheckBox         _pauseOnLock         = null!;
    private CheckBox         _heuristicEnabled    = null!;
    private NumericUpDown    _heuristicMinNud     = null!;
    private NumericUpDown    _heuristicMaxNud     = null!;

    // Blocklist tab
    private ListBox _blocklistBox       = null!;
    private Button  _blocklistAddBtn    = null!;
    private Button  _blocklistRemoveBtn = null!;
    private Button  _blocklistAddFgBtn  = null!;

    // System tab
    private CheckBox      _runAtStartup   = null!;
    private NumericUpDown _startupDelayNud = null!;

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;

        Text            = "ClipTray Settings";
        ClientSize      = new System.Drawing.Size(480, 560);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;
        ShowInTaskbar   = false;

        BuildLayout();
        LoadValues();

        ThemeApplier.ApplyTo(this, TrayTheme.Current);
        ApplyHintColors();

        TrayTheme.Current.Changed += OnThemeChanged;
        Disposed += (_, _) => TrayTheme.Current.Changed -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        ThemeApplier.ApplyTo(this, TrayTheme.Current);
        ApplyHintColors();
    }

    private void ApplyHintColors()
    {
        var dim = TrayTheme.Current.ForegroundDim;
        foreach (Control c in Controls)
            ApplyHintColorsToTree(c, dim);
    }

    private static void ApplyHintColorsToTree(Control root, System.Drawing.Color dim)
    {
        if (root.Tag is string tag && tag == "hint")
            root.ForeColor = dim;
        foreach (Control child in root.Controls)
            ApplyHintColorsToTree(child, dim);
    }

    private void BuildLayout()
    {
        var tabs = new TabControl
        {
            Dock    = DockStyle.Fill,
            Padding = new System.Drawing.Point(12, 8),
        };

        var tabGeneral   = new TabPage("General");
        var tabPrivacy   = new TabPage("Privacy");
        var tabBlocklist = new TabPage("Blocklist");
        var tabSystem    = new TabPage("System");

        tabGeneral.Controls.Add(BuildGeneralPanel());
        tabPrivacy.Controls.Add(BuildPrivacyPanel());
        tabBlocklist.Controls.Add(BuildBlocklistPanel());
        tabSystem.Controls.Add(BuildSystemPanel());

        tabs.TabPages.AddRange(new[] { tabGeneral, tabPrivacy, tabBlocklist, tabSystem });

        Controls.Add(tabs);
        Controls.Add(BuildButtonRow());
    }

    private Control BuildGeneralPanel()
    {
        var grid = NewGrid();
        AddRow(grid, "Picker hotkey:",       _pickerHotkey = new HotkeyCaptureBox());
        AddRow(grid, "Text history cap:",    _textCapNud   = NewNumeric(10,  1000));
        AddRow(grid, "Image history cap:",   _imageCapNud  = NewNumeric(0,   200));
        AddRow(grid, "Disk quota (MB):",     _diskQuotaNud = NewNumeric(10,  2000));

        var hint = new Label
        {
            Text      = "Text and image caps apply independently. FIFO eviction; pinned items are exempt.",
            AutoSize  = true,
            Margin    = new Padding(0, 8, 0, 0),
            Tag       = "hint",
        };
        grid.RowCount += 1;
        grid.Controls.Add(hint, 0, grid.RowCount - 1);
        grid.SetColumnSpan(hint, 2);

        return WrapInPadding(grid);
    }

    private Control BuildPrivacyPanel()
    {
        var grid = NewGrid();
        AddRow(grid, "Pause-toggle hotkey:", _pauseHotkey = new HotkeyCaptureBox());
        AddRow(grid, "",                     _pauseOnLock      = new CheckBox { Text = "Pause capture when screen is locked", AutoSize = true });
        AddRow(grid, "",                     _heuristicEnabled = new CheckBox { Text = "Enable password heuristic",           AutoSize = true });
        AddRow(grid, "Heuristic min length:", _heuristicMinNud = NewNumeric(4,  32));
        AddRow(grid, "Heuristic max length:", _heuristicMaxNud = NewNumeric(16, 256));

        var hint = new Label
        {
            Text     = "The heuristic flags (but does not drop) text that looks like a secret.\n"
                     + "Flagged items show as •••••••• in the picker.",
            AutoSize = true,
            Margin   = new Padding(0, 8, 0, 0),
            Tag      = "hint",
        };
        grid.RowCount += 1;
        grid.Controls.Add(hint, 0, grid.RowCount - 1);
        grid.SetColumnSpan(hint, 2);

        return WrapInPadding(grid);
    }

    private Control BuildBlocklistPanel()
    {
        var outer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };

        _blocklistBox = new ListBox
        {
            Dock          = DockStyle.Fill,
            SelectionMode = SelectionMode.One,
        };

        _blocklistAddBtn = new Button
        {
            Text      = "&Add...",
            Width     = 110,
            Height    = 28,
        };
        _blocklistAddBtn.Click += OnBlocklistAdd;

        _blocklistRemoveBtn = new Button
        {
            Text      = "&Remove",
            Width     = 110,
            Height    = 28,
        };
        _blocklistRemoveBtn.Click += OnBlocklistRemove;

        _blocklistAddFgBtn = new Button
        {
            Text      = "Add &foreground app",
            Width     = 160,
            Height    = 28,
        };
        _blocklistAddFgBtn.Click += OnBlocklistAddForeground;

        var fgHint = new Label
        {
            Text     = "Alt-tab to the target app, then click 'Add foreground app'. You have 1 second.",
            AutoSize = true,
            Margin   = new Padding(0, 6, 0, 0),
            Tag      = "hint",
        };

        var buttonRow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Bottom,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize      = true,
            Padding       = new Padding(0, 8, 0, 0),
        };
        buttonRow.Controls.Add(_blocklistAddBtn);
        buttonRow.Controls.Add(_blocklistRemoveBtn);
        buttonRow.Controls.Add(_blocklistAddFgBtn);

        var hintContainer = new Panel { Dock = DockStyle.Bottom, AutoSize = true };
        hintContainer.Controls.Add(fgHint);

        outer.Controls.Add(_blocklistBox);
        outer.Controls.Add(hintContainer);
        outer.Controls.Add(buttonRow);

        return outer;
    }

    private Control BuildSystemPanel()
    {
        var stack = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents  = false,
            Padding       = new Padding(16),
        };

        _runAtStartup = new CheckBox
        {
            Text     = "Run at Windows startup",
            AutoSize = true,
        };

        var startupDelayRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize      = true,
            Margin        = new Padding(0, 8, 0, 0),
        };
        _startupDelayNud = NewNumeric(0, 300);
        startupDelayRow.Controls.Add(new Label
        {
            Text     = "Startup delay (seconds):",
            AutoSize = true,
            Margin   = new Padding(0, 6, 8, 0),
        });
        startupDelayRow.Controls.Add(_startupDelayNud);

        var dataFolderLink = new LinkLabel
        {
            Text     = "Open ClipTray data folder...",
            AutoSize = true,
            Margin   = new Padding(0, 16, 0, 4),
        };
        dataFolderLink.LinkClicked += (_, _) => OpenFolder(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipTray"));

        var logsFolderLink = new LinkLabel
        {
            Text     = "Open logs folder...",
            AutoSize = true,
            Margin   = new Padding(0, 4, 0, 0),
        };
        logsFolderLink.LinkClicked += (_, _) => OpenFolder(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipTray", "logs"));

        stack.Controls.Add(_runAtStartup);
        stack.Controls.Add(startupDelayRow);
        stack.Controls.Add(dataFolderLink);
        stack.Controls.Add(logsFolderLink);

        return stack;
    }

    private static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName       = "explorer.exe",
                Arguments      = path,
                UseShellExecute = true,
            });
        }
        catch { /* swallow */ }
    }

    private FlowLayoutPanel BuildButtonRow()
    {
        var row = new FlowLayoutPanel
        {
            Dock          = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height        = 44,
            Padding       = new Padding(8),
        };

        var save = new Button { Text = "&Save", Width = 90 };
        save.Click += (_, _) => SaveAndClose();

        var cancel = new Button { Text = "&Cancel", Width = 90 };
        cancel.Click += (_, _) => Close();

        row.Controls.Add(save);
        row.Controls.Add(cancel);

        AcceptButton = save;
        CancelButton = cancel;
        return row;
    }

    private void LoadValues()
    {
        // General
        _pickerHotkey.ModifiersValue = _settings.PickerHotkeyModifiers;
        _pickerHotkey.KeyValue       = _settings.PickerHotkeyKey;
        _textCapNud.Value            = Math.Clamp(_settings.TextHistoryCap,  10,  1000);
        _imageCapNud.Value           = Math.Clamp(_settings.ImageHistoryCap, 0,   200);
        _diskQuotaNud.Value          = Math.Clamp(_settings.DiskQuotaMb,     10,  2000);

        // Privacy
        _pauseHotkey.ModifiersValue  = _settings.PauseHotkeyModifiers;
        _pauseHotkey.KeyValue        = _settings.PauseHotkeyKey;
        _pauseOnLock.Checked         = _settings.PauseOnLockScreen;
        _heuristicEnabled.Checked    = _settings.PasswordHeuristicEnabled;
        _heuristicMinNud.Value       = Math.Clamp(_settings.PasswordHeuristicMinLength, 4,  32);
        _heuristicMaxNud.Value       = Math.Clamp(_settings.PasswordHeuristicMaxLength, 16, 256);

        // Blocklist
        _blocklistBox.Items.Clear();
        foreach (var name in _settings.ForegroundBlocklist)
            _blocklistBox.Items.Add(name);

        // System
        _runAtStartup.Checked  = _settings.RunAtStartup;
        _startupDelayNud.Value = Math.Clamp(_settings.StartupDelaySeconds, 0, 300);
    }

    private void SaveAndClose()
    {
        // General
        _settings.PickerHotkeyModifiers = _pickerHotkey.ModifiersValue;
        _settings.PickerHotkeyKey       = _pickerHotkey.KeyValue;
        _settings.TextHistoryCap        = (int)_textCapNud.Value;
        _settings.ImageHistoryCap       = (int)_imageCapNud.Value;
        _settings.DiskQuotaMb           = (int)_diskQuotaNud.Value;

        // Privacy
        _settings.PauseHotkeyModifiers       = _pauseHotkey.ModifiersValue;
        _settings.PauseHotkeyKey             = _pauseHotkey.KeyValue;
        _settings.PauseOnLockScreen          = _pauseOnLock.Checked;
        _settings.PasswordHeuristicEnabled   = _heuristicEnabled.Checked;
        _settings.PasswordHeuristicMinLength = (int)_heuristicMinNud.Value;
        _settings.PasswordHeuristicMaxLength = (int)_heuristicMaxNud.Value;

        // Blocklist
        _settings.ForegroundBlocklist.Clear();
        foreach (var item in _blocklistBox.Items)
        {
            if (item is string s)
                _settings.ForegroundBlocklist.Add(s);
        }

        // System
        _settings.RunAtStartup        = _runAtStartup.Checked;
        _settings.StartupDelaySeconds = (int)_startupDelayNud.Value;

        _settings.Save();
        Close();
    }

    private void OnBlocklistAdd(object? sender, EventArgs e)
    {
        using var dlg = new BlocklistInputDialog();
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;
        var name = dlg.EnteredName;
        if (string.IsNullOrWhiteSpace(name))
            return;
        name = name.Trim().ToLowerInvariant();
        if (!_blocklistBox.Items.Contains(name))
            _blocklistBox.Items.Add(name);
    }

    private void OnBlocklistRemove(object? sender, EventArgs e)
    {
        if (_blocklistBox.SelectedIndex < 0)
            return;
        _blocklistBox.Items.RemoveAt(_blocklistBox.SelectedIndex);
    }

    private void OnBlocklistAddForeground(object? sender, EventArgs e)
    {
        MessageBox.Show(
            this,
            "Alt-tab to the target app within 1 second.",
            "ClipTray",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        var timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            var name = ForegroundProcessProbe.GetCurrentName();
            if (string.IsNullOrWhiteSpace(name))
                return;
            if (!_blocklistBox.Items.Contains(name))
                _blocklistBox.Items.Add(name);
        };
        timer.Start();
    }

    // Layout helpers

    private static TableLayoutPanel NewGrid() => new()
    {
        Dock        = DockStyle.Fill,
        ColumnCount = 2,
        ColumnStyles =
        {
            new ColumnStyle(SizeType.Absolute, 200),
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
            Text   = label,
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
        Width   = 90,
    };

    // Small inline dialog for typing a process name into the blocklist.
    private sealed class BlocklistInputDialog : Form
    {
        private readonly TextBox _input;

        public string EnteredName => _input.Text;

        public BlocklistInputDialog()
        {
            Text            = "Add to blocklist";
            ClientSize      = new System.Drawing.Size(340, 100);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;
            ShowInTaskbar   = false;

            var label = new Label
            {
                Text     = "Process name (without .exe):",
                AutoSize = true,
                Location = new System.Drawing.Point(12, 14),
            };

            _input = new TextBox
            {
                Location  = new System.Drawing.Point(12, 36),
                Width     = 316,
                MaxLength = 260,
            };
            _input.KeyDown += (_, args) =>
            {
                if (args.KeyCode == Keys.Enter) { DialogResult = DialogResult.OK; Close(); }
                if (args.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            };

            var ok = new Button
            {
                Text          = "OK",
                DialogResult  = DialogResult.OK,
                Location      = new System.Drawing.Point(160, 66),
                Width         = 80,
            };
            var cancel = new Button
            {
                Text          = "Cancel",
                DialogResult  = DialogResult.Cancel,
                Location      = new System.Drawing.Point(248, 66),
                Width         = 80,
            };

            AcceptButton = ok;
            CancelButton = cancel;

            Controls.Add(label);
            Controls.Add(_input);
            Controls.Add(ok);
            Controls.Add(cancel);
        }
    }

    // Hotkey capture TextBox subclass.
    internal sealed class HotkeyCaptureBox : TextBox
    {
        private HotkeyModifiers _modifiers;
        private Keys            _key;

        public HotkeyModifiers ModifiersValue
        {
            get => _modifiers;
            set { _modifiers = value; UpdateText(); }
        }

        public Keys KeyValue
        {
            get => _key;
            set { _key = value; UpdateText(); }
        }

        public HotkeyCaptureBox()
        {
            ReadOnly    = true;
            Width       = 180;
            BackColor   = System.Drawing.SystemColors.Window;
            Cursor      = Cursors.Arrow;
            ShortcutsEnabled = false;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            e.SuppressKeyPress = true;

            if (e.KeyCode == Keys.Escape)
            {
                _modifiers = HotkeyModifiers.None;
                _key       = Keys.None;
                UpdateText();
                return;
            }

            // Ignore modifier-only keystrokes.
            if (IsModifierOnly(e.KeyCode))
                return;

            var mods = HotkeyModifiers.None;
            if ((e.Modifiers & Keys.Control) != 0) mods |= HotkeyModifiers.Control;
            if ((e.Modifiers & Keys.Alt)     != 0) mods |= HotkeyModifiers.Alt;
            if ((e.Modifiers & Keys.Shift)   != 0) mods |= HotkeyModifiers.Shift;

            _modifiers = mods;
            _key       = e.KeyCode;
            UpdateText();
        }

        private void UpdateText()
        {
            if (_key == Keys.None && _modifiers == HotkeyModifiers.None)
            {
                Text = "(none)";
                return;
            }

            var parts = new System.Collections.Generic.List<string>(5);
            if ((_modifiers & HotkeyModifiers.Control) != 0) parts.Add("Ctrl");
            if ((_modifiers & HotkeyModifiers.Alt)     != 0) parts.Add("Alt");
            if ((_modifiers & HotkeyModifiers.Shift)   != 0) parts.Add("Shift");
            if ((_modifiers & HotkeyModifiers.Win)     != 0) parts.Add("Win");
            if (_key != Keys.None)                            parts.Add(KeyToString(_key));
            Text = string.Join("+", parts);
        }

        private static bool IsModifierOnly(Keys key) =>
            key is Keys.ControlKey or Keys.LControlKey or Keys.RControlKey
                or Keys.ShiftKey   or Keys.LShiftKey   or Keys.RShiftKey
                or Keys.Menu       or Keys.LMenu        or Keys.RMenu
                or Keys.LWin       or Keys.RWin;

        private static string KeyToString(Keys key) =>
            key switch
            {
                Keys.D0 => "0", Keys.D1 => "1", Keys.D2 => "2",
                Keys.D3 => "3", Keys.D4 => "4", Keys.D5 => "5",
                Keys.D6 => "6", Keys.D7 => "7", Keys.D8 => "8", Keys.D9 => "9",
                _ => key.ToString(),
            };
    }
}
