namespace WindowsTrayCore;

/// <summary>
/// Notification abstraction. Apps choose a backend at startup
/// (<see cref="BalloonNotifier"/> or <see cref="ToastNotifier"/>).
/// </summary>
public interface IUserNotifier : IDisposable
{
    /// <summary>Shows a non-blocking notification (balloon or toast).</summary>
    void Notify(string title, string message, NotificationLevel level = NotificationLevel.Info);

    /// <summary>Blocking modal dialog — only for fatal errors before the tray icon is visible.</summary>
    void Alert(string title, string message);

    /// <summary>Updates the tray tooltip text (enforces the 63-char WinAPI limit).</summary>
    void SetStatus(string status);

    /// <summary>Wires <see cref="Application.ThreadException"/> to show a balloon/toast and log.</summary>
    void InstallThreadExceptionHandler();
}
