using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace BatteryTray;

/// <summary>
/// Reads battery info via the Setup API + IOCTL_BATTERY_QUERY_INFORMATION path,
/// which is what every reliable Windows battery tool uses (BatteryInfoView,
/// HWiNFO, the Settings app, etc).
///
/// Why not just WMI? Because the chemistry field on Win32_Battery is an enum
/// from the original WMI 2.0 schema (ca. 1999) with 8 values topping out at
/// "Lithium Polymer". It hasn't been extended. Modern firmware reports specific
/// chemistries that don't fit any enum value, so Windows writes back code 2
/// ("Unknown") on the WMI surface.
///
/// IOCTL_BATTERY_QUERY_INFORMATION asks the battery driver directly. The
/// BatteryDeviceName/Manufacture/Chemistry info levels return the strings the
/// firmware actually populates — same as ACPI _BIX. This is the truth path.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class BatteryIoctlReader
{
    // setupapi/cfgmgr GUIDs for power device class.
    private static readonly Guid GUID_DEVCLASS_BATTERY = new("72631e54-78a4-11d0-bcf7-00aa00b7b32a");

    private const int DIGCF_PRESENT       = 0x00000002;
    private const int DIGCF_DEVICEINTERFACE = 0x00000010;

    private const uint GENERIC_READ  = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ  = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;

    private const uint IOCTL_BATTERY_QUERY_TAG         = 0x00294040;
    private const uint IOCTL_BATTERY_QUERY_INFORMATION = 0x00294044;

    private enum BATTERY_QUERY_INFORMATION_LEVEL
    {
        BatteryInformation         = 0,
        BatteryGranularityInformation = 1,
        BatteryTemperature         = 2,
        BatteryEstimatedTime       = 3,
        BatteryDeviceName          = 4,
        BatteryManufactureDate     = 5,
        BatteryManufactureName     = 6,
        BatteryUniqueID            = 7,
        BatterySerialNumber        = 8,
    }

    [Flags]
    private enum BATTERY_CAPABILITIES : uint
    {
        BATTERY_CAPACITY_RELATIVE = 0x40000000,
        BATTERY_IS_SHORT_TERM     = 0x20000000,
        BATTERY_SET_CHARGE_SUPPORTED   = 0x00000001,
        BATTERY_SET_DISCHARGE_SUPPORTED= 0x00000002,
        BATTERY_SYSTEM_BATTERY    = 0x80000000,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BATTERY_QUERY_INFORMATION
    {
        public uint BatteryTag;
        public BATTERY_QUERY_INFORMATION_LEVEL InformationLevel;
        public int  AtRate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BATTERY_INFORMATION
    {
        public BATTERY_CAPABILITIES Capabilities;
        public byte Technology;          // 1 = rechargeable, 0 = primary
        public byte Reserved1;
        public byte Reserved2;
        public byte Reserved3;
        // 4-byte ASCII chemistry tag — this is the one we want
        public byte Chemistry0;
        public byte Chemistry1;
        public byte Chemistry2;
        public byte Chemistry3;
        public uint DesignedCapacity;
        public uint FullChargedCapacity;
        public uint DefaultAlert1;
        public uint DefaultAlert2;
        public uint CriticalBias;
        public uint CycleCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVICE_INTERFACE_DATA
    {
        public int   cbSize;
        public Guid  InterfaceClassGuid;
        public uint  Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SP_DEVICE_INTERFACE_DETAIL_DATA
    {
        public int    cbSize;
        // DevicePath is a flexible-length WCHAR array starting here. We fix the
        // struct size at allocation time and read the path off the same buffer.
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string DevicePath;
    }

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr SetupDiGetClassDevs(
        ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, uint Flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr DeviceInfoSet, IntPtr DeviceInfoData,
        ref Guid InterfaceClassGuid, uint MemberIndex,
        ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr DeviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData,
        ref SP_DEVICE_INTERFACE_DETAIL_DATA DeviceInterfaceDetailData,
        int DeviceInterfaceDetailDataSize,
        out int RequiredSize,
        IntPtr DeviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice, uint dwIoControlCode,
        ref BATTERY_QUERY_INFORMATION lpInBuffer, int nInBufferSize,
        IntPtr lpOutBuffer, int nOutBufferSize,
        out int lpBytesReturned, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice, uint dwIoControlCode,
        IntPtr lpInBuffer, int nInBufferSize,
        out uint lpOutBuffer, int nOutBufferSize,
        out int lpBytesReturned, IntPtr lpOverlapped);

    public sealed record IoctlBatteryInfo(
        string? Chemistry,
        string? FriendlyChemistry,
        byte? Technology,
        uint? DesignedCapacityMilliwattHours,
        uint? FullChargedCapacityMilliwattHours,
        uint? CycleCount,
        string? DeviceName,
        string? Manufacturer);

    /// <summary>
    /// Enumerates battery devices, queries the first one for chemistry + capacity info.
    /// Returns null if no battery present or the IOCTL path fails entirely.
    /// </summary>
    public static IoctlBatteryInfo? Read()
    {
        var batteryGuid = GUID_DEVCLASS_BATTERY;
        var hDevInfo = SetupDiGetClassDevs(ref batteryGuid, IntPtr.Zero, IntPtr.Zero,
            DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (hDevInfo == new IntPtr(-1)) return null;

        try
        {
            // Iterate battery interfaces — most laptops have one, but some have two
            // (main + bay). We just take the first one that responds.
            for (uint index = 0; ; index++)
            {
                var did = new SP_DEVICE_INTERFACE_DATA();
                did.cbSize = Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>();

                if (!SetupDiEnumDeviceInterfaces(hDevInfo, IntPtr.Zero,
                        ref batteryGuid, index, ref did))
                {
                    return null;
                }

                // Get the device path. The detail struct has a quirky layout — we
                // pre-size it for a 256-char path which fits any real device path.
                var detail = new SP_DEVICE_INTERFACE_DETAIL_DATA();
                // cbSize must be exactly the size of the fixed header (4 on x86, 8 on x64).
                detail.cbSize = IntPtr.Size == 8 ? 8 : 6;

                if (!SetupDiGetDeviceInterfaceDetail(hDevInfo, ref did, ref detail,
                        Marshal.SizeOf<SP_DEVICE_INTERFACE_DETAIL_DATA>(),
                        out _, IntPtr.Zero))
                {
                    continue;
                }

                using var handle = CreateFile(detail.DevicePath,
                    GENERIC_READ | GENERIC_WRITE,
                    FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);

                if (handle.IsInvalid) continue;

                // First we need a battery tag to use in subsequent queries.
                if (!DeviceIoControl(handle, IOCTL_BATTERY_QUERY_TAG,
                        IntPtr.Zero, 0, out uint tag, sizeof(uint),
                        out _, IntPtr.Zero) || tag == 0)
                {
                    continue;
                }

                var info = QueryBatteryInformation(handle, tag);
                var deviceName = QueryString(handle, tag, BATTERY_QUERY_INFORMATION_LEVEL.BatteryDeviceName);
                var manufacturer = QueryString(handle, tag, BATTERY_QUERY_INFORMATION_LEVEL.BatteryManufactureName);

                return new IoctlBatteryInfo(
                    info.Chemistry,
                    info.FriendlyChemistry,
                    info.Technology,
                    info.DesignedCapacity,
                    info.FullChargedCapacity,
                    info.CycleCount,
                    deviceName,
                    manufacturer);
            }
        }
        catch (Exception ex)
        {
            CrashLogger.Write("BatteryIoctlReader.Read", ex);
            return null;
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(hDevInfo);
        }
    }

    private static (string? Chemistry, string? FriendlyChemistry, byte? Technology,
                    uint? DesignedCapacity, uint? FullChargedCapacity, uint? CycleCount)
        QueryBatteryInformation(SafeFileHandle handle, uint tag)
    {
        var query = new BATTERY_QUERY_INFORMATION
        {
            BatteryTag = tag,
            InformationLevel = BATTERY_QUERY_INFORMATION_LEVEL.BatteryInformation,
        };

        var size = Marshal.SizeOf<BATTERY_INFORMATION>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (!DeviceIoControl(handle, IOCTL_BATTERY_QUERY_INFORMATION,
                    ref query, Marshal.SizeOf<BATTERY_QUERY_INFORMATION>(),
                    buffer, size, out _, IntPtr.Zero))
            {
                return (null, null, null, null, null, null);
            }

            var bi = Marshal.PtrToStructure<BATTERY_INFORMATION>(buffer);
            var chemBytes = new[] { bi.Chemistry0, bi.Chemistry1, bi.Chemistry2, bi.Chemistry3 };
            var chem = DecodeAsciiTag(chemBytes);
            var friendly = MapToFriendly(chem);

            return (chem, friendly, bi.Technology,
                    bi.DesignedCapacity == 0xFFFFFFFF ? null : bi.DesignedCapacity,
                    bi.FullChargedCapacity == 0xFFFFFFFF ? null : bi.FullChargedCapacity,
                    bi.CycleCount == 0 ? null : bi.CycleCount);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string? QueryString(SafeFileHandle handle, uint tag, BATTERY_QUERY_INFORMATION_LEVEL level)
    {
        var query = new BATTERY_QUERY_INFORMATION
        {
            BatteryTag = tag,
            InformationLevel = level,
        };

        // String fields are variable-length WCHAR. Allocate generously.
        const int bufSize = 512;
        var buffer = Marshal.AllocHGlobal(bufSize);
        try
        {
            if (!DeviceIoControl(handle, IOCTL_BATTERY_QUERY_INFORMATION,
                    ref query, Marshal.SizeOf<BATTERY_QUERY_INFORMATION>(),
                    buffer, bufSize, out int returned, IntPtr.Zero))
            {
                return null;
            }
            if (returned <= 0) return null;
            // Strings come back as null-terminated WCHAR. Marshal handles that.
            var s = Marshal.PtrToStringUni(buffer);
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    internal static string? DecodeAsciiTag(byte[] bytes)
    {
        var sb = new StringBuilder();
        foreach (var b in bytes)
        {
            if (b == 0) break;
            if (b < 0x20 || b > 0x7E) continue;
            sb.Append((char)b);
        }
        var s = sb.ToString().Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    internal static string? MapToFriendly(string? tag)
    {
        if (string.IsNullOrEmpty(tag)) return null;
        return tag.ToUpperInvariant() switch
        {
            "LION" or "LI-I" => $"Lithium-ion ({tag})",
            "LIP"  or "LI-P" => $"Lithium Polymer ({tag})",
            "LIFE" or "LFP"  => $"Lithium Iron Phosphate / LiFePO4 ({tag})",
            "NIMH" or "NMH"  => $"Nickel Metal Hydride ({tag})",
            "NICD" or "NCD"  => $"Nickel Cadmium ({tag})",
            "PBAC" or "PBA"  => $"Lead Acid ({tag})",
            "ZNAR"           => $"Zinc Air ({tag})",
            "LMNO" or "NMC"  => $"Li-NMC ({tag})",
            _ => $"{tag} (unrecognized firmware tag)",
        };
    }
}
