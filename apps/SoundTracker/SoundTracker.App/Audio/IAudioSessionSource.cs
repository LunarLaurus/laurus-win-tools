namespace SoundTracker.App.Audio;

internal interface IAudioSessionSource : IDisposable
{
    event EventHandler<AudioActivityEventArgs>? ActivityRecorded;

    event EventHandler? SessionsChanged;

    event EventHandler? VolumeStateChanged;

    IReadOnlyList<string> GetActiveSessionNames();

    IReadOnlyList<AudioActivityEvent> GetRecentActivities();

    EndpointVolumeSnapshot GetEndpointVolume();
}
