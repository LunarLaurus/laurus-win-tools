namespace ProgramHider;

internal sealed class ActiveWindowTracker
{
    private readonly IWindowPlatform _platform;
    private readonly object _sync = new();
    private nint _lastTrackedHandle;

    public ActiveWindowTracker(IWindowPlatform platform)
    {
        _platform = platform;
    }

    public NativeWindowSnapshot? CaptureCurrentSnapshot(Func<NativeWindowSnapshot, bool> canTrackWindow)
    {
        return CaptureSnapshotForHandle(_platform.GetForegroundWindow(), canTrackWindow);
    }

    public NativeWindowSnapshot? CaptureSnapshotForHandle(nint handle, Func<NativeWindowSnapshot, bool> canTrackWindow)
    {
        var snapshot = _platform.TryCreateWindowSnapshot(handle);
        if (snapshot is null || !canTrackWindow(snapshot.Value))
        {
            return null;
        }

        lock (_sync)
        {
            _lastTrackedHandle = snapshot.Value.Handle;
        }

        return snapshot;
    }

    public NativeWindowSnapshot? ResolveSnapshot(Func<NativeWindowSnapshot, bool> canTrackWindow)
    {
        var currentSnapshot = CaptureCurrentSnapshot(canTrackWindow);
        if (currentSnapshot is not null)
        {
            return currentSnapshot;
        }

        nint fallbackHandle;
        lock (_sync)
        {
            fallbackHandle = _lastTrackedHandle;
        }

        if (fallbackHandle == 0)
        {
            return null;
        }

        var fallbackSnapshot = _platform.TryCreateWindowSnapshot(fallbackHandle);
        if (fallbackSnapshot is not null && canTrackWindow(fallbackSnapshot.Value))
        {
            return fallbackSnapshot;
        }

        Clear();
        return null;
    }

    public void Clear()
    {
        lock (_sync)
        {
            _lastTrackedHandle = 0;
        }
    }
}
