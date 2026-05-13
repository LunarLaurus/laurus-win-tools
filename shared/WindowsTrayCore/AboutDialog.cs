using System.Diagnostics;
using System.Drawing;
using WindowsAppCore;

namespace WindowsTrayCore;

public sealed class AboutDialog : Form
{
    private readonly UpdateChecker? _updateChecker;
    private readonly Label _statusLabel;
    private readonly Button _checkUpdatesButton;

    public AboutDialog(
        string appName,
        string? extraInfo = null,
        UpdateChecker? updateChecker = null,
        string repoUrl = RepoInfo.Url,
        string licenseSummary = RepoInfo.LicenseSummary)
    {
        _updateChecker = updateChecker;

        var displayVersion = TrimSemverSuffix(Application.ProductVersion);
        var theme = TrayTheme.Current;

        Text = $"About {appName}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        BackColor = theme.Background;
        ForeColor = theme.Text;
        ClientSize = new Size(420, extraInfo is null ? 230 : 360);

        var pad = new Padding(20);
        var titleFont = new Font(Font.FontFamily, 14, FontStyle.Bold);
        var titleLabel = new Label
        {
            Text = appName,
            Font = titleFont,
            ForeColor = theme.Text,
            AutoSize = true,
            Location = new Point(pad.Left, pad.Top)
        };

        var versionLabel = new Label
        {
            Text = $"Version {displayVersion}",
            ForeColor = theme.Text,
            AutoSize = true,
            Location = new Point(pad.Left, titleLabel.Bottom + 4)
        };

        var repoLink = new LinkLabel
        {
            Text = repoUrl,
            LinkColor = theme.Accent,
            ActiveLinkColor = theme.Accent,
            VisitedLinkColor = theme.Accent,
            BackColor = theme.Background,
            AutoSize = true,
            Location = new Point(pad.Left, versionLabel.Bottom + 4)
        };
        repoLink.LinkClicked += (_, _) => OpenUrl(repoUrl);

        var licenseLabel = new Label
        {
            Text = $"License: {licenseSummary}",
            ForeColor = theme.Text,
            AutoSize = true,
            Location = new Point(pad.Left, repoLink.Bottom + 12)
        };

        int nextTop = licenseLabel.Bottom + 12;

        if (!string.IsNullOrWhiteSpace(extraInfo))
        {
            var extra = new TextBox
            {
                Text = extraInfo,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = theme.Surface,
                ForeColor = theme.Text,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(pad.Left, nextTop),
                Size = new Size(ClientSize.Width - pad.Horizontal, 120)
            };
            Controls.Add(extra);
            nextTop = extra.Bottom + 12;
        }

        _statusLabel = new Label
        {
            Text = string.Empty,
            ForeColor = theme.Text,
            AutoSize = true,
            Location = new Point(pad.Left, nextTop)
        };
        nextTop = _statusLabel.Bottom + 8;

        _checkUpdatesButton = new Button
        {
            Text = "&Check for updates",
            AutoSize = true,
            BackColor = theme.Surface,
            ForeColor = theme.Text,
            FlatStyle = FlatStyle.Flat,
            Enabled = _updateChecker != null,
            Location = new Point(pad.Left, nextTop)
        };
        _checkUpdatesButton.FlatAppearance.BorderColor = theme.Accent;
        _checkUpdatesButton.Click += async (_, _) => await CheckForUpdatesAsync();

        var closeButton = new Button
        {
            Text = "&Close",
            AutoSize = true,
            DialogResult = DialogResult.OK,
            BackColor = theme.Surface,
            ForeColor = theme.Text,
            FlatStyle = FlatStyle.Flat
        };
        closeButton.FlatAppearance.BorderColor = theme.Accent;
        closeButton.Location = new Point(ClientSize.Width - pad.Right - closeButton.PreferredSize.Width, nextTop);

        AcceptButton = closeButton;
        CancelButton = closeButton;

        Controls.Add(titleLabel);
        Controls.Add(versionLabel);
        Controls.Add(repoLink);
        Controls.Add(licenseLabel);
        Controls.Add(_statusLabel);
        Controls.Add(_checkUpdatesButton);
        Controls.Add(closeButton);
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_updateChecker is null) return;
        _checkUpdatesButton.Enabled = false;
        _statusLabel.Text = "Checking...";
        try
        {
            var result = await _updateChecker.CheckAsync();
            _statusLabel.Text = result.IsUpdateAvailable
                ? $"Update available: {result.LatestVersion} — visit GitHub to download."
                : "You're on the latest version.";
        }
        finally
        {
            _checkUpdatesButton.Enabled = true;
        }
    }

    internal static string TrimSemverSuffix(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "unknown";
        var idx = raw.IndexOfAny(new[] { '+', '-' });
        return idx < 0 ? raw : raw[..idx];
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            // Shell-execute failures (no default browser, etc.) — surfacing them
            // via a MessageBox inside an About dialog would be more annoying than
            // the silent failure. Logged elsewhere when the host wires it.
        }
    }
}
