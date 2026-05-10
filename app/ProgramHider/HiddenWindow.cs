namespace ProgramHider;

internal sealed record HiddenWindow(
    nint Handle,
    string Title,
    string ProcessName,
    string ClassName,
    bool WasMaximized,
    DateTimeOffset HiddenAtUtc,
    NativeMethods.WindowPlacement? SavedPlacement,
    string MonitorDeviceName,
    bool RequirePinOnRestore,
    bool SuppressNotifications);
