using FluentAssertions;
using ProgramHider;
using Xunit;

namespace ProgramHider.Tests;

public class WindowRuleTests
{
    [Fact]
    public void Rule_MatchesProcessTitleAndClass()
    {
        var rule = new WindowRule
        {
            MatchProcessName = "pwsh",
            MatchTitleContains = "Adguard Home",
            MatchClassName = "ConsoleWindowClass"
        };
        rule.Normalize();

        var matching = new NativeWindowSnapshot(
            (nint)42, "Adguard Home - Administrator: PowerShell",
            "ConsoleWindowClass", "pwsh", 42, 0, 0, false);
        var nonMatching = matching with { ProcessName = "notepad" };

        rule.Matches(matching).Should().BeTrue();
        rule.Matches(nonMatching).Should().BeFalse();
    }

    [Fact]
    public void Evaluator_MergesFlags()
    {
        var window = new NativeWindowSnapshot((nint)7, "Adguard Home", "ConsoleWindowClass", "powershell", 7, 0, 0, false);
        var rules = new[]
        {
            new WindowRule { RuleName = "auto", MatchProcessName = "powershell", AutoHideOnMinimize = true },
            new WindowRule { RuleName = "pin", MatchTitleContains = "Adguard", AutoHideOnMinimize = false, RequirePinOnRestore = true },
            new WindowRule { RuleName = "quiet", MatchClassName = "ConsoleWindowClass", AutoHideOnMinimize = false, SuppressNotifications = true }
        };
        foreach (var r in rules) r.Normalize();

        var result = WindowRuleMatchResult.Evaluate(rules, window);

        result.AutoHideOnMinimize.Should().BeTrue();
        result.RequirePinOnRestore.Should().BeTrue();
        result.SuppressNotifications.Should().BeTrue();
        result.MatchingRules.Should().HaveCount(3);
    }
}
