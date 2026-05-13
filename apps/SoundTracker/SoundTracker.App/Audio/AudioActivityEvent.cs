namespace SoundTracker.App.Audio;

internal sealed record AudioActivityEvent(
    DateTimeOffset TimestampUtc,
    AudioActivityKind Kind,
    string Description,
    string? SessionInstanceId,
    uint? ProcessId,
    string? DeviceId,
    TimeSpan? Duration);
