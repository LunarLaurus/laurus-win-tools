namespace ProgramHider;

internal sealed class WindowHideService
{
    private readonly IWindowPlatform _platform;

    public WindowHideService(IWindowPlatform platform)
    {
        _platform = platform;
    }

    public bool TryHideWindow(
        nint handle,
        nint excludedHandle,
        IDictionary<nint, HiddenWindow> hiddenWindows,
        WindowRuleMatchResult? existingMatch,
        out HiddenWindow? hiddenWindow)
    {
        hiddenWindow = null;
        if (handle == 0 || handle == excludedHandle || hiddenWindows.ContainsKey(handle))
        {
            return false;
        }

        var snapshot = _platform.TryCreateWindowSnapshot(handle);
        if (snapshot is null || !WindowCatalog.IsManageableWindow(snapshot.Value))
        {
            return false;
        }

        var savedPlacement = _platform.TryGetWindowPlacement(handle);
        var monitorDeviceName = _platform.TryGetMonitorDeviceNameForWindow(handle);
        var ruleMatch = existingMatch ?? WindowRuleMatchResult.Evaluate(Array.Empty<WindowRule>(), snapshot.Value);
        if (!_platform.ShowWindow(handle, NativeMethods.SW_HIDE))
        {
            return false;
        }

        hiddenWindow = new HiddenWindow(
            handle,
            snapshot.Value.Title,
            snapshot.Value.ProcessName,
            snapshot.Value.ClassName,
            snapshot.Value.IsMaximized,
            DateTimeOffset.UtcNow,
            savedPlacement,
            monitorDeviceName,
            ruleMatch.RequirePinOnRestore,
            ruleMatch.SuppressNotifications);
        hiddenWindows[handle] = hiddenWindow;
        return true;
    }

    public bool TryRestoreWindow(
        nint handle,
        IDictionary<nint, HiddenWindow> hiddenWindows,
        bool restoreWithoutFocus,
        out HiddenWindow? restoredWindow)
    {
        restoredWindow = null;
        if (!hiddenWindows.Remove(handle, out var hiddenWindow))
        {
            return false;
        }

        restoredWindow = hiddenWindow;
        if (!_platform.IsWindow(handle))
        {
            return false;
        }

        if (hiddenWindow.SavedPlacement is NativeMethods.WindowPlacement placement)
        {
            _platform.TrySetWindowPlacement(handle, placement);
        }

        _platform.ShowWindow(
            handle,
            hiddenWindow.WasMaximized ? NativeMethods.SW_SHOWMAXIMIZED : NativeMethods.SW_RESTORE);
        if (!restoreWithoutFocus)
        {
            _platform.SetForegroundWindow(handle);
        }

        return true;
    }

    public int PruneDeadWindows(IDictionary<nint, HiddenWindow> hiddenWindows)
    {
        var removed = hiddenWindows.Keys
            .Where(handle => !_platform.IsWindow(handle))
            .ToArray();

        foreach (var handle in removed)
        {
            hiddenWindows.Remove(handle);
        }

        return removed.Length;
    }
}
