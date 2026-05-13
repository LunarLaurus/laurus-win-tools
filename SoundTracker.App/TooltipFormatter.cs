namespace SoundTracker.App;

using SoundTracker.App.Audio;

internal static class TooltipFormatter
{
    private const int NotifyIconTextLimit = 63;

    public static string BuildMultiline(
        EndpointVolumeSnapshot volumeSnapshot,
        IReadOnlyList<string> activeSessions,
        IReadOnlyList<AudioActivityEvent> recentActivities)
    {
        var lines = new List<string> { AppMetadata.TooltipPrefix };
        lines.Add(BuildVolumeSummary(volumeSnapshot));

        if (activeSessions.Count > 0)
        {
            lines.Add(BuildActiveTooltipSummary(activeSessions));
        }
        else
        {
            var recentSummary = BuildRecentTooltipSummary(recentActivities, DateTimeOffset.UtcNow);
            if (recentSummary is not null)
            {
                lines.Add(recentSummary);
            }
        }

        return Truncate(string.Join(Environment.NewLine, lines));
    }

    public static string BuildActiveMenuLabel(IReadOnlyList<string> activeSessions)
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

        return "Active now: idle";
    }

    public static string BuildRecentMenuLabel(IReadOnlyList<AudioActivityEvent> recentActivities)
    {
        var recentSummary = BuildRecentMenuSummary(recentActivities, DateTimeOffset.UtcNow);
        if (recentSummary is null)
        {
            return "Recent activity will appear here";
        }

        return $"Recent: {recentSummary}";
    }

    public static string BuildVolumeMenuLabel(EndpointVolumeSnapshot snapshot)
    {
        if (!snapshot.IsAvailable)
        {
            return "Volume: unavailable";
        }

        return snapshot.IsMuted
            ? $"Volume: muted ({snapshot.Percent}%)"
            : $"Volume: {snapshot.Percent}%";
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

    private static string BuildActiveTooltipSummary(IReadOnlyList<string> activeSessions)
    {
        if (activeSessions.Count == 1)
        {
            return $"Active: {CompactSource(activeSessions[0])}";
        }

        return $"Active: {activeSessions.Count} apps";
    }

    private static string BuildVolumeSummary(EndpointVolumeSnapshot snapshot)
    {
        if (!snapshot.IsAvailable)
        {
            return "Volume unavailable";
        }

        return snapshot.IsMuted
            ? $"Muted {snapshot.Percent}%"
            : $"Volume {snapshot.Percent}%";
    }

    private static string? BuildRecentTooltipSummary(IReadOnlyList<AudioActivityEvent> recentActivities, DateTimeOffset nowUtc)
    {
        var latestActivity = GetLatestActivity(recentActivities);
        if (latestActivity is null)
        {
            return null;
        }

        var age = BuildCompactAge(latestActivity.TimestampUtc, nowUtc);
        var source = CompactSource(latestActivity.Description);

        return latestActivity.Kind switch
        {
            AudioActivityKind.ObservedActive => $"Recent: heard {source} {age}",
            AudioActivityKind.Started => $"Recent: start {source} {age}",
            AudioActivityKind.Stopped => $"Recent: stop {source} {age}",
            AudioActivityKind.DefaultDeviceChanged => $"Recent: device {age}",
            _ => $"Recent: {source} {age}",
        };
    }

    private static string? BuildRecentMenuSummary(IReadOnlyList<AudioActivityEvent> recentActivities, DateTimeOffset nowUtc)
    {
        var latestActivity = GetLatestActivity(recentActivities);
        if (latestActivity is null)
        {
            return null;
        }

        return BuildHistorySummary(latestActivity, nowUtc);
    }

    private static string CompactSource(string description)
    {
        var value = description.Trim();
        if (value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^4];
        }

        if (value.Length <= 16)
        {
            return value;
        }

        return value[..13] + "...";
    }

    private static string BuildCompactAge(DateTimeOffset timestampUtc, DateTimeOffset nowUtc)
    {
        var age = nowUtc - timestampUtc;
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (age < TimeSpan.FromMinutes(1))
        {
            return $"{Math.Max(0, (int)age.TotalSeconds)}s";
        }

        if (age < TimeSpan.FromHours(1))
        {
            return $"{(int)age.TotalMinutes}m";
        }

        if (age < TimeSpan.FromDays(1))
        {
            return $"{(int)age.TotalHours}h";
        }

        return $"{(int)age.TotalDays}d";
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
