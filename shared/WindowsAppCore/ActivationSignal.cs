using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;

namespace WindowsAppCore;

/// <summary>
/// Cross-process named event used to wake the running instance when a second
/// invocation is detected. Same cross-IL DACL story as CrossIntegrityMutex.
/// </summary>
internal sealed class ActivationSignal : IDisposable
{
    private readonly EventWaitHandle _handle;

    private ActivationSignal(EventWaitHandle handle) => _handle = handle;

    public bool TrySignal()
    {
        try { return _handle.Set(); }
        catch { return false; }
    }

    public bool WaitOne(int millisecondsTimeout) => _handle.WaitOne(millisecondsTimeout);

    public static ActivationSignal CreateOrOpen(string name)
    {
        try
        {
            var security = BuildSecurity();
            var ev = EventWaitHandleAcl.Create(
                initialState: false,
                mode: EventResetMode.AutoReset,
                name: name,
                createdNew: out _,
                eventSecurity: security);
            return new ActivationSignal(ev);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ActivationSignal.CreateOrOpen — falling back to plain handle: {ex}");
            try
            {
                return new ActivationSignal(new EventWaitHandle(false, EventResetMode.AutoReset, name));
            }
            catch
            {
                return new ActivationSignal(new EventWaitHandle(false, EventResetMode.AutoReset));
            }
        }
    }

    private static EventWaitHandleSecurity BuildSecurity()
    {
        var sec = new EventWaitHandleSecurity();
        var users = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
        sec.AddAccessRule(new EventWaitHandleAccessRule(
            users,
            EventWaitHandleRights.Synchronize | EventWaitHandleRights.Modify,
            AccessControlType.Allow));

        try
        {
            var owner = WindowsIdentity.GetCurrent().User;
            if (owner is not null)
                sec.AddAccessRule(new EventWaitHandleAccessRule(
                    owner, EventWaitHandleRights.FullControl, AccessControlType.Allow));
        }
        catch { }

        return sec;
    }

    public void Dispose() => _handle.Dispose();
}
