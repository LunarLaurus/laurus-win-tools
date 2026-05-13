namespace SoundTracker.App.Audio;

internal interface IAudioSessionSource : IDisposable
{
    event EventHandler<AudioActivityEventArgs>? ActivityRecorded;

    event EventHandler? SessionsChanged;

    IReadOnlyList<string> GetActiveSessionNames();
}
