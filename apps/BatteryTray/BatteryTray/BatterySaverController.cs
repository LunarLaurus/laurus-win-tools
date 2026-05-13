using System.Diagnostics;
using Windows.System.Power;

namespace BatteryTray;

/// <summary>
/// Observation-only wrapper for Windows Battery Saver state.
///
/// Microsoft deliberately does not expose a public API to enable/disable Battery
/// Saver from user-mode applications — it's a user-controlled toggle and they don't
/// want third-party apps fighting over it. We respect that:
///
///   - We OBSERVE the state via WinRT and use it to inform our own decisions
///     (e.g. suppress redundant low-battery alerts, paint a leaf on the icon).
///   - We OFFER to open the Settings page if the user wants to configure it.
///   - We DO NOT pretend to toggle it ourselves.
/// </summary>
public static class BatterySaverController
{
    /// <summary>True when Windows Battery Saver is currently on.</summary>
    public static bool IsActive()
    {
        try { return PowerManager.EnergySaverStatus == EnergySaverStatus.On; }
        catch (Exception ex)
        {
            Debug.WriteLine($"BatterySaver query failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Subscribes to OS Battery Saver state changes. The handler is invoked on the
    /// thread the underlying PowerManager event raises on — typically a thread-pool
    /// thread, so callers must marshal back to the UI thread themselves.
    /// </summary>
    public static IDisposable Subscribe(Action<bool> onStateChanged)
    {
        return new Subscription(onStateChanged);
    }

    public static void OpenBatterySaverSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:batterysaver",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            CrashLogger.Write("ms-settings:batterysaver", ex);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action<bool> _handler;

        public Subscription(Action<bool> handler)
        {
            _handler = handler;
            try { PowerManager.EnergySaverStatusChanged += OnChanged; }
            catch (Exception ex) { CrashLogger.Write("BatterySaver.Subscribe", ex); }
        }

        // EnergySaverStatusChanged passes a sender object (no useful args). The actual
        // state has to be re-read off PowerManager itself.
        private void OnChanged(object? sender, object e)
        {
            try { _handler(IsActive()); }
            catch (Exception ex) { CrashLogger.Write("BatterySaver handler", ex); }
        }

        public void Dispose()
        {
            try { PowerManager.EnergySaverStatusChanged -= OnChanged; }
            catch { /* ignore */ }
        }
    }
}
