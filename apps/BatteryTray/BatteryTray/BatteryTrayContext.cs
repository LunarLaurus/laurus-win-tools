using System.Diagnostics;
using System.Windows.Forms;
using WindowsAppCore;

namespace BatteryTray;

public sealed class BatteryTrayContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _safetyTimer;
    private readonly BatteryMonitor _monitor = new();
    private readonly Notifier _notifier;
    private readonly PowerEventListener _powerListener;
    private readonly RateHistory _rateHistory = new();
    private readonly SingleInstanceActivation _activation;
    private readonly ScheduledTaskStartupRegistration _startup;
    private readonly IDisposable _saverSubscription;
    private readonly ProcessPowerCoordinator _powerCoordinator = new();

    private AppSettings _settings;
    private Icon? _currentIcon;
    private SettingsForm? _settingsForm;
    private BatteryInfoForm? _batteryInfoForm;

    private int? _lastSeenPercent;
    private bool _hasNotifiedFullyCharged;
    private DateTime? _fullChargeReachedAt;
    private bool _hasNotifiedChargeLimit;
    private bool _hasAutoSwitchedForCurrentSource;
    private PowerLineStatusSource _lastPowerSource = PowerLineStatusSource.Unknown;
    private bool _monitorOn = true;

    private BatteryRenderKey? _lastRenderKey;

    private enum PowerLineStatusSource { Unknown, Ac, Battery }

    public BatteryTrayContext(AppSettings settings, SingleInstanceActivation activation, ScheduledTaskStartupRegistration startup)
    {
        _settings = settings;
        _activation = activation;
        _startup = startup;

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };
        _notifyIcon.DoubleClick += (_, _) => OpenSettings();

        _notifier = new Notifier(_notifyIcon);

        _powerListener = new PowerEventListener();
        _powerListener.PowerStateChanged += (_, _) =>
        {
            _monitor.InvalidateCache();
            Refresh();
        };
        _powerListener.MonitorPowerChanged += (_, on) =>
        {
            _monitorOn = on;
            if (on) Refresh();
        };

        _safetyTimer = new System.Windows.Forms.Timer
        {
            Interval = ClampInterval(_settings.UpdateIntervalSeconds) * 1000,
        };
        _safetyTimer.Tick += (_, _) => OnSafetyTick();
        _safetyTimer.Start();

        _saverSubscription = BatterySaverController.Subscribe(_ =>
        {
            try
            {
                if (_notifyIcon.ContextMenuStrip is { IsDisposed: false } menu)
                {
                    menu.BeginInvoke(new Action(() =>
                    {
                        _monitor.InvalidateCache();
                        Refresh();
                    }));
                }
            }
            catch (Exception ex) { CrashLogger.Write("Saver→UI marshal", ex); }
        });

        _activation.ActivationRequested += (_, _) =>
        {
            try { _notifyIcon.ContextMenuStrip?.BeginInvoke(new Action(OpenSettings)); }
            catch (Exception ex) { CrashLogger.Write("Activation→OpenSettings", ex); }
        };

        Refresh();
        ShowFirstRunWelcomeIfNeeded();
    }

    private void OnSafetyTick()
    {
        if (!_monitorOn) return;
        Refresh();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings…", null, (_, _) => OpenSettings());
        menu.Items.Add("Battery info…", null, (_, _) => OpenBatteryInfo());
        menu.Items.Add("Hide default Windows battery icon…", null, (_, _) => OpenWindowsTaskbarSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Refresh now", null, (_, _) =>
        {
            _monitor.InvalidateCache();
            Refresh();
        });
        menu.Items.Add("About", null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => ExitThread());
        return menu;
    }

    private void Refresh()
    {
        BatteryState state;
        try { state = _monitor.Read(); }
        catch (Exception ex) { CrashLogger.Write("BatteryMonitor.Read", ex); return; }

        UpdateIcon(state);
        UpdateTooltip(state);
        EvaluateNotifications(state);
        EvaluateAutoPowerPlan(state);
        _rateHistory.Record(state.ChargeRateMilliwatts, state.Percent);
    }

    private void UpdateIcon(BatteryState state)
    {
        var key = BatteryRenderKey.From(state, _settings);
        if (_lastRenderKey == key) return;
        _lastRenderKey = key;

        var newIcon = IconRenderer.Create(state, _settings);
        var old = _currentIcon;
        _notifyIcon.Icon = newIcon;
        _currentIcon = newIcon;
        if (old is not null) IconRenderer.Free(old);
    }

    private void UpdateTooltip(BatteryState state)
    {
        string status =
            !state.HasBattery   ? "On AC power (no battery detected)" :
             state.IsCharging   ? $"Charging — {state.Percent}%"      :
             state.IsOnAcPower  ? $"Plugged in — {state.Percent}%"    :
                                  $"On battery — {state.Percent}%";

        if (state.BatterySaverActive) status += "  ·  Battery Saver";

        if (_settings.ShowTimeRemainingInTooltip && state.HasBattery)
        {
            if (state.IsCharging && state.SecondsToFull is int toFull && toFull > 0)
                status += $"  ·  {TimeFormat.Duration(toFull)} to full";
            else if (!state.IsCharging && state.SecondsRemaining is int toEmpty)
                status += $"  ·  {TimeFormat.Duration(toEmpty)} remaining";
        }

        if (status.Length > 127) status = status[..127];
        _notifyIcon.Text = status;
    }

    private void EvaluateNotifications(BatteryState state)
    {
        if (!state.HasBattery) return;

        var prev = _lastSeenPercent;
        _lastSeenPercent = state.Percent;

        if (!state.IsOnAcPower)
        {
            if (_settings.NotifyOnCritical
                && state.Percent <= _settings.CriticalBatteryThreshold
                && (prev is null || prev > _settings.CriticalBatteryThreshold))
            {
                _notifier.Notify("Battery critical",
                                 $"At {state.Percent}%. Connect a charger.",
                                 NotificationLevel.Error);
            }
            else if (_settings.NotifyOnLow
                     && state.Percent <= _settings.LowBatteryThreshold
                     && (prev is null || prev > _settings.LowBatteryThreshold)
                     && !(state.BatterySaverActive && _settings.SuppressLowAlertWhenBatterySaverActive))
            {
                _notifier.Notify("Battery low",
                                 $"At {state.Percent}%.",
                                 NotificationLevel.Warning);
            }
        }

        if (state.IsOnAcPower && state.Percent >= 99)
        {
            _fullChargeReachedAt ??= DateTime.UtcNow;

            if (_settings.NotifyOnFullyCharged && !_hasNotifiedFullyCharged)
            {
                _notifier.Notify("Battery fully charged",
                                 "You can unplug the charger.",
                                 NotificationLevel.Info);
                _hasNotifiedFullyCharged = true;
            }

            if (_settings.NotifyOnChargeLimitReached && !_hasNotifiedChargeLimit
                && _fullChargeReachedAt is DateTime reachedAt
                && (DateTime.UtcNow - reachedAt).TotalMinutes >= _settings.ChargeLimitMinutesAtFull)
            {
                _notifier.Notify(
                    "Still on charger",
                    $"At 100% for {_settings.ChargeLimitMinutesAtFull} minutes. " +
                    "Lithium batteries last longer if not kept fully charged for long periods.",
                    NotificationLevel.Info);
                _hasNotifiedChargeLimit = true;
            }
        }
        else if (state.Percent < 95)
        {
            _hasNotifiedFullyCharged = false;
            _hasNotifiedChargeLimit = false;
            _fullChargeReachedAt = null;
        }
    }

    private void EvaluateAutoPowerPlan(BatteryState state)
    {
        if (!_settings.PowerPlanAutoSwitchEnabled) return;
        if (!state.HasBattery) return;

        var currentSource = state.IsOnAcPower ? PowerLineStatusSource.Ac : PowerLineStatusSource.Battery;
        if (currentSource != _lastPowerSource)
        {
            _lastPowerSource = currentSource;
            _hasAutoSwitchedForCurrentSource = false;
        }
        if (_hasAutoSwitchedForCurrentSource) return;

        Guid? target = currentSource == PowerLineStatusSource.Ac
            ? TryParseGuid(_settings.PowerPlanOnAcGuid)
            : TryParseGuid(_settings.PowerPlanOnBatteryGuid);

        if (target is Guid g)
        {
            var active = PowerPlanController.GetActive();
            if (active != g && PowerPlanController.SetActive(g))
                _hasAutoSwitchedForCurrentSource = true;
        }
        else
        {
            _hasAutoSwitchedForCurrentSource = true;
        }
    }

    private static Guid? TryParseGuid(string? s) => Guid.TryParse(s, out var g) ? g : null;

    private void ShowFirstRunWelcomeIfNeeded()
    {
        if (_settings.ShownFirstRunWelcome) return;

        _notifier.ShowBalloon(
            "BatteryTray is running",
            "I'm in your system tray. Right-click for settings, or double-click to configure.",
            NotificationLevel.Info);

        _settings.ShownFirstRunWelcome = true;
        try { _settings.Save(); } catch (Exception ex) { CrashLogger.Write("Save first-run", ex); }
    }

    private void OpenSettings()
    {
        if (_settingsForm is { IsDisposed: false }) { _settingsForm.Activate(); return; }

        _settingsForm = new SettingsForm(_settings, _startup.IsRegistered);
        _settingsForm.SettingsSaved += OnSettingsSaved;
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    private void OnSettingsSaved(object? sender, AppSettings updated)
    {
        var startupChanged = _settings.RunAtStartup != updated.RunAtStartup;
        _settings = updated;
        _safetyTimer.Interval = ClampInterval(_settings.UpdateIntervalSeconds) * 1000;
        _lastRenderKey = null;

        if (startupChanged)
        {
            var result = _settings.RunAtStartup ? _startup.Register() : _startup.Unregister();
            if (result != StartupRegistrationResult.Success)
            {
                _settings.RunAtStartup = !_settings.RunAtStartup;
                try { _settings.Save(); } catch { }

                if (result == StartupRegistrationResult.Failed)
                {
                    MessageBox.Show(
                        "Couldn't update Windows startup configuration.",
                        "Startup configuration",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
        }

        Refresh();
    }

    private void OpenBatteryInfo()
    {
        if (_batteryInfoForm is { IsDisposed: false }) { _batteryInfoForm.Activate(); return; }

        _batteryInfoForm = new BatteryInfoForm(_monitor, _rateHistory, _powerCoordinator);
        _batteryInfoForm.FormClosed += (_, _) => _batteryInfoForm = null;
        _batteryInfoForm.Show();
        _batteryInfoForm.Activate();
    }

    private static void OpenWindowsTaskbarSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = "ms-settings:taskbar", UseShellExecute = true });
        }
        catch
        {
            MessageBox.Show(
                "Open Settings → Personalization → Taskbar, expand \"Other system tray icons\", and turn off Power.",
                "Hide default battery icon",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private void ShowAbout()
    {
        var elevated = ElevationHelper.IsElevated() ? "elevated" : "user";
        var toasts = _notifier.ToastsAvailable ? "toasts" : "balloons (legacy)";
        var (currentSource, _, _) = _powerCoordinator.GetCurrent();
        MessageBox.Show(
            $"BatteryTray 1.6\nA configurable taskbar battery indicator.\n\n" +
            $"Running as: {elevated}\n" +
            $"Notifications: {toasts}\n" +
            $"Battery Saver: {(BatterySaverController.IsActive() ? "active" : "inactive")}\n" +
            $"Power sampler: {currentSource}\n" +
            $"Crash log: {CrashLogger.GetLogPath()}\n\n" +
            "Double-click the tray icon to open settings.",
            "About BatteryTray",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static int ClampInterval(int seconds) =>
        seconds < 5 ? 5 : seconds > 300 ? 300 : seconds;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _activation.Dispose();
            _saverSubscription.Dispose();
            _safetyTimer.Stop();
            _safetyTimer.Dispose();
            _powerListener.Dispose();
            _powerCoordinator.Dispose();
            _notifier.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            if (_currentIcon is not null) IconRenderer.Free(_currentIcon);
            _settingsForm?.Dispose();
            _batteryInfoForm?.Dispose();
        }
        base.Dispose(disposing);
    }

    private readonly record struct BatteryRenderKey(
        int Percent, bool Charging, bool OnAc, bool HasBattery, bool Saver,
        IconStyle Style, IconTheme Theme,
        bool Smooth, bool Dpi, bool ShowSaver,
        string Cs, string Cn, string Cl, string Cc, string Ct)
    {
        public static BatteryRenderKey From(BatteryState s, AppSettings a) => new(
            s.Percent, s.IsCharging, s.IsOnAcPower, s.HasBattery, s.BatterySaverActive,
            a.Style, a.Theme, a.SmoothColorTransitions, a.DpiAwareIcon, a.ShowBatterySaverIndicator,
            a.ColorCharging, a.ColorNormal, a.ColorLow, a.ColorCritical, a.ColorText);
    }
}
