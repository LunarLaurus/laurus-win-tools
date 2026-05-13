using System.Runtime.InteropServices;

namespace BatteryTray;

/// <summary>
/// Receives WM_POWERBROADCAST notifications by hosting a hidden NativeWindow.
/// We register interest in three power-setting GUIDs:
///   GUID_ACDC_POWER_SOURCE — AC plug/unplug
///   GUID_BATTERY_PERCENTAGE_REMAINING — every 1pp change
///   GUID_MONITOR_POWER_ON — display on/off (useful for skipping work while screen off)
/// Subscribers get a coarse "power state changed, re-read it" event.
/// </summary>
public sealed class PowerEventListener : IDisposable
{
    private const int WM_POWERBROADCAST = 0x0218;
    private const int PBT_POWERSETTINGCHANGE = 0x8013;
    private const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

    private static readonly Guid GUID_ACDC_POWER_SOURCE             = new("5d3e9a59-e9D5-4b00-a6bd-ff34ff516548");
    private static readonly Guid GUID_BATTERY_PERCENTAGE_REMAINING  = new("a7ad8041-b45a-4cae-87a3-eecbb468a9e1");
    private static readonly Guid GUID_MONITOR_POWER_ON              = new("02731015-4510-4526-99e6-e5a17ebd1aea");

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct POWERBROADCAST_SETTING
    {
        public Guid PowerSetting;
        public uint DataLength;
        public byte Data;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid PowerSettingGuid, int Flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterPowerSettingNotification(IntPtr Handle);

    public event EventHandler? PowerStateChanged;
    public event EventHandler<bool>? MonitorPowerChanged;  // true = on, false = off

    private readonly MessageWindow _window;
    private readonly List<IntPtr> _registrations = new();

    public PowerEventListener()
    {
        _window = new MessageWindow(this);

        Register(GUID_ACDC_POWER_SOURCE);
        Register(GUID_BATTERY_PERCENTAGE_REMAINING);
        Register(GUID_MONITOR_POWER_ON);
    }

    private void Register(Guid setting)
    {
        var handle = RegisterPowerSettingNotification(_window.Handle, ref setting, DEVICE_NOTIFY_WINDOW_HANDLE);
        if (handle != IntPtr.Zero) _registrations.Add(handle);
    }

    private void OnMessage(ref Message m)
    {
        if (m.Msg != WM_POWERBROADCAST) return;
        if ((int)m.WParam != PBT_POWERSETTINGCHANGE) return;
        if (m.LParam == IntPtr.Zero) return;

        var setting = Marshal.PtrToStructure<POWERBROADCAST_SETTING>(m.LParam);

        if (setting.PowerSetting == GUID_MONITOR_POWER_ON)
        {
            MonitorPowerChanged?.Invoke(this, setting.Data != 0);
        }

        // ACDC and BATTERY_PERCENTAGE_REMAINING both indicate "battery picture changed".
        if (setting.PowerSetting == GUID_ACDC_POWER_SOURCE
            || setting.PowerSetting == GUID_BATTERY_PERCENTAGE_REMAINING)
        {
            PowerStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        foreach (var h in _registrations)
        {
            try { UnregisterPowerSettingNotification(h); } catch { }
        }
        _registrations.Clear();
        _window.DestroyHandle();
    }

    private sealed class MessageWindow : NativeWindow
    {
        private readonly PowerEventListener _owner;

        public MessageWindow(PowerEventListener owner)
        {
            _owner = owner;
            CreateHandle(new CreateParams { Caption = "BatteryTrayPowerSink" });
        }

        protected override void WndProc(ref Message m)
        {
            _owner.OnMessage(ref m);
            base.WndProc(ref m);
        }
    }
}
