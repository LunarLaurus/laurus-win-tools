using System.Diagnostics;
using Microsoft.Toolkit.Uwp.Notifications;
using WindowsAppCore;

namespace WindowsTrayCore;

/// <summary>
/// <see cref="IUserNotifier"/> implementation that sends Windows Action Center toasts,
/// falling back to balloons when the toast infrastructure is unavailable.
/// </summary>
public sealed class ToastNotifier : IUserNotifier
{
    private readonly NotifyIcon _fallback;
    private readonly string _appTag;
    private readonly bool _toastsAvailable;

    public ToastNotifier(NotifyIcon fallback, string appTag)
    {
        _fallback = fallback;
        _appTag = appTag;

        try
        {
            _ = ToastNotificationManagerCompat.History;
            _toastsAvailable = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Toast registration failed; falling back to balloons: {ex}");
            _toastsAvailable = false;
        }
    }

    public bool ToastsAvailable => _toastsAvailable;

    public void Notify(string title, string message, NotificationLevel level = NotificationLevel.Info)
    {
        if (_toastsAvailable && TryShowToast(title, message, level))
            return;
        ShowBalloon(title, message, level);
    }

    public void Alert(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

    public void SetStatus(string status) =>
        _fallback.Text = TrayTooltip.Truncate(status);

    public void InstallThreadExceptionHandler()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            CrashSink.Write("ThreadException", "UI thread", e.Exception);
            Notify("Unexpected error", e.Exception.Message, NotificationLevel.Error);
        };
    }

    public void Dispose()
    {
        if (!_toastsAvailable) return;
        try { ToastNotificationManagerCompat.History.RemoveGroup(_appTag); }
        catch { }
    }

    private void ShowBalloon(string title, string message, NotificationLevel level)
    {
        _fallback.BalloonTipTitle = title;
        _fallback.BalloonTipText = message;
        _fallback.BalloonTipIcon = level switch
        {
            NotificationLevel.Error   => ToolTipIcon.Error,
            NotificationLevel.Warning => ToolTipIcon.Warning,
            _                         => ToolTipIcon.Info,
        };
        _fallback.ShowBalloonTip(8000);
    }

    private bool TryShowToast(string title, string message, NotificationLevel level)
    {
        try
        {
            var builder = new ToastContentBuilder()
                .AddText(title)
                .AddText(message);

            if (level == NotificationLevel.Error)
                builder.SetToastScenario(ToastScenario.Reminder);

            builder.Show(toast =>
            {
                toast.ExpirationTime = DateTimeOffset.Now.AddMinutes(5);
                toast.Tag = _appTag;
            });

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Toast send failed: {ex}");
            return false;
        }
    }
}
