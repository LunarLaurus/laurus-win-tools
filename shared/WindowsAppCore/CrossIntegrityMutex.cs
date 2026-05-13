using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;

namespace WindowsAppCore;

/// <summary>
/// Single-instance enforcement that survives integrity-level boundaries.
///
/// The default Mutex constructor grants access only to the owner. That fails when
/// a High-IL (elevated) instance is already running and a Medium-IL instance tries
/// to detect it — the non-elevated process can't open the mutex. We attach an
/// explicit DACL that grants Synchronize+Modify to Authenticated Users so any IL
/// can detect the running instance.
/// </summary>
public sealed class CrossIntegrityMutex : IDisposable
{
    private readonly Mutex _mutex;
    private readonly bool _ownsHandle;

    public bool CreatedNew { get; }

    private CrossIntegrityMutex(Mutex mutex, bool createdNew, bool ownsHandle)
    {
        _mutex = mutex;
        CreatedNew = createdNew;
        _ownsHandle = ownsHandle;
    }

    public static CrossIntegrityMutex CreateOrOpen(string name)
    {
        try
        {
            var security = BuildSecurity();
            var mutex = MutexAcl.Create(
                initiallyOwned: true,
                name: name,
                createdNew: out bool createdNew,
                mutexSecurity: security);
            return new CrossIntegrityMutex(mutex, createdNew, ownsHandle: createdNew);
        }
        catch (UnauthorizedAccessException)
        {
            // Mutex exists with a restrictive DACL (older binary). Treat as "already running".
            return new CrossIntegrityMutex(new Mutex(false), createdNew: false, ownsHandle: false);
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or NotSupportedException)
        {
            Debug.WriteLine($"MutexAcl unavailable — falling back to plain Mutex: {ex}");
            var mutex = new Mutex(initiallyOwned: true, name, out bool createdNew);
            return new CrossIntegrityMutex(mutex, createdNew, ownsHandle: createdNew);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CrossIntegrityMutex.CreateOrOpen failed: {ex}");
            return new CrossIntegrityMutex(new Mutex(false), createdNew: false, ownsHandle: false);
        }
    }

    private static MutexSecurity BuildSecurity()
    {
        var security = new MutexSecurity();

        var users = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
        security.AddAccessRule(new MutexAccessRule(
            users,
            MutexRights.Synchronize | MutexRights.Modify,
            AccessControlType.Allow));

        try
        {
            var owner = WindowsIdentity.GetCurrent().User;
            if (owner is not null)
                security.AddAccessRule(new MutexAccessRule(
                    owner, MutexRights.FullControl, AccessControlType.Allow));
        }
        catch { }

        return security;
    }

    public void Dispose()
    {
        try
        {
            if (_ownsHandle)
                try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
            _mutex.Dispose();
        }
        catch (Exception ex) { Debug.WriteLine($"CrossIntegrityMutex.Dispose: {ex}"); }
    }
}
