namespace ProgramHider;

internal sealed class WindowRule
{
    public string RuleName { get; set; } = "New rule";
    public string MatchProcessName { get; set; } = string.Empty;
    public string MatchTitleContains { get; set; } = string.Empty;
    public string MatchClassName { get; set; } = string.Empty;
    public bool AutoHideOnMinimize { get; set; } = true;
    public bool RequirePinOnRestore { get; set; }
    public bool SuppressNotifications { get; set; }

    public bool HasAnyMatchField =>
        !string.IsNullOrWhiteSpace(MatchProcessName) ||
        !string.IsNullOrWhiteSpace(MatchTitleContains) ||
        !string.IsNullOrWhiteSpace(MatchClassName);

    public WindowRule Clone()
    {
        return new WindowRule
        {
            RuleName = RuleName,
            MatchProcessName = MatchProcessName,
            MatchTitleContains = MatchTitleContains,
            MatchClassName = MatchClassName,
            AutoHideOnMinimize = AutoHideOnMinimize,
            RequirePinOnRestore = RequirePinOnRestore,
            SuppressNotifications = SuppressNotifications
        };
    }

    public void Normalize()
    {
        RuleName = RuleName?.Trim() ?? string.Empty;
        MatchProcessName = MatchProcessName?.Trim() ?? string.Empty;
        MatchTitleContains = MatchTitleContains?.Trim() ?? string.Empty;
        MatchClassName = MatchClassName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(RuleName))
        {
            RuleName = BuildDefaultName();
        }
    }

    public bool Matches(NativeWindowSnapshot window)
    {
        if (!HasAnyMatchField)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(MatchProcessName) &&
            !string.Equals(window.ProcessName, MatchProcessName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(MatchTitleContains) &&
            window.Title.IndexOf(MatchTitleContains, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(MatchClassName) &&
            !string.Equals(window.ClassName, MatchClassName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    public string DescribeMatch()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(MatchProcessName))
        {
            parts.Add($"process={MatchProcessName}");
        }

        if (!string.IsNullOrWhiteSpace(MatchTitleContains))
        {
            parts.Add($"title~{MatchTitleContains}");
        }

        if (!string.IsNullOrWhiteSpace(MatchClassName))
        {
            parts.Add($"class={MatchClassName}");
        }

        return parts.Count == 0 ? "No match fields" : string.Join(", ", parts);
    }

    public string DescribeBehavior()
    {
        var parts = new List<string>();
        if (AutoHideOnMinimize)
        {
            parts.Add("auto-hide");
        }

        if (RequirePinOnRestore)
        {
            parts.Add("pin restore");
        }

        if (SuppressNotifications)
        {
            parts.Add("quiet");
        }

        return parts.Count == 0 ? "manual only" : string.Join(", ", parts);
    }

    public string GetIdentityKey()
    {
        return $"{MatchProcessName}\u001f{MatchTitleContains}\u001f{MatchClassName}\u001f{AutoHideOnMinimize}\u001f{RequirePinOnRestore}\u001f{SuppressNotifications}";
    }

    private string BuildDefaultName()
    {
        if (!string.IsNullOrWhiteSpace(MatchProcessName))
        {
            return $"{MatchProcessName} rule";
        }

        if (!string.IsNullOrWhiteSpace(MatchTitleContains))
        {
            return $"Title contains '{MatchTitleContains}'";
        }

        if (!string.IsNullOrWhiteSpace(MatchClassName))
        {
            return $"{MatchClassName} class rule";
        }

        return "Window rule";
    }
}

internal readonly record struct WindowRuleMatchResult(
    bool AutoHideOnMinimize,
    bool RequirePinOnRestore,
    bool SuppressNotifications,
    IReadOnlyList<WindowRule> MatchingRules)
{
    public static readonly WindowRuleMatchResult None = new(false, false, false, Array.Empty<WindowRule>());

    public bool HasMatches => MatchingRules.Count > 0;

    public static WindowRuleMatchResult Evaluate(IEnumerable<WindowRule> rules, NativeWindowSnapshot window)
    {
        var matches = rules
            .Where(rule => rule.Matches(window))
            .Select(rule => rule.Clone())
            .ToArray();

        if (matches.Length == 0)
        {
            return None;
        }

        return new WindowRuleMatchResult(
            matches.Any(rule => rule.AutoHideOnMinimize),
            matches.Any(rule => rule.RequirePinOnRestore),
            matches.Any(rule => rule.SuppressNotifications),
            matches);
    }
}
