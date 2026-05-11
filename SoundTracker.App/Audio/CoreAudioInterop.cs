using System.Runtime.InteropServices;

namespace SoundTracker.App.Audio;

internal static class CoreAudioInterop
{
    public static readonly Guid MMDeviceEnumeratorClsid =
        new("BCDE0395-E52F-467C-8E3D-C4579291692E");
}

internal enum EDataFlow
{
    Render = 0,
    Capture = 1,
    All = 2,
}

internal enum ERole
{
    Console = 0,
    Multimedia = 1,
    Communications = 2,
}

internal enum AudioSessionState
{
    Inactive = 0,
    Active = 1,
    Expired = 2,
}

[Flags]
internal enum CLSCTX : uint
{
    InProcServer = 0x1,
}

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal sealed class MMDeviceEnumeratorComObject
{
}

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig]
    int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out object devices);

    [PreserveSig]
    int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);

    [PreserveSig]
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);

    [PreserveSig]
    int RegisterEndpointNotificationCallback(IntPtr client);

    [PreserveSig]
    int UnregisterEndpointNotificationCallback(IntPtr client);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig]
    int Activate(
        ref Guid iid,
        CLSCTX clsCtx,
        IntPtr activationParams,
        [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);

    [PreserveSig]
    int OpenPropertyStore(uint storageAccess, out object properties);

    [PreserveSig]
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

    [PreserveSig]
    int GetState(out uint state);
}

[ComImport]
[Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionControl
{
    [PreserveSig]
    int GetState(out AudioSessionState state);

    [PreserveSig]
    int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);

    [PreserveSig]
    int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid eventContext);

    [PreserveSig]
    int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);

    [PreserveSig]
    int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid eventContext);

    [PreserveSig]
    int GetGroupingParam(out Guid groupingId);

    [PreserveSig]
    int SetGroupingParam(ref Guid groupingId, ref Guid eventContext);

    [PreserveSig]
    int RegisterAudioSessionNotification(IntPtr client);

    [PreserveSig]
    int UnregisterAudioSessionNotification(IntPtr client);
}

[ComImport]
[Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionControl2
{
    [PreserveSig]
    int GetState(out AudioSessionState state);

    [PreserveSig]
    int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);

    [PreserveSig]
    int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid eventContext);

    [PreserveSig]
    int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);

    [PreserveSig]
    int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid eventContext);

    [PreserveSig]
    int GetGroupingParam(out Guid groupingId);

    [PreserveSig]
    int SetGroupingParam(ref Guid groupingId, ref Guid eventContext);

    [PreserveSig]
    int RegisterAudioSessionNotification(IntPtr client);

    [PreserveSig]
    int UnregisterAudioSessionNotification(IntPtr client);

    [PreserveSig]
    int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string identifier);

    [PreserveSig]
    int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string identifier);

    [PreserveSig]
    int GetProcessId(out uint processId);

    [PreserveSig]
    int IsSystemSoundsSession();

    [PreserveSig]
    int SetDuckingPreference(bool optOut);
}

[ComImport]
[Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionManager2
{
    [PreserveSig]
    int GetAudioSessionControl(ref Guid audioSessionGuid, uint streamFlags, out IAudioSessionControl sessionControl);

    [PreserveSig]
    int GetSimpleAudioVolume(ref Guid audioSessionGuid, uint streamFlags, out object audioVolume);

    [PreserveSig]
    int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnumerator);

    [PreserveSig]
    int RegisterSessionNotification(IntPtr sessionNotification);

    [PreserveSig]
    int UnregisterSessionNotification(IntPtr sessionNotification);

    [PreserveSig]
    int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId, IntPtr duckNotification);

    [PreserveSig]
    int UnregisterDuckNotification(IntPtr duckNotification);
}

[ComImport]
[Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionEnumerator
{
    [PreserveSig]
    int GetCount(out int sessionCount);

    [PreserveSig]
    int GetSession(int sessionIndex, out IAudioSessionControl session);
}
