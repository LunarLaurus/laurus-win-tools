namespace BatteryTray;

internal static class TimeFormat
{
    /// <summary>
    /// Formats a duration in seconds as a compact human-readable string.
    /// Returns "—" for non-positive input, ">24h" for absurdly long durations,
    /// "Xm" when under an hour, and "Xh YYm" otherwise.
    /// </summary>
    public static string Duration(int seconds)
    {
        if (seconds <= 0) return "—";
        var t = TimeSpan.FromSeconds(seconds);
        if (t.TotalHours >= 24) return ">24h";
        if (t.Hours == 0) return $"{t.Minutes}m";
        return $"{t.Hours}h {t.Minutes:D2}m";
    }
}
