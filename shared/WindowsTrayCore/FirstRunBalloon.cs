namespace WindowsTrayCore;

/// <summary>
/// One-shot balloon notification gated by a persisted "shown already" flag.
/// Wire each app's tray context to call <see cref="ShowIfNeeded"/> at the end
/// of construction; the helper handles the no-op short-circuit and the
/// persist-the-flag callback so each caller is one line.
/// </summary>
public static class FirstRunBalloon
{
    public const int DefaultTimeoutMs = 8000;

    public static void ShowIfNeeded(
        TrayIcon icon,
        bool shownAlready,
        Action markShown,
        string appName,
        string message,
        int timeoutMs = DefaultTimeoutMs)
    {
        if (shownAlready) return;
        if (!icon.Visible) return;
        icon.ShowBalloonTip(timeoutMs, appName, message, ToolTipIcon.Info);
        markShown();
    }
}
