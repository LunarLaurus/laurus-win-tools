namespace WindowsAppCore;

public sealed record UpdateCheckResult(bool IsUpdateAvailable, string? LatestVersion, string? ReleaseUrl)
{
    public static UpdateCheckResult NoUpdate { get; } = new(false, null, null);
}
