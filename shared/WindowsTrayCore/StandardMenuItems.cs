using System.Diagnostics;
using System.IO;
using WindowsAppCore;

namespace WindowsTrayCore;

/// <summary>
/// Factory helpers for the three standard tray-menu items: About, Check for
/// updates, and Open logs. Apps wire these in next to their app-specific items.
/// </summary>
public static class StandardMenuItems
{
    public static ToolStripMenuItem CreateAbout(
        string appName,
        Func<string?>? extraInfoProvider = null,
        UpdateChecker? updateChecker = null,
        Icon? appIcon = null,
        string repoUrl = RepoInfo.Url,
        string releasesUrl = RepoInfo.ReleasesUrl,
        string licenseSummary = RepoInfo.LicenseSummary)
    {
        var item = new ToolStripMenuItem("&About...");
        item.Click += (_, _) =>
        {
            var info = extraInfoProvider?.Invoke();
            using var dlg = new AboutDialog(appName, info, updateChecker, appIcon, repoUrl, releasesUrl, licenseSummary);
            dlg.ShowDialog();
        };
        return item;
    }

    public static ToolStripMenuItem CreateCheckForUpdates(
        UpdateChecker updateChecker,
        TrayIcon trayIcon,
        string appName)
    {
        var item = new ToolStripMenuItem("Check for &updates");
        item.Click += async (_, _) =>
        {
            item.Enabled = false;
            try
            {
                var result = await updateChecker.CheckAsync();
                if (result.IsUpdateAvailable)
                {
                    trayIcon.ShowBalloonTip(5000, $"{appName} update available",
                        $"Version {result.LatestVersion} is available — visit GitHub to download.",
                        ToolTipIcon.Info);
                }
                else
                {
                    trayIcon.ShowBalloonTip(3000, appName,
                        "You're on the latest version.",
                        ToolTipIcon.Info);
                }
            }
            finally
            {
                item.Enabled = true;
            }
        };
        return item;
    }

    public static ToolStripMenuItem CreateOpenLogs(string appName)
    {
        var item = new ToolStripMenuItem("Open &logs folder");
        item.Click += (_, _) =>
        {
            var path = AppPaths.LogDir(appName);
            Directory.CreateDirectory(path);
            try
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch
            {
                // Shell-execute failure (no Explorer association, etc.) — let the user
                // see the path in a fallback message box rather than fail silently.
                MessageBox.Show($"Could not open folder. Path:\n{path}", appName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        return item;
    }
}
