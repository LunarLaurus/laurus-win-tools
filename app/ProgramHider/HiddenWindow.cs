namespace ProgramHider;

internal sealed record HiddenWindow(nint Handle, string Title, bool WasMaximized);
