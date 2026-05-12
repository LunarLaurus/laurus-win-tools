namespace SoundTracker.App.Audio;

internal interface IAudioSessionSource : IDisposable
{
    event EventHandler? SessionsChanged;

    IReadOnlyList<string> GetActiveSessionNames();
}
