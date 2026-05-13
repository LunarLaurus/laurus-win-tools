namespace SoundTracker.App.Audio;

internal sealed class AudioActivityEventArgs : EventArgs
{
    public AudioActivityEventArgs(AudioActivityEvent activity)
    {
        Activity = activity;
    }

    public AudioActivityEvent Activity { get; }
}
