using System.Runtime.InteropServices;
using SoundTracker.App.Processes;

namespace SoundTracker.App.Audio;

internal sealed class AudioSessionPoller
{
    private readonly ProcessNameResolver _processNameResolver = new();

    public IReadOnlyList<string> GetActiveSessionNames()
    {
        var sessions = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        IAudioSessionManager2? manager = null;
        IAudioSessionEnumerator? sessionEnumerator = null;

        try
        {
            enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(
                Type.GetTypeFromCLSID(CoreAudioInterop.MMDeviceEnumeratorClsid)!)!;
            Marshal.ThrowExceptionForHR(
                enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Console, out device));

            var sessionManagerGuid = typeof(IAudioSessionManager2).GUID;
            Marshal.ThrowExceptionForHR(
                device.Activate(ref sessionManagerGuid, CLSCTX.InProcServer, IntPtr.Zero, out var managerObject));
            manager = (IAudioSessionManager2)managerObject;

            Marshal.ThrowExceptionForHR(manager.GetSessionEnumerator(out sessionEnumerator));
            Marshal.ThrowExceptionForHR(sessionEnumerator.GetCount(out var count));

            for (var index = 0; index < count; index++)
            {
                IAudioSessionControl? sessionControl = null;

                try
                {
                    Marshal.ThrowExceptionForHR(sessionEnumerator.GetSession(index, out sessionControl));
                    if (sessionControl is not IAudioSessionControl2 sessionControl2)
                    {
                        continue;
                    }

                    Marshal.ThrowExceptionForHR(sessionControl.GetState(out var state));
                    if (state != AudioSessionState.Active)
                    {
                        continue;
                    }

                    Marshal.ThrowExceptionForHR(sessionControl2.GetProcessId(out var processId));
                    if (processId == 0)
                    {
                        continue;
                    }

                    var name = _processNameResolver.TryGetProcessName(processId);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        sessions.Add(name);
                    }
                }
                catch (COMException)
                {
                    // Sessions can disappear between enumeration and inspection.
                }
                finally
                {
                    ReleaseComObject(sessionControl);
                }
            }
        }
        catch (COMException)
        {
            return Array.Empty<string>();
        }
        finally
        {
            ReleaseComObject(sessionEnumerator);
            ReleaseComObject(manager);
            ReleaseComObject(device);
            ReleaseComObject(enumerator);
        }

        return sessions.ToList();
    }

    private static void ReleaseComObject(object? instance)
    {
        if (instance is not null && Marshal.IsComObject(instance))
        {
            Marshal.FinalReleaseComObject(instance);
        }
    }
}
