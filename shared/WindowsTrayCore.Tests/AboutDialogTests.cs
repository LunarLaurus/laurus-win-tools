using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class AboutDialogTests
{
    [WindowsFact]
    public void Construct_WithMinimalArgs_TitlesCorrectly()
    {
        using var dlg = new AboutDialog("MyApp");
        dlg.Text.Should().Be("About MyApp");
    }

    [WindowsFact]
    public void Construct_WithExtraInfo_IncreasesHeight()
    {
        using var withoutExtra = new AboutDialog("MyApp");
        using var withExtra = new AboutDialog("MyApp", extraInfo: "line 1\nline 2\nline 3");
        withExtra.ClientSize.Height.Should().BeGreaterThan(withoutExtra.ClientSize.Height);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("1.0.0+abc123", "1.0.0")]
    [InlineData("1.0.0-beta.1", "1.0.0")]
    [InlineData("1.0.0-beta.1+abc", "1.0.0")]
    [InlineData("", "unknown")]
    [InlineData(null, "unknown")]
    public void TrimSemverSuffix_HandlesExpectedInputs(string? raw, string expected)
    {
        AboutDialog.TrimSemverSuffix(raw).Should().Be(expected);
    }
}
