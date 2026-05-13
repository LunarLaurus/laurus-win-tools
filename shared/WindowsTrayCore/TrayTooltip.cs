namespace WindowsTrayCore;

/// <summary>
/// Helpers for the 63-character WinAPI tooltip limit on NotifyIcon.
/// </summary>
public static class TrayTooltip
{
    public const int MaxLength = 63;

    public static string Truncate(string text) =>
        text.Length <= MaxLength ? text : text[..MaxLength];
}
