namespace SoundTracker.App;

using SoundTracker.App.Audio;

internal static class TooltipFormatter
{
    private const int NotifyIconTextLimit = 63;

    public static string Build(
        IReadOnlyList<string> activeSessions,
        IReadOnlyList<AudioActivityEvent> recentActivities)
    {
        if (activeSessions.Count > 0)
        {
            var summary = string.Join(", ", activeSessions.Take(2));
            if (activeSessions.Count > 2)
            {
                summary = $"{summary} +{activeSessions.Count - 2}";
            }

            return Truncate($"{AppMetadata.TooltipPrefix}: active {summary}");
        }

        var latestActivity = GetLatestActivity(recentActivities);
        if (latestActivity is null)
        {
            return $"{AppMetadata.TooltipPrefix}: listening";
        }

        return Truncate($"{AppMetadata.TooltipPrefix}: {BuildHistorySummary(latestActivity, DateTimeOffset.UtcNow)}");
    }

    public static string BuildMenuLabel(
        IReadOnlyList<string> activeSessions,
        IReadOnlyList<AudioActivityEvent> recentActivities)
    {
        if (activeSessions.Count > 0)
        {
            var summary = string.Join(", ", activeSessions.Take(5));
            if (activeSessions.Count > 5)
            {
                summary = $"{summary} +{activeSessions.Count - 5} more";
            }

            return $"Active now: {summary}";
        }

        var latestActivity = GetLatestActivity(recentActivities);
        if (latestActivity is null)
        {
            return "Recent activity will appear here";
        }

        return BuildHistorySummary(latestActivity, DateTimeOffset.UtcNow);
    }

    public static string BuildHistoryRow(AudioActivityEvent activity)
    {
        var eventLabel = activity.Kind switch
        {
            AudioActivityKind.ObservedActive => "Observed active",
            AudioActivityKind.Started => "Started",
            AudioActivityKind.Stopped => "Stopped",
            AudioActivityKind.DefaultDeviceChanged => "Device changed",
            _ => activity.Kind.ToString(),
        };

        return eventLabel;
    }

    public static string BuildRelativeAge(DateTimeOffset timestampUtc, DateTimeOffset nowUtc)
    {
        var age = nowUtc - timestampUtc;
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (age < TimeSpan.FromMinutes(1))
        {
            return $"{Math.Max(0, (int)age.TotalSeconds)}s ago";
        }

        if (age < TimeSpan.FromHours(1))
        {
            return $"{(int)age.TotalMinutes}m ago";
        }

        if (age < TimeSpan.FromDays(1))
        {
            return $"{(int)age.TotalHours}h ago";
        }

        return $"{(int)age.TotalDays}d ago";
    }

    public static string BuildDuration(TimeSpan? duration)
    {
        if (duration is null)
        {
            return string.Empty;
        }

        if (duration.Value < TimeSpan.FromMinutes(1))
        {
            return $"{Math.Max(0, (int)duration.Value.TotalSeconds)}s";
        }

        if (duration.Value < TimeSpan.FromHours(1))
        {
            return $"{(int)duration.Value.TotalMinutes}m {duration.Value.Seconds}s";
        }

        return $"{(int)duration.Value.TotalHours}h {duration.Value.Minutes}m";
    }

    private static AudioActivityEvent? GetLatestActivity(IReadOnlyList<AudioActivityEvent> recentActivities)
    {
        return recentActivities
            .OrderByDescending(activity => activity.TimestampUtc)
            .FirstOrDefault();
    }

    private static string BuildHistorySummary(AudioActivityEvent activity, DateTimeOffset nowUtc)
    {
        var age = BuildRelativeAge(activity.TimestampUtc, nowUtc);
        return activity.Kind switch
        {
            AudioActivityKind.ObservedActive => $"heard {activity.Description} {age}",
            AudioActivityKind.Started => $"started {activity.Description} {age}",
            AudioActivityKind.Stopped => $"stopped {activity.Description} {age}",
            AudioActivityKind.DefaultDeviceChanged => $"device changed {age}",
            _ => $"{activity.Description} {age}",
        };
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
