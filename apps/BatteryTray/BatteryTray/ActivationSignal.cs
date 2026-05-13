using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace BatteryTray;

/// <summary>
/// Cross-process signal used to ask the existing instance to "show yourself" when
/// a second invocation is attempted. Same cross-IL DACL story as CrossIntegrityMutex:
/// the running elevated instance must be wakeable from a non-elevated launch attempt.
/// </summary>
public sealed class ActivationSignal : IDisposable
{
    private readonly EventWaitHandle _handle;

    public ActivationSignal(EventWaitHandle handle) { _handle = handle; }

    public bool TrySignal()
    {
        try { return _handle.Set(); }
        catch { return false; }
    }

    public bool WaitOne(int millisecondsTimeout) => _handle.WaitOne(millisecondsTimeout);
    public void Reset() { try { _handle.Reset(); } catch { /* ignored */ } }

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
            CrashLogger.Write("ActivationSignal.CreateOrOpen", ex);
            // Fallback: best-effort plain handle. Cross-IL signalling won't work, but
            // basic same-IL signalling will.
            try
            {
                return new ActivationSignal(new EventWaitHandle(false, EventResetMode.AutoReset, name));
            }
            catch
            {
                // Ultimate fallback: an unnamed handle that will never be signalled by
                // anyone else. Caller behaves as if no second instance ever runs.
                return new ActivationSignal(new EventWaitHandle(false, EventResetMode.AutoReset));
            }
        }
    }

    private static EventWaitHandleSecurity BuildSecurity()
    {
        var sec = new EventWaitHandleSecurity();
        var users = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
        // Synchronize lets others Wait, Modify lets them Set.
        sec.AddAccessRule(new EventWaitHandleAccessRule(
            users,
            EventWaitHandleRights.Synchronize | EventWaitHandleRights.Modify,
            AccessControlType.Allow));

        try
        {
            var owner = WindowsIdentity.GetCurrent().User;
            if (owner is not null)
            {
                sec.AddAccessRule(new EventWaitHandleAccessRule(
                    owner,
                    EventWaitHandleRights.FullControl,
                    AccessControlType.Allow));
            }
        }
        catch { /* non-fatal */ }

        return sec;
    }

    public void Dispose() => _handle.Dispose();
}
