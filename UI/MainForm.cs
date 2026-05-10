using System.Net.NetworkInformation;
using System.Drawing;
using System.Windows.Forms;
using NetProfileSwitcher.Models;
using NetProfileSwitcher.Services;
using NetProfileSwitcher.UI.Controls;

namespace NetProfileSwitcher.UI;

public class MainForm : Form
{
    private AppConfig _cfg;
    private readonly ToolTip _tip = new() { AutoPopDelay = 8000, InitialDelay = 300, ReshowDelay = 200 };

    // Tray
    private NotifyIcon _tray = null!;
    private ContextMenuStrip _trayMenu = null!;

    // Monitor
    private System.Windows.Forms.Timer _pollTimer = null!;
    private string _lastSsid = "";

    // Controls — top bar
    private ComboBox _adapterCombo = null!;
    private Label _ssidLabel = null!;

    // Controls — profile list
    private ListBox _profileList = null!;

    // Controls — editor
    private TextBox _nameBox = null!;
    private RadioButton _rbStatic = null!, _rbDhcp = null!;
    private TextBox _ipBox = null!, _subnetBox = null!, _gatewayBox = null!;
    private TextBox _dns1Box = null!, _dns2Box = null!;
    private TextBox _ssidBox = null!;
    private ListBox _ssidList = null!;

    // Controls — bottom
    private CheckBox _chkMonitor = null!, _chkTray = null!, _chkStartup = null!;
    private Label _statusLabel = null!;

    // Controls — action
    private FlatButton _btnApply = null!;

    // Tray state
    private System.Windows.Forms.Timer? _switchingTimer;
    private int _applyInFlight;

    // Startup
    private bool _suppressInitialShow;

    public MainForm()
    {
        _cfg = ConfigStore.Load();
        InitForm();
        BuildLayout();
        InitTray();
        _chkStartup.Checked = StartupManager.IsRegistered();
        _chkStartup.CheckedChanged += OnStartupCheckChanged;
        _suppressInitialShow = _cfg.StartMinimized;
        InitMonitor();
        RefreshProfileList();
        PollSsid();
    }

    // ── Form setup ─────────────────────────────────────────────────────────

    private void InitForm()
    {
        Text = "Network Profile Switcher";
        Size = new Size(640, 580);
        MinimumSize = new Size(640, 580);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Icon = Icons.AppIcon;
    }

    // ── Layout ─────────────────────────────────────────────────────────────

    private void BuildLayout()
    {
        int W = ClientSize.Width;

        var title = MakeLabel("Network Profile Switcher", Theme.Header, Theme.Accent);
        title.SetBounds(16, 10, 400, 28);
        Controls.Add(title);

        // ── Adapter row
        var lblAdapter = MakeLabel("Adapter:", Theme.Body, Theme.Muted);
        lblAdapter.SetBounds(16, 46, 56, 20);
        Controls.Add(lblAdapter);

        _adapterCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Theme.Field, ForeColor = Theme.Text, Font = Theme.Body,
            FlatStyle = FlatStyle.Flat,
        };
        _adapterCombo.SetBounds(76, 42, 190, 24);
        _adapterCombo.Items.AddRange(NetCommands.GetAdapters().ToArray());
        _adapterCombo.SelectedItem = _cfg.SelectedAdapter;
        if (_adapterCombo.SelectedIndex < 0 && _adapterCombo.Items.Count > 0)
            _adapterCombo.SelectedIndex = 0;
        _adapterCombo.SelectedIndexChanged += (_, _) =>
        {
            _cfg.SelectedAdapter = _adapterCombo.Text;
            ConfigStore.Save(_cfg);
        };
        _tip.SetToolTip(_adapterCombo, "Choose which network adapter to configure.\nTypically \"Wi-Fi\" for wireless connections.");
        Controls.Add(_adapterCombo);

        _ssidLabel = MakeLabel("SSID: —", Theme.Body, Theme.Muted);
        _ssidLabel.SetBounds(280, 46, 340, 20);
        Controls.Add(_ssidLabel);

        var div = new Panel { BackColor = Theme.Surface2 };
        div.SetBounds(16, 72, W - 32, 1);
        Controls.Add(div);

        // ── Profile list
        var lblProfiles = MakeLabel("Profiles", Theme.BodyBold, Theme.Text);
        lblProfiles.SetBounds(16, 82, 100, 20);
        Controls.Add(lblProfiles);

        _profileList = new ListBox();
        Theme.StyleListBox(_profileList);
        _profileList.SetBounds(16, 106, 160, 320);
        _profileList.SelectedIndexChanged += (_, _) => LoadSelectedProfile();
        _tip.SetToolTip(_profileList, "Your saved network profiles.\nClick one to view/edit, then Apply to activate it.");
        Controls.Add(_profileList);

        var btnNew = new FlatButton(Theme.Surface2, Theme.AccentDim) { Text = "+ New" };
        btnNew.SetBounds(16, 430, 76, 30);
        btnNew.Click += OnNewProfile;
        _tip.SetToolTip(btnNew, "Create a blank profile.\nName it, configure settings, then Save.");
        Controls.Add(btnNew);

        var btnDup = new FlatButton(Theme.Surface2, Theme.AccentDim) { Text = "Clone" };
        btnDup.SetBounds(98, 430, 78, 30);
        btnDup.Click += OnCloneProfile;
        _tip.SetToolTip(btnDup, "Duplicate the selected profile.\nUseful for creating a variant of existing settings.");
        Controls.Add(btnDup);

        // ── Editor panel
        int ex = 192;
        int ew = W - ex - 16;

        var editorBg = new Panel { BackColor = Theme.Surface };
        editorBg.SetBounds(ex, 80, ew, 382);
        Controls.Add(editorBg);

        int row = 12, lx = 14, fx = 110, fw = 160;

        editorBg.Controls.Add(FieldLabel("Name:", lx, row));
        _nameBox = FieldTextBox(fx, row - 2, fw + 80);
        _tip.SetToolTip(_nameBox, "A friendly name for this profile (e.g. \"Home\", \"Office VPN\").");
        editorBg.Controls.Add(_nameBox);
        row += 34;

        editorBg.Controls.Add(FieldLabel("Mode:", lx, row));
        _rbStatic = new RadioButton
        {
            Text = "Static IP", AutoSize = true,
            ForeColor = Theme.Text, BackColor = Theme.Surface, Font = Theme.Body,
        };
        _rbStatic.SetBounds(fx, row - 2, 100, 22);
        _rbStatic.CheckedChanged += (_, _) => ToggleStaticFields();
        _tip.SetToolTip(_rbStatic, "Manually assign a fixed IP address, subnet, and gateway.\nUse this for networks where you have a reserved address.");
        editorBg.Controls.Add(_rbStatic);

        _rbDhcp = new RadioButton
        {
            Text = "DHCP", AutoSize = true,
            ForeColor = Theme.Text, BackColor = Theme.Surface, Font = Theme.Body,
        };
        _rbDhcp.SetBounds(fx + 110, row - 2, 80, 22);
        _tip.SetToolTip(_rbDhcp, "Get an IP address automatically from the router.\nYou can still set custom DNS servers below.");
        editorBg.Controls.Add(_rbDhcp);
        row += 32;

        editorBg.Controls.Add(FieldLabel("IP Address:", lx, row));
        _ipBox = FieldTextBox(fx, row - 2, fw);
        _tip.SetToolTip(_ipBox, "The static IPv4 address (e.g. 192.168.1.100).\nDisabled in DHCP mode.");
        editorBg.Controls.Add(_ipBox);
        row += 30;

        editorBg.Controls.Add(FieldLabel("Subnet:", lx, row));
        _subnetBox = FieldTextBox(fx, row - 2, fw);
        _subnetBox.Text = "255.255.255.0";
        _tip.SetToolTip(_subnetBox, "Subnet mask (usually 255.255.255.0 for home networks).\nDisabled in DHCP mode.");
        editorBg.Controls.Add(_subnetBox);
        row += 30;

        editorBg.Controls.Add(FieldLabel("Gateway:", lx, row));
        _gatewayBox = FieldTextBox(fx, row - 2, fw);
        _tip.SetToolTip(_gatewayBox, "Default gateway / router address (e.g. 192.168.1.1).\nDisabled in DHCP mode.");
        editorBg.Controls.Add(_gatewayBox);
        row += 34;

        var dnsHeader = MakeLabel("DNS Servers", Theme.BodyBold, Theme.Accent);
        dnsHeader.SetBounds(lx, row, 150, 18);
        editorBg.Controls.Add(dnsHeader);
        row += 22;

        editorBg.Controls.Add(FieldLabel("Primary:", lx, row));
        _dns1Box = FieldTextBox(fx, row - 2, fw);
        _tip.SetToolTip(_dns1Box, "Primary DNS server.\nPopular choices: 1.1.1.1 (Cloudflare), 8.8.8.8 (Google), 9.9.9.9 (Quad9).");
        editorBg.Controls.Add(_dns1Box);
        row += 30;

        editorBg.Controls.Add(FieldLabel("Secondary:", lx, row));
        _dns2Box = FieldTextBox(fx, row - 2, fw);
        _tip.SetToolTip(_dns2Box, "Fallback DNS server, used if the primary is unreachable.");
        editorBg.Controls.Add(_dns2Box);
        row += 36;

        var ssidHeader = MakeLabel("Auto-Switch SSIDs", Theme.BodyBold, Theme.Accent);
        ssidHeader.SetBounds(lx, row, 200, 18);
        editorBg.Controls.Add(ssidHeader);

        var ssidHelp = MakeLabel("?", Theme.BodyBold, Theme.Muted);
        ssidHelp.SetBounds(lx + 148, row, 16, 18);
        ssidHelp.Cursor = Cursors.Help;
        _tip.SetToolTip(ssidHelp,
            "Link Wi-Fi network names (SSIDs) to this profile.\n" +
            "When monitoring is enabled and you connect to a\n" +
            "listed SSID, this profile is applied automatically.");
        editorBg.Controls.Add(ssidHelp);
        row += 22;

        _ssidList = new ListBox
        {
            BackColor = Theme.Field, ForeColor = Theme.Text,
            Font = Theme.Small, BorderStyle = BorderStyle.None,
        };
        _ssidList.SetBounds(lx, row, 200, 52);
        _tip.SetToolTip(_ssidList, "SSIDs linked to this profile.\nDouble-click an entry to remove it.");
        _ssidList.DoubleClick += (_, _) =>
        {
            if (_ssidList.SelectedItem != null)
                _ssidList.Items.Remove(_ssidList.SelectedItem);
        };
        editorBg.Controls.Add(_ssidList);

        _ssidBox = FieldTextBox(220, row, 100);
        _ssidBox.Height = 24;
        _tip.SetToolTip(_ssidBox, "Type an SSID name and press Enter or click +.\nOr click [Current] to add the SSID you're on now.");
        _ssidBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { AddSsidFromBox(); e.SuppressKeyPress = true; } };
        editorBg.Controls.Add(_ssidBox);

        var btnAddSsid = new FlatButton(Theme.AccentDim, Theme.Accent) { Text = "+" };
        btnAddSsid.SetBounds(324, row, 30, 24);
        btnAddSsid.Click += (_, _) => AddSsidFromBox();
        _tip.SetToolTip(btnAddSsid, "Add the typed SSID to the list.");
        editorBg.Controls.Add(btnAddSsid);

        var btnCurSsid = new FlatButton(Theme.Surface2, Theme.AccentDim) { Text = "Current" };
        btnCurSsid.SetBounds(220, row + 28, 80, 24);
        btnCurSsid.Click += (_, _) =>
        {
            string ssid = NetCommands.GetCurrentSsid();
            if (!string.IsNullOrEmpty(ssid) && !_ssidList.Items.Contains(ssid))
                _ssidList.Items.Add(ssid);
        };
        _tip.SetToolTip(btnCurSsid, "Add whichever SSID you're currently connected to.");
        editorBg.Controls.Add(btnCurSsid);

        // ── Action buttons
        var btnSave = new FlatButton(Theme.Accent, Theme.AccentDim) { Text = "Save" };
        btnSave.SetBounds(ex, 466, 90, 32);
        btnSave.Click += OnSaveProfile;
        _tip.SetToolTip(btnSave, "Save changes to this profile (settings + SSID links).");
        Controls.Add(btnSave);

        var btnDelete = new FlatButton(Theme.Red, Color.FromArgb(180, 60, 70)) { Text = "Delete" };
        btnDelete.SetBounds(ex + 96, 466, 80, 32);
        btnDelete.Click += OnDeleteProfile;
        _tip.SetToolTip(btnDelete, "Permanently remove this profile.");
        Controls.Add(btnDelete);

        _btnApply = new FlatButton(Theme.Green, Color.FromArgb(60, 170, 100)) { Text = "▶  Apply Now" };
        _btnApply.SetBounds(W - 148, 466, 132, 32);
        _btnApply.Click += OnApplyProfile;
        _tip.SetToolTip(_btnApply, "Immediately apply this profile's settings to the\nselected adapter using netsh.");
        Controls.Add(_btnApply);

        // ── Bottom bar
        var div2 = new Panel { BackColor = Theme.Surface2 };
        div2.SetBounds(16, 506, W - 32, 1);
        Controls.Add(div2);

        _chkMonitor = new CheckBox
        {
            Text = "Monitor SSID changes", Checked = _cfg.MonitorEnabled,
            ForeColor = Theme.Text, BackColor = Theme.Bg, Font = Theme.Body, AutoSize = true,
        };
        _chkMonitor.SetBounds(16, 512, 200, 22);
        _chkMonitor.CheckedChanged += (_, _) => { _cfg.MonitorEnabled = _chkMonitor.Checked; ConfigStore.Save(_cfg); SyncTrayState(); };
        _tip.SetToolTip(_chkMonitor, "When enabled, the app polls your Wi-Fi SSID every few seconds\nand auto-applies a matching profile if one is linked.");
        Controls.Add(_chkMonitor);

        _chkTray = new CheckBox
        {
            Text = "Minimize to tray", Checked = _cfg.MinimizeToTray,
            ForeColor = Theme.Text, BackColor = Theme.Bg, Font = Theme.Body, AutoSize = true,
        };
        _chkTray.SetBounds(196, 512, 140, 22);
        _chkTray.CheckedChanged += (_, _) => { _cfg.MinimizeToTray = _chkTray.Checked; ConfigStore.Save(_cfg); };
        _tip.SetToolTip(_chkTray, "When checked, closing the window hides to the system tray\ninstead of exiting. Right-click the tray icon to quit.");
        Controls.Add(_chkTray);

        _chkStartup = new CheckBox
        {
            Text = "Run on startup", Checked = false,
            ForeColor = Theme.Text, BackColor = Theme.Bg, Font = Theme.Body, AutoSize = true,
        };
        _chkStartup.SetBounds(356, 512, 140, 22);
        // Handler wired in constructor after InitTray, so _tray exists when SetStatus runs
        _tip.SetToolTip(_chkStartup, "Register a Windows Task Scheduler logon task so the app\nstarts automatically at login with administrator privileges.");
        Controls.Add(_chkStartup);

        _statusLabel = MakeLabel("Ready", Theme.Small, Theme.Muted);
        _statusLabel.SetBounds(16, 540, W - 32, 18);
        _statusLabel.AutoSize = false;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        Controls.Add(_statusLabel);
    }

    // ── Control factories ──────────────────────────────────────────────────

    private static Label MakeLabel(string text, Font font, Color color) =>
        new() { Text = text, Font = font, ForeColor = color, AutoSize = true, BackColor = Color.Transparent };

    private Label FieldLabel(string text, int x, int y)
    {
        var lbl = new Label
        {
            Text = text, Font = Theme.Body, ForeColor = Theme.Muted,
            BackColor = Theme.Surface, TextAlign = ContentAlignment.MiddleRight,
        };
        lbl.SetBounds(x, y, 90, 20);
        return lbl;
    }

    private static TextBox FieldTextBox(int x, int y, int w)
    {
        var tb = new TextBox();
        Theme.StyleTextBox(tb);
        tb.SetBounds(x, y, w, 24);
        return tb;
    }

    // ── Tray icon ──────────────────────────────────────────────────────────

    private void InitTray()
    {
        _trayMenu = new ContextMenuStrip
        {
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
        };
        _trayMenu.Opening += (_, _) => RebuildTrayMenu();

        _tray = new NotifyIcon
        {
            Text = "Network Profile Switcher",
            Icon = Icons.GetTrayIcon(TrayState.Idle),
            ContextMenuStrip = _trayMenu,
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); };
    }

    private void RebuildTrayMenu()
    {
        _trayMenu.Items.Clear();
        _trayMenu.Items.Add($"SSID: {(_lastSsid == "" ? "—" : _lastSsid)}").Enabled = false;
        _trayMenu.Items.Add(new ToolStripSeparator());

        foreach (var p in _cfg.Profiles)
        {
            var item = _trayMenu.Items.Add($"▶  {p.Name}");
            item.Click += async (_, _) =>
            {
                if (System.Threading.Interlocked.Exchange(ref _applyInFlight, 1) == 1) return;
                string adapter = _adapterCombo.Text;
                try
                {
                    var (ok, msg) = await Task.Run(() => NetCommands.Apply(adapter, p));
                    SetTrayState(ok ? TrayState.MatchedProfile : TrayState.Error);
                    SetStatus(ok ? $"✓  {p.Name} applied" : $"✗  Error: {msg}");
                }
                finally
                {
                    System.Threading.Interlocked.Exchange(ref _applyInFlight, 0);
                }
            };
        }

        _trayMenu.Items.Add(new ToolStripSeparator());

        var mon = new ToolStripMenuItem("Monitor SSID changes") { Checked = _cfg.MonitorEnabled, CheckOnClick = true };
        mon.Click += (_, _) => { _cfg.MonitorEnabled = mon.Checked; _chkMonitor.Checked = mon.Checked; ConfigStore.Save(_cfg); };
        _trayMenu.Items.Add(mon);

        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("Show Window").Click += (_, _) => { Show(); WindowState = FormWindowState.Normal; };
        _trayMenu.Items.Add("Quit").Click += (_, _) => { _tray.Visible = false; Application.Exit(); };
    }

    // ── Tray state ─────────────────────────────────────────────────────────

    private void SetTrayState(TrayState state)
    {
        _switchingTimer?.Stop();
        _switchingTimer = null;
        _tray.Icon = Icons.GetTrayIcon(state);

        if (state != TrayState.Switching) return;

        _switchingTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _switchingTimer.Tick += (_, _) =>
        {
            _switchingTimer!.Stop();
            _switchingTimer = null;
            bool matched = !string.IsNullOrEmpty(_lastSsid) &&
                           _cfg.Profiles.Any(p =>
                               p.LinkedSsids.Any(s => s.Equals(_lastSsid, StringComparison.OrdinalIgnoreCase)));
            SetTrayState(matched ? TrayState.MatchedProfile : TrayState.Idle);
        };
        _switchingTimer.Start();
    }

    // Evaluate and apply the correct tray state for the current SSID + monitor settings
    // without triggering a netsh apply. Call whenever either value changes.
    private void SyncTrayState()
    {
        if (!_cfg.MonitorEnabled || string.IsNullOrEmpty(_lastSsid))
        {
            SetTrayState(TrayState.Idle);
            return;
        }
        bool matched = _cfg.Profiles.Any(p =>
            p.LinkedSsids.Any(s => s.Equals(_lastSsid, StringComparison.OrdinalIgnoreCase)));
        SetTrayState(matched ? TrayState.MatchedProfile : TrayState.Idle);
    }

    // ── SSID Monitor ───────────────────────────────────────────────────────

    private void InitMonitor()
    {
        _pollTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _pollTimer.Tick += (_, _) => PollSsid();
        _pollTimer.Start();

        NetworkChange.NetworkAddressChanged += (_, _) =>
        {
            if (InvokeRequired) BeginInvoke(PollSsid);
            else PollSsid();
        };
    }

    private void PollSsid()
    {
        string ssid = NetCommands.GetCurrentSsid();
        _ssidLabel.Text = string.IsNullOrEmpty(ssid) ? "SSID: — (not connected)" : $"SSID: {ssid}";

        if (ssid == _lastSsid) return;

        _lastSsid = ssid;
        _tray.Text = BuildTrayTooltip();

        if (!string.IsNullOrEmpty(ssid) && _cfg.MonitorEnabled)
        {
            TryAutoSwitch(ssid);
        }
        else
        {
            SyncTrayState();
        }
    }

    private async void TryAutoSwitch(string ssid)
    {
        var match = _cfg.Profiles.FirstOrDefault(p =>
            p.LinkedSsids.Any(s => s.Equals(ssid, StringComparison.OrdinalIgnoreCase)));

        if (match == null)
        {
            SetTrayState(TrayState.Idle);
            return;
        }

        if (System.Threading.Interlocked.Exchange(ref _applyInFlight, 1) == 1) return;

        string adapter = _adapterCombo.Text;
        try
        {
            var (ok, msg) = await Task.Run(() => NetCommands.Apply(adapter, match));
            SetTrayState(ok ? TrayState.Switching : TrayState.Error);
            SetStatus(ok
                ? $"✓  Auto-applied \"{match.Name}\" for SSID \"{ssid}\""
                : $"✗  Auto-apply failed: {msg}");
            _tray.BalloonTipTitle = "Profile Switched";
            _tray.BalloonTipText = ok ? $"Applied \"{match.Name}\"" : $"Failed: {msg}";
            _tray.BalloonTipIcon = ok ? ToolTipIcon.Info : ToolTipIcon.Error;
            _tray.ShowBalloonTip(3000);
        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref _applyInFlight, 0);
        }
    }

    // ── Profile list ───────────────────────────────────────────────────────

    private void RefreshProfileList()
    {
        string sel = _profileList.SelectedItem?.ToString() ?? "";
        _profileList.Items.Clear();
        foreach (var p in _cfg.Profiles)
            _profileList.Items.Add(p.Name);
        if (_profileList.Items.Contains(sel))
            _profileList.SelectedItem = sel;
        else if (_profileList.Items.Count > 0)
            _profileList.SelectedIndex = 0;
    }

    private NetworkProfile? SelectedProfile()
    {
        string? name = _profileList.SelectedItem?.ToString();
        return name == null ? null : _cfg.Profiles.FirstOrDefault(p => p.Name == name);
    }

    private void LoadSelectedProfile()
    {
        var p = SelectedProfile();
        if (p == null) return;

        _nameBox.Text = p.Name;
        _rbDhcp.Checked = p.UseDhcp;
        _rbStatic.Checked = !p.UseDhcp;
        _ipBox.Text = p.Ip;
        _subnetBox.Text = p.Subnet;
        _gatewayBox.Text = p.Gateway;
        _dns1Box.Text = p.Dns1;
        _dns2Box.Text = p.Dns2;

        _ssidList.Items.Clear();
        foreach (var s in p.LinkedSsids) _ssidList.Items.Add(s);

        ToggleStaticFields();
    }

    private void ToggleStaticFields()
    {
        bool isStatic = _rbStatic.Checked;
        _ipBox.Enabled = isStatic;
        _subnetBox.Enabled = isStatic;
        _gatewayBox.Enabled = isStatic;

        _ipBox.BackColor = isStatic ? Theme.Field : Theme.Surface;
        _subnetBox.BackColor = isStatic ? Theme.Field : Theme.Surface;
        _gatewayBox.BackColor = isStatic ? Theme.Field : Theme.Surface;
    }

    private void AddSsidFromBox()
    {
        string s = _ssidBox.Text.Trim();
        if (!string.IsNullOrEmpty(s) && !_ssidList.Items.Contains(s))
        {
            _ssidList.Items.Add(s);
            _ssidBox.Clear();
        }
    }

    // ── Actions ────────────────────────────────────────────────────────────

    private void OnSaveProfile(object? sender, EventArgs e)
    {
        string name = _nameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) { SetStatus("Enter a profile name."); return; }

        // Use the selected profile as identity; fall back to name-match for new profiles
        var existing = SelectedProfile()
                       ?? _cfg.Profiles.FirstOrDefault(p => p.Name == name)
                       ?? new NetworkProfile();
        if (!_cfg.Profiles.Contains(existing))
            _cfg.Profiles.Add(existing);

        existing.Name = name;
        existing.UseDhcp = _rbDhcp.Checked;
        existing.Ip = _ipBox.Text.Trim();
        existing.Subnet = _subnetBox.Text.Trim();
        existing.Gateway = _gatewayBox.Text.Trim();
        existing.Dns1 = _dns1Box.Text.Trim();
        existing.Dns2 = _dns2Box.Text.Trim();
        existing.LinkedSsids = _ssidList.Items.Cast<string>().ToList();

        ConfigStore.Save(_cfg);
        RefreshProfileList();
        _profileList.SelectedItem = name;
        SetStatus($"Profile \"{name}\" saved.");
    }

    private void OnDeleteProfile(object? sender, EventArgs e)
    {
        var p = SelectedProfile();
        if (p == null) return;
        if (MessageBox.Show($"Delete \"{p.Name}\"?", "Confirm", MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning) != DialogResult.Yes) return;
        _cfg.Profiles.Remove(p);
        ConfigStore.Save(_cfg);
        RefreshProfileList();
        SetStatus($"Deleted \"{p.Name}\".");
    }

    private void OnNewProfile(object? sender, EventArgs e)
    {
        string name = $"New Profile {_cfg.Profiles.Count + 1}";
        _cfg.Profiles.Add(new NetworkProfile { Name = name, UseDhcp = true });
        ConfigStore.Save(_cfg);
        RefreshProfileList();
        _profileList.SelectedItem = name;
        _nameBox.SelectAll();
        _nameBox.Focus();
        SetStatus("New profile created — edit and Save.");
    }

    private void OnCloneProfile(object? sender, EventArgs e)
    {
        var src = SelectedProfile();
        if (src == null) return;
        var clone = new NetworkProfile
        {
            Name = src.Name + " (copy)",
            UseDhcp = src.UseDhcp, Ip = src.Ip, Subnet = src.Subnet,
            Gateway = src.Gateway, Dns1 = src.Dns1, Dns2 = src.Dns2,
            LinkedSsids = new List<string>(src.LinkedSsids),
        };
        _cfg.Profiles.Add(clone);
        ConfigStore.Save(_cfg);
        RefreshProfileList();
        _profileList.SelectedItem = clone.Name;
        SetStatus($"Cloned \"{src.Name}\".");
    }

    private void OnStartupCheckChanged(object? sender, EventArgs e)
    {
        string exePath = Application.ExecutablePath;
        var (ok, msg) = _chkStartup.Checked
            ? StartupManager.Register(exePath)
            : StartupManager.Unregister();

        if (ok)
        {
            _cfg.RunOnStartup = _chkStartup.Checked;
            ConfigStore.Save(_cfg);
        }
        else
        {
            _chkStartup.CheckedChanged -= OnStartupCheckChanged;
            _chkStartup.Checked = StartupManager.IsRegistered();
            _chkStartup.CheckedChanged += OnStartupCheckChanged;
        }
        SetStatus(ok ? $"✓  {msg}" : $"✗  {msg}");
    }

    private async void OnApplyProfile(object? sender, EventArgs e)
    {
        var p = SelectedProfile();
        if (p == null) { SetStatus("Select a profile first."); return; }

        if (System.Threading.Interlocked.Exchange(ref _applyInFlight, 1) == 1)
        {
            SetStatus("Apply already in progress…");
            return;
        }

        _btnApply.Enabled = false;
        SetStatus($"Applying \"{p.Name}\"…");

        string adapter = _adapterCombo.Text;
        try
        {
            var (ok, msg) = await Task.Run(() => NetCommands.Apply(adapter, p));
            SetTrayState(ok ? TrayState.MatchedProfile : TrayState.Error);
            SetStatus(ok ? $"✓  \"{p.Name}\" applied to {adapter}" : $"✗  {msg}");
            if (!ok)
                MessageBox.Show(msg, "netsh error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref _applyInFlight, 0);
            _btnApply.Enabled = true;
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void SetStatus(string msg)
    {
        _statusLabel.Text = msg;
        _tray.Text = BuildTrayTooltip();
    }

    private string BuildTrayTooltip()
    {
        string ssid = string.IsNullOrEmpty(_lastSsid) ? "not connected" : _lastSsid;
        var matched = _cfg.Profiles.FirstOrDefault(p =>
            p.LinkedSsids.Any(s => s.Equals(_lastSsid, StringComparison.OrdinalIgnoreCase)));
        string summary = matched != null
            ? $"{matched.Name} · {ssid}"
            : $"No profile · {ssid}";
        return summary.Length <= 63 ? summary : TruncateAtWord(summary, 63);
    }

    private static string TruncateAtWord(string text, int maxLen)
    {
        int cut = text.LastIndexOf(' ', maxLen - 1);
        return cut > 0 ? text[..cut] + "…" : text[..maxLen];
    }

    // ── Window chrome ──────────────────────────────────────────────────────

    protected override void SetVisibleCore(bool value)
    {
        if (_suppressInitialShow)
        {
            _suppressInitialShow = false;
            base.SetVisibleCore(false);
            _tray.BalloonTipTitle = "NetProfileSwitcher";
            _tray.BalloonTipText = "Running in the background. Right-click the tray icon to manage profiles.";
            _tray.BalloonTipIcon = ToolTipIcon.Info;
            _tray.ShowBalloonTip(3000);
            return;
        }
        base.SetVisibleCore(value);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && _cfg.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            _tray.BalloonTipTitle = "Still running";
            _tray.BalloonTipText = "Right-click the tray icon to quit or switch profiles.";
            _tray.BalloonTipIcon = ToolTipIcon.Info;
            _tray.ShowBalloonTip(2000);
        }
        else
        {
            _tray.Visible = false;
        }
        base.OnFormClosing(e);
    }

    protected override void OnResize(EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized && _cfg.MinimizeToTray)
            Hide();
        base.OnResize(e);
    }
}
