namespace SoundTracker.App.Audio;

internal readonly record struct EndpointVolumeSnapshot(
    int Percent,
    bool IsMuted,
    bool IsAvailable)
{
    public static EndpointVolumeSnapshot Unavailable => new(0, false, false);
}
