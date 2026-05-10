namespace ProgramHider;

// Captures everything Program Hider needs to restore a previously hidden
// window and present it in restore menus.
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
