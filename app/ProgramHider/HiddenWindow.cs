namespace ProgramHider;

internal sealed record HiddenWindow(
    nint Handle,
    string Title,
    string ProcessName,
    string ClassName,
    bool WasMaximized,
    DateTimeOffset HiddenAtUtc,
    bool RequirePinOnRestore,
    bool SuppressNotifications);
