using System.Diagnostics;
using System.Drawing;
using WindowsAppCore;

namespace WindowsTrayCore;

public sealed class AboutDialog : Form
{
    private readonly UpdateChecker? _updateChecker;
    private readonly Label _statusLabel;
    private readonly Button _checkUpdatesButton;
    private readonly TextBox? _extraInfoBox;
    private readonly string _appName;

    public AboutDialog(
        string appName,
        string? extraInfo = null,
        UpdateChecker? updateChecker = null,
        Icon? appIcon = null,
        string repoUrl = RepoInfo.Url,
        string releasesUrl = RepoInfo.ReleasesUrl,
        string licenseSummary = RepoInfo.LicenseSummary)
    {
        _appName = appName;
        _updateChecker = updateChecker;

        var displayVersion = TrimSemverSuffix(Application.ProductVersion);
        var theme = TrayTheme.Current;
        var hasExtra = !string.IsNullOrWhiteSpace(extraInfo);

        Text = $"About {appName}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        BackColor = theme.Background;
        ForeColor = theme.Text;
        ClientSize = new Size(460, hasExtra ? 400 : 240);

        if (appIcon is not null)
            Icon = appIcon;

        var pad = new Padding(20);
        int tab = 0;

        var iconBox = new PictureBox
        {
            Image = (appIcon ?? SystemIcons.Application).ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = theme.Background,
            Size = new Size(48, 48),
            Location = new Point(pad.Left, pad.Top),
        };

        var titleLabel = new Label
        {
            Text = appName,
            Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
            ForeColor = theme.Text,
            BackColor = theme.Background,
            AutoSize = true,
            Location = new Point(iconBox.Right + 12, pad.Top),
        };

        var versionLabel = new Label
        {
            Text = $"Version {displayVersion}",
            ForeColor = theme.Text,
            BackColor = theme.Background,
            AutoSize = true,
            Location = new Point(iconBox.Right + 12, titleLabel.Bottom + 2),
        };

        var licenseLabel = new Label
        {
            Text = $"License: {licenseSummary}",
            ForeColor = theme.TextMuted,
            BackColor = theme.Background,
            AutoSize = true,
            Location = new Point(iconBox.Right + 12, versionLabel.Bottom + 2),
        };

        int nextTop = Math.Max(iconBox.Bottom, licenseLabel.Bottom) + 16;

        if (hasExtra)
        {
            var diagHeader = new Label
            {
                Text = "Diagnostics:",
                ForeColor = theme.TextMuted,
                BackColor = theme.Background,
                AutoSize = true,
                Location = new Point(pad.Left, nextTop),
            };
            Controls.Add(diagHeader);

            var copyButton = new Button
            {
                Text = "&Copy",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = theme.Surface,
                ForeColor = theme.Text,
                TabIndex = ++tab,
                Location = new Point(ClientSize.Width - pad.Right - 60, nextTop - 4),
            };
            copyButton.FlatAppearance.BorderColor = theme.Accent;
            copyButton.Click += (_, _) =>
            {
                try { Clipboard.SetText(extraInfo!); }
                catch { }
            };
            Controls.Add(copyButton);

            nextTop = diagHeader.Bottom + 4;

            _extraInfoBox = new TextBox
            {
                Text = extraInfo,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = theme.Surface,
                ForeColor = theme.Text,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(pad.Left, nextTop),
                Size = new Size(ClientSize.Width - pad.Horizontal, 110),
                TabIndex = ++tab,
            };
            Controls.Add(_extraInfoBox);
            nextTop = _extraInfoBox.Bottom + 16;
        }

        _statusLabel = new Label
        {
            Text = string.Empty,
            ForeColor = theme.TextMuted,
            BackColor = theme.Background,
            AutoSize = true,
            Location = new Point(pad.Left, nextTop),
        };
        Controls.Add(_statusLabel);
        nextTop = _statusLabel.Bottom + 8;

        var viewSourceLink = new LinkLabel
        {
            Text = "View source on GitHub",
            LinkColor = theme.Accent,
            ActiveLinkColor = theme.Accent,
            BackColor = theme.Background,
            AutoSize = true,
            TabIndex = ++tab,
            Location = new Point(pad.Left, nextTop),
        };
        viewSourceLink.LinkClicked += (_, _) => OpenUrl(repoUrl);
        Controls.Add(viewSourceLink);

        var viewReleasesLink = new LinkLabel
        {
            Text = "Release notes",
            LinkColor = theme.Accent,
            ActiveLinkColor = theme.Accent,
            BackColor = theme.Background,
            AutoSize = true,
            TabIndex = ++tab,
            Location = new Point(viewSourceLink.Right + 16, nextTop),
        };
        viewReleasesLink.LinkClicked += (_, _) => OpenUrl(releasesUrl);
        Controls.Add(viewReleasesLink);

        nextTop = Math.Max(viewSourceLink.Bottom, viewReleasesLink.Bottom) + 16;

        _checkUpdatesButton = new Button
        {
            Text = "Check for &updates",
            AutoSize = true,
            BackColor = theme.Surface,
            ForeColor = theme.Text,
            FlatStyle = FlatStyle.Flat,
            Enabled = _updateChecker is not null,
            TabIndex = ++tab,
            Location = new Point(pad.Left, nextTop),
        };
        _checkUpdatesButton.FlatAppearance.BorderColor = theme.Accent;
        _checkUpdatesButton.Click += async (_, _) => await CheckForUpdatesAsync();
        Controls.Add(_checkUpdatesButton);

        var closeButton = new Button
        {
            Text = "&Close",
            AutoSize = true,
            DialogResult = DialogResult.OK,
            BackColor = theme.Surface,
            ForeColor = theme.Text,
            FlatStyle = FlatStyle.Flat,
            TabIndex = ++tab,
        };
        closeButton.FlatAppearance.BorderColor = theme.Accent;
        closeButton.Location = new Point(ClientSize.Width - pad.Right - closeButton.PreferredSize.Width, nextTop);
        Controls.Add(closeButton);

        AcceptButton = closeButton;
        CancelButton = closeButton;

        Controls.Add(iconBox);
        Controls.Add(titleLabel);
        Controls.Add(versionLabel);
        Controls.Add(licenseLabel);

        ClientSize = new Size(ClientSize.Width, Math.Max(ClientSize.Height, closeButton.Bottom + pad.Bottom));
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
                ? $"Update available: {result.LatestVersion}. Visit GitHub to download."
                : "You are on the latest version.";
        }
        finally
        {
            _checkUpdatesButton.Enabled = true;
        }
    }

    internal static string TrimSemverSuffix(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "unknown";
        var idx = raw.IndexOfAny(['+', '-']);
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
            // Shell-execute failures (no default browser, etc.) leave the link
            // silently inert. A MessageBox inside an About dialog would be more
            // disruptive than the failure itself.
        }
    }
}
