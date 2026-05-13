using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class StandardMenuItemsTests
{
    [WindowsFact]
    public void CreateAbout_HasExpectedText()
    {
        using var item = StandardMenuItems.CreateAbout("MyApp");
        item.Text.Should().Be("&About...");
    }

    [WindowsFact]
    public void CreateOpenLogs_HasExpectedText()
    {
        using var item = StandardMenuItems.CreateOpenLogs("MyApp");
        item.Text.Should().Be("Open &logs folder");
    }

    [WindowsFact]
    public void CreateCheckForUpdates_HasExpectedText()
    {
        using var trayIcon = TrayIcon.ForApp("CreateCheckForUpdates_Test");
        var http = new HttpClient();
        var checker = new WindowsAppCore.UpdateChecker(http, "1.0.0", "owner", "repo");
        using var item = StandardMenuItems.CreateCheckForUpdates(checker, trayIcon, "MyApp");
        item.Text.Should().Be("Check for &updates");
    }

    [WindowsFact]
    public void CreateAbout_DefaultsToRepoInfoConstants()
    {
        // Smoke test: helper accepts the default repo + license args without throwing.
        using var item = StandardMenuItems.CreateAbout("MyApp", extraInfoProvider: () => "diag");
        item.Should().NotBeNull();
    }
}
