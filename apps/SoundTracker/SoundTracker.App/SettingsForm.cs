using System.Drawing;
using WindowsTrayCore;
using RunKeyStartupRegistration = WindowsAppCore.RunKeyStartupRegistration;
using StartupRegistrationResult = WindowsAppCore.StartupRegistrationResult;

namespace SoundTracker.App;

internal sealed class SettingsForm : Form
{
    private readonly SoundTrackerConfig _config;
    private readonly RunKeyStartupRegistration _startup;

    private readonly CheckBox _runAtStartup;
    private readonly NumericUpDown _startupDelay;
    private readonly Label _settingsPathValue;

    public SettingsForm(SoundTrackerConfig config, RunKeyStartupRegistration startup)
    {
        _config = config;
        _startup = startup;

        var theme = TrayTheme.Current;
        Text = "SoundTracker Settings";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        BackColor = theme.Background;
        ForeColor = theme.Text;
        ClientSize = new Size(440, 240);

        var pad = 18;
        var labelWidth = 160;
        var inputLeft = pad + labelWidth + 8;
        var inputWidth = ClientSize.Width - inputLeft - pad;
        var rowHeight = 32;

        Label MakeLabel(string text, int y) => new()
        {
            Text = text,
            ForeColor = theme.Text,
            BackColor = theme.Background,
            Location = new Point(pad, y + 4),
            Size = new Size(labelWidth, 22),
        };

        var y = pad;

        Controls.Add(MakeLabel("Run at startup:", y));
        _runAtStartup = new CheckBox
        {
            Checked = _startup.IsRegistered,
            Location = new Point(inputLeft, y + 2),
            AutoSize = true,
            BackColor = theme.Background,
            ForeColor = theme.Text,
            FlatStyle = FlatStyle.Standard,
            Text = "Launch automatically on logon",
        };
        Controls.Add(_runAtStartup);
        y += rowHeight;

        Controls.Add(MakeLabel("Startup delay (sec):", y));
        _startupDelay = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 300,
            Value = Math.Clamp(_config.StartupDelaySeconds, 0, 300),
            Location = new Point(inputLeft, y),
            Width = 80,
            BackColor = theme.Field,
            ForeColor = theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
        };
        Controls.Add(_startupDelay);
        var delayHint = new Label
        {
            Text = "Wait this long after logon before sampling audio.",
            ForeColor = theme.TextMuted,
            BackColor = theme.Background,
            Location = new Point(inputLeft + 88, y + 4),
            Size = new Size(inputWidth - 88, 22),
        };
        Controls.Add(delayHint);
        y += rowHeight;

        Controls.Add(MakeLabel("Settings file:", y));
        _settingsPathValue = new Label
        {
            Text = _config.SettingsFilePath,
            ForeColor = theme.TextMuted,
            BackColor = theme.Background,
            Location = new Point(inputLeft, y + 4),
            Size = new Size(inputWidth, 22),
            AutoEllipsis = true,
        };
        Controls.Add(_settingsPathValue);
        y += rowHeight + 12;

        var saveButton = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.None,
            BackColor = theme.Surface,
            ForeColor = theme.Text,
            FlatStyle = FlatStyle.Flat,
            AutoSize = true,
            Location = new Point(ClientSize.Width - pad - 160, ClientSize.Height - pad - 32),
        };
        saveButton.FlatAppearance.BorderColor = theme.Accent;
        saveButton.Click += (_, _) => SaveAndClose();
        Controls.Add(saveButton);

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            BackColor = theme.Surface,
            ForeColor = theme.Text,
            FlatStyle = FlatStyle.Flat,
            AutoSize = true,
            Location = new Point(ClientSize.Width - pad - 80, ClientSize.Height - pad - 32),
        };
        cancelButton.FlatAppearance.BorderColor = theme.Accent;
        Controls.Add(cancelButton);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private void SaveAndClose()
    {
        var wantStartup = _runAtStartup.Checked;
        if (wantStartup != _startup.IsRegistered)
        {
            var result = wantStartup ? _startup.Register() : _startup.Unregister();
            if (result != StartupRegistrationResult.Success)
            {
                MessageBox.Show(this,
                    $"Failed to update startup registration: {result}",
                    "SoundTracker Settings",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
        }

        _config.RunAtStartup = wantStartup;
        _config.StartupDelaySeconds = (int)_startupDelay.Value;
        _config.Save();

        DialogResult = DialogResult.OK;
        Close();
    }
}
