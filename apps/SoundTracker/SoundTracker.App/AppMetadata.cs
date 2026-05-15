namespace SoundTracker.App;

internal static class AppMetadata
{
    internal static string DisplayVersion => Application.ProductVersion;
    internal static string TooltipPrefix => $"SoundTracker {WindowsTrayCore.VersionFormatter.TrimSemverSuffix(Application.ProductVersion)}";
}
