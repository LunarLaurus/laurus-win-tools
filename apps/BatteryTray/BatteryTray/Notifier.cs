using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Toolkit.Uwp.Notifications;
using WindowsTrayCore;

namespace BatteryTray;

public enum NotificationLevel { Info, Warning, Error }

public sealed class Notifier : IDisposable
{
    private readonly TrayIcon _fallbackHost;
    private readonly bool _toastsAvailable;

    public Notifier(TrayIcon fallbackHost)
    {
        _fallbackHost = fallbackHost;

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

    public void Notify(string title, string body, NotificationLevel level)
    {
        if (_toastsAvailable && TryShowToast(title, body, level)) return;
        ShowBalloon(title, body, level);
    }

    public void ShowBalloon(string title, string body, NotificationLevel level = NotificationLevel.Info)
    {
        var icon = level switch
        {
            NotificationLevel.Error   => ToolTipIcon.Error,
            NotificationLevel.Warning => ToolTipIcon.Warning,
            _                         => ToolTipIcon.Info,
        };
        _fallbackHost.BalloonTipTitle = title;
        _fallbackHost.BalloonTipText = body;
        _fallbackHost.BalloonTipIcon = icon;
        _fallbackHost.ShowBalloonTip(8000);
    }

    private static bool TryShowToast(string title, string body, NotificationLevel level)
    {
        try
        {
            var builder = new ToastContentBuilder()
                .AddText(title)
                .AddText(body);

            if (level == NotificationLevel.Error)
            {
                builder.SetToastScenario(ToastScenario.Reminder);
            }

            builder.Show(toast =>
            {
                toast.ExpirationTime = DateTimeOffset.Now.AddMinutes(5);
                toast.Tag = "BatteryTray";
            });

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Toast send failed: {ex}");
            return false;
        }
    }

    public void Dispose()
    {
        if (_toastsAvailable)
        {
            try { ToastNotificationManagerCompat.History.RemoveGroup("BatteryTray"); }
            catch { }
        }
    }
}
