using System.Diagnostics;
using System.Windows.Forms;
using Windows.Devices.Power;
using WinRtBatteryStatus = Windows.System.Power.BatteryStatus;

namespace BatteryTray;

public readonly record struct BatteryState(
    int Percent,
    bool IsCharging,
    bool IsOnAcPower,
    bool HasBattery,
    int? SecondsRemaining,
    int? SecondsToFull,
    int? ChargeRateMilliwatts,
    int? RemainingMilliwattHours,
    int? FullChargeMilliwattHours,
    bool BatterySaverActive);

public sealed class BatteryMonitor
{
    private readonly object _gate = new();
    private (DateTime At, Detail Value)? _detailCache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(1);

    public BatteryState Read()
    {
        var ps = SystemInformation.PowerStatus;

        var hasBattery =
            ps.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery
            && ps.BatteryChargeStatus != BatteryChargeStatus.Unknown;

        int percent;
        if (hasBattery)
        {
            var raw = ps.BatteryLifePercent;
            percent = raw is >= 0f and <= 1f ? (int)Math.Round(raw * 100f) : 100;
        }
        else
        {
            percent = 100;
        }
        if (percent < 0) percent = 0;
        if (percent > 100) percent = 100;

        var isOnAc = ps.PowerLineStatus == PowerLineStatus.Online;
        var isCharging =
            isOnAc && hasBattery && percent < 100
            && (ps.BatteryChargeStatus & BatteryChargeStatus.Charging) != 0;

        int? secondsRemaining = ps.BatteryLifeRemaining > 0 ? ps.BatteryLifeRemaining : null;

        var detail = ReadDetailCached();

        int? secondsToFull = null;
        if (isCharging
            && detail.ChargeRate is int rate && rate > 0
            && detail.Remaining is int rem
            && detail.Full is int full
            && rem < full)
        {
            var hoursToFull = (full - rem) / (double)rate;
            secondsToFull = (int)Math.Round(hoursToFull * 3600);
        }

        return new BatteryState(
            percent, isCharging, isOnAc, hasBattery,
            secondsRemaining, secondsToFull,
            detail.ChargeRate, detail.Remaining, detail.Full,
            BatterySaverController.IsActive());
    }

    public void InvalidateCache()
    {
        lock (_gate) _detailCache = null;
    }

    private Detail ReadDetailCached()
    {
        lock (_gate)
        {
            if (_detailCache is { } c && DateTime.UtcNow - c.At < CacheTtl)
                return c.Value;
        }
        var fresh = TryReadDetail();
        lock (_gate) _detailCache = (DateTime.UtcNow, fresh);
        return fresh;
    }

    internal readonly record struct Detail(int? ChargeRate, int? Remaining, int? Full);

    private static Detail TryReadDetail()
    {
        try
        {
            var battery = Battery.AggregateBattery;
            var report = battery.GetReport();

            if (report.Status == WinRtBatteryStatus.NotPresent) return default;

            return new Detail(
                report.ChargeRateInMilliwatts,
                report.RemainingCapacityInMilliwattHours,
                report.FullChargeCapacityInMilliwattHours);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WinRT Battery read failed: {ex.GetType().Name}: {ex.Message}");
            return default;
        }
    }
}
