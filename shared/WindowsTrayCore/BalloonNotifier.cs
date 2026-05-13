using WindowsAppCore;

namespace WindowsTrayCore;

/// <summary>
/// <see cref="IUserNotifier"/> implementation backed by <see cref="NotifyIcon"/> balloons.
/// All apps baseline; use <see cref="ToastNotifier"/> for Action Center toasts.
/// </summary>
public sealed class BalloonNotifier : IUserNotifier
{
    private readonly NotifyIcon _icon;

    public BalloonNotifier(NotifyIcon icon) => _icon = icon;

    public void Notify(string title, string message, NotificationLevel level = NotificationLevel.Info)
    {
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = message;
        _icon.BalloonTipIcon = ToToolTipIcon(level);
        _icon.ShowBalloonTip(8000);
    }

    public void Alert(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

    public void SetStatus(string status) =>
        _icon.Text = TrayTooltip.Truncate(status);

    public void InstallThreadExceptionHandler()
    {
        // SetUnhandledExceptionMode must be called before any control is created
        // on the thread. In production the app does this from Main before any
        // Form / NativeWindow exists, so we expect success. In tests the call
        // order isn't guaranteed -- swallow the InvalidOperationException so
        // installing the ThreadException subscription always succeeds.
        try { Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException); }
        catch (InvalidOperationException) { }

        Application.ThreadException += (_, e) =>
        {
            CrashSink.Write("ThreadException", "UI thread", e.Exception);
            Notify("Unexpected error", e.Exception.Message, NotificationLevel.Error);
        };
    }

    public void Dispose() { }   // nothing owned beyond the shared NotifyIcon

    private static ToolTipIcon ToToolTipIcon(NotificationLevel level) => level switch
    {
        NotificationLevel.Error   => ToolTipIcon.Error,
        NotificationLevel.Warning => ToolTipIcon.Warning,
        _                         => ToolTipIcon.Info,
    };
}
