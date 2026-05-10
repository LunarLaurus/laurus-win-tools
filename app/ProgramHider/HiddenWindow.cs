namespace ProgramHider;

internal sealed record HiddenWindow(nint Handle, string Title, string ProcessName, bool WasMaximized);
