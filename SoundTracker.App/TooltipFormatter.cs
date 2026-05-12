namespace SoundTracker.App;

internal static class TooltipFormatter
{
    private const int NotifyIconTextLimit = 63;

    public static string Build(IReadOnlyList<string> sessions)
    {
        if (sessions.Count == 0)
        {
            return $"{AppMetadata.TooltipPrefix}: idle";
        }

        var summary = string.Join(", ", sessions.Take(3));
        if (sessions.Count > 3)
        {
            summary = $"{summary} +{sessions.Count - 3}";
        }

        return Truncate($"{AppMetadata.TooltipPrefix}: {summary}");
    }

    public static string BuildMenuLabel(IReadOnlyList<string> sessions)
    {
        if (sessions.Count == 0)
        {
            return "No active audio sessions";
        }

        var summary = string.Join(", ", sessions.Take(8));
        if (sessions.Count > 8)
        {
            summary = $"{summary} +{sessions.Count - 8} more";
        }

        return summary;
    }

    private static string Truncate(string value)
    {
        if (value.Length <= NotifyIconTextLimit)
        {
            return value;
        }

        return $"{value[..(NotifyIconTextLimit - 3)]}...";
    }
}
