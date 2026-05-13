using FluentAssertions;
using ProgramHider;
using Xunit;

namespace ProgramHider.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Normalize_MigratesLegacyProcessRules()
    {
        var settings = new AppSettings
        {
            AutoHideProcessNames = new List<string> { "powershell", "PowerShell", "notepad" }
        };

        settings.Normalize();

        settings.WindowRules.Should().HaveCount(2, "duplicates are collapsed");
        settings.WindowRules.Should().OnlyContain(r => r.AutoHideOnMinimize);
        settings.AutoHideProcessNames.Should().BeEmpty("legacy list is cleared after migration");
    }

    [Fact]
    public void Normalize_PreservesRuleProtectedPinHash()
    {
        var expectedHash = PinSecurity.HashSecret("1234");
        var settings = new AppSettings
        {
            RequirePinToRestore = false,
            PinHash = expectedHash,
            WindowRules = new List<WindowRule>
            {
                new() { MatchProcessName = "powershell", RequirePinOnRestore = true }
            }
        };

        settings.Normalize();

        settings.PinHash.Should().Be(expectedHash);
    }
}
