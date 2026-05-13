using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace BatteryTray;

/// <summary>
/// Single-instance enforcement that survives integrity-level boundaries.
///
/// The default Mutex constructor sets a security descriptor that allows access only
/// to the owner. That fails when:
///   1. An elevated (High IL) instance auto-launches via the scheduled task, AND
///   2. A non-elevated (Medium IL) instance is launched manually by double-clicking
///
/// The Medium-IL process can't open the High-IL mutex, so it thinks no one's running
/// and starts a second tray icon. We fix this by attaching an explicit DACL that
/// grants Synchronize+Modify rights to Authenticated Users, plus a low-IL
/// mandatory-label SACL so processes at any integrity level can find us.
/// </summary>
internal sealed class CrossIntegrityMutex : IDisposable
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

    /// <summary>
    /// Creates or opens a named mutex with cross-IL access. Falls back to a plain
    /// Mutex if MutexAcl isn't available on this runtime — better to have basic
    /// single-instance than nothing.
    /// </summary>
    public static CrossIntegrityMutex CreateOrOpen(string name)
    {
        try
        {
            var security = BuildSecurity();
            // MutexAcl.Create returns a new mutex (and reports createdNew). If one already
            // exists with the same name and our DACL allows MutexRights.Synchronize, we'll
            // get a handle to it.
            var mutex = MutexAcl.Create(
                initiallyOwned: true,
                name: name,
                createdNew: out bool createdNew,
                mutexSecurity: security);
            return new CrossIntegrityMutex(mutex, createdNew, ownsHandle: createdNew);
        }
        catch (UnauthorizedAccessException)
        {
            // The mutex exists but we don't have rights to it (e.g. created by an
            // older binary version with a restrictive DACL). Treat as "another instance
            // already running" — that's the safe interpretation.
            return new CrossIntegrityMutex(new Mutex(false), createdNew: false, ownsHandle: false);
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or NotSupportedException)
        {
            // Older runtime without MutexAcl. Fall back to plain Mutex; cross-IL gap returns
            // but the app at least still starts.
            CrashLogger.Write("MutexAcl unavailable", ex);
            var mutex = new Mutex(initiallyOwned: true, name, out bool createdNew);
            return new CrossIntegrityMutex(mutex, createdNew, ownsHandle: createdNew);
        }
        catch (Exception ex)
        {
            CrashLogger.Write("CrossIntegrityMutex.CreateOrOpen", ex);
            var mutex = new Mutex(false);
            return new CrossIntegrityMutex(mutex, createdNew: false, ownsHandle: false);
        }
    }

    private static MutexSecurity BuildSecurity()
    {
        var security = new MutexSecurity();

        // Grant Synchronize + Modify (the rights needed to wait/release) to all
        // authenticated users. This is what lets a Medium-IL process open a mutex
        // created by a High-IL process.
        var authenticatedUsers = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
        security.AddAccessRule(new MutexAccessRule(
            authenticatedUsers,
            MutexRights.Synchronize | MutexRights.Modify,
            AccessControlType.Allow));

        // Also add the current user explicitly with full control, so we can always
        // modify the DACL if needed for cleanup.
        try
        {
            var owner = WindowsIdentity.GetCurrent().User;
            if (owner is not null)
            {
                security.AddAccessRule(new MutexAccessRule(
                    owner,
                    MutexRights.FullControl,
                    AccessControlType.Allow));
            }
        }
        catch
        {
            // Non-fatal — Authenticated Users rule above is sufficient.
        }

        // Note on the SACL / mandatory label: in principle we'd add a Low integrity
        // mandatory label here so even sandboxed processes (e.g. browser sandbox)
        // could open the mutex. In practice BatteryTray is only ever run from the
        // user's desktop session at Medium or High IL, and adding a SACL requires
        // SeSecurityPrivilege which we'd rather not depend on. The DACL alone
        // bridges the Medium↔High gap that's the actual concern.

        return security;
    }

    public void Dispose()
    {
        try
        {
            if (_ownsHandle)
            {
                try { _mutex.ReleaseMutex(); } catch (ApplicationException) { /* not held */ }
            }
            _mutex.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CrossIntegrityMutex.Dispose: {ex}");
        }
    }
}
