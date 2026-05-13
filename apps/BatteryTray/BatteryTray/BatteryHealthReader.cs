using System.Management;
using System.Runtime.Versioning;

namespace BatteryTray;

public sealed record BatteryHealthInfo(
    string? DeviceName,
    string? Manufacturer,
    string? SerialNumber,
    string? Chemistry,
    string? ChemistrySource,         // "IOCTL" / "Win32_Battery (legacy enum)" / "(none)"
    uint?   DesignCapacityMilliwattHours,
    uint?   FullChargedCapacityMilliwattHours,
    uint?   CycleCount)
{
    public double? HealthPercent
    {
        get
        {
            if (DesignCapacityMilliwattHours is null or 0) return null;
            if (FullChargedCapacityMilliwattHours is null) return null;
            return (double)FullChargedCapacityMilliwattHours.Value
                   / DesignCapacityMilliwattHours.Value * 100.0;
        }
    }
}

[SupportedOSPlatform("windows")]
public static class BatteryHealthReader
{
    public static BatteryHealthInfo? Read()
    {
        try
        {
            // ---- Primary source: IOCTL_BATTERY_QUERY_INFORMATION ----
            // This talks to the battery driver directly and gets the firmware-reported
            // chemistry tag (LION, LiP, LFP, etc) — the authoritative source. Real
            // tools like BatteryInfoView use exactly this path.
            var ioctl = BatteryIoctlReader.Read();

            string? chemistry = ioctl?.FriendlyChemistry;
            string? chemistrySource = chemistry is not null
                ? "IOCTL_BATTERY_QUERY_INFORMATION (firmware)"
                : null;

            string? deviceName   = ioctl?.DeviceName;
            string? manufacturer = ioctl?.Manufacturer;
            uint?   designCap    = ioctl?.DesignedCapacityMilliwattHours;
            uint?   fullCap      = ioctl?.FullChargedCapacityMilliwattHours;
            uint?   cycles       = ioctl?.CycleCount;
            string? serial       = null;

            // ---- Fallback chain via WMI ----
            // Even with IOCTL succeeding, WMI gives us additional fields the IOCTL
            // path doesn't (serial number is exposed via DeviceID), so we always
            // run both and merge.
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    @"\\.\root\cimv2",
                    "SELECT Name, DeviceID, Chemistry FROM Win32_Battery");
                using var collection = searcher.Get();
                foreach (ManagementObject obj in collection)
                {
                    deviceName ??= obj["Name"] as string;
                    serial ??= obj["DeviceID"] as string;
                    if (chemistry is null)
                    {
                        chemistry = LegacyChemistryToString(obj["Chemistry"]);
                        if (chemistry is not null) chemistrySource = "Win32_Battery (legacy enum)";
                    }
                    obj.Dispose();
                    break;
                }
            }
            catch (Exception ex) { CrashLogger.Write("Win32_Battery query", ex); }

            // Capacity backups via root\wmi if IOCTL didn't get them.
            if (designCap is null || manufacturer is null)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        @"\\.\root\wmi", "SELECT * FROM BatteryStaticData");
                    using var collection = searcher.Get();
                    foreach (ManagementObject obj in collection)
                    {
                        designCap    ??= TryReadUInt32(obj, "DesignedCapacity");
                        manufacturer ??= obj["ManufactureName"] as string;
                        deviceName   ??= obj["DeviceName"] as string;
                        obj.Dispose();
                        break;
                    }
                }
                catch (Exception ex) { CrashLogger.Write("BatteryStaticData query", ex); }
            }

            if (fullCap is null)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        @"\\.\root\wmi", "SELECT * FROM BatteryFullChargedCapacity");
                    using var collection = searcher.Get();
                    foreach (ManagementObject obj in collection)
                    {
                        fullCap ??= TryReadUInt32(obj, "FullChargedCapacity");
                        obj.Dispose();
                        break;
                    }
                }
                catch (Exception ex) { CrashLogger.Write("BatteryFullChargedCapacity query", ex); }
            }

            if (cycles is null)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        @"\\.\root\wmi", "SELECT * FROM BatteryCycleCount");
                    using var collection = searcher.Get();
                    foreach (ManagementObject obj in collection)
                    {
                        cycles ??= TryReadUInt32(obj, "CycleCount");
                        obj.Dispose();
                        break;
                    }
                }
                catch { /* not exposed on this hardware; fine */ }
            }

            if (chemistry is null)
            {
                chemistry = "(unknown — battery firmware doesn't expose it)";
                chemistrySource = "(no source returned data)";
            }

            if (deviceName is null && manufacturer is null && designCap is null && fullCap is null
                && ioctl is null)
            {
                return null;
            }

            return new BatteryHealthInfo(
                Trim(deviceName), Trim(manufacturer), Trim(serial),
                chemistry, chemistrySource,
                designCap, fullCap, cycles);
        }
        catch (Exception ex)
        {
            CrashLogger.Write("BatteryHealthReader.Read", ex);
            return null;
        }
    }

    private static uint? TryReadUInt32(ManagementBaseObject obj, string field)
    {
        try
        {
            var v = obj[field];
            return v switch
            {
                null     => null,
                uint u   => u,
                int i    => i >= 0 ? (uint?)i : null,
                ulong ul => ul <= uint.MaxValue ? (uint?)ul : null,
                long l   => l >= 0 && l <= uint.MaxValue ? (uint?)l : null,
                _        => uint.TryParse(v.ToString(), out var p) ? p : null,
            };
        }
        catch { return null; }
    }

    private static string? Trim(string? value)
    {
        if (value is null) return null;
        var t = value.Trim().Trim('\0');
        return t.Length == 0 ? null : t;
    }

    private static string? LegacyChemistryToString(object? raw)
    {
        if (raw is null) return null;
        if (!ushort.TryParse(raw.ToString(), out var code)) return null;
        return code switch
        {
            1 => "Other",
            2 => "Unknown (Win32_Battery enum has no value for this chemistry)",
            3 => "Lead Acid",
            4 => "Nickel Cadmium",
            5 => "Nickel Metal Hydride",
            6 => "Lithium-ion",
            7 => "Zinc air",
            8 => "Lithium Polymer",
            _ => null,
        };
    }
}
