namespace ProgramHider;

// Stateless helpers for enumerating and filtering candidate top-level windows.
internal static class WindowCatalog
{
    internal static IReadOnlyList<NativeWindowSnapshot> EnumerateManageableWindows(
        IWindowPlatform platform,
        IReadOnlyCollection<nint>? hiddenHandles = null,
        nint excludedHandle = 0)
    {
        hiddenHandles ??= Array.Empty<nint>();

        return platform.EnumerateTopLevelWindows()
            .Where(window => window.Handle != excludedHandle)
            .Where(window => !hiddenHandles.Contains(window.Handle))
            .Where(IsManageableWindow)
            .OrderBy(window => window.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static NativeWindowSnapshot? FindFirstByTitleContains(
        IWindowPlatform platform,
        string titleFragment,
        string? processNameFilter = null,
        IReadOnlyCollection<nint>? hiddenHandles = null,
        nint excludedHandle = 0)
    {
        if (string.IsNullOrWhiteSpace(titleFragment))
        {
            return null;
        }

        foreach (var window in EnumerateManageableWindows(platform, hiddenHandles, excludedHandle))
        {
            if (!string.IsNullOrWhiteSpace(processNameFilter) &&
                !string.Equals(window.ProcessName, processNameFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (window.Title.Contains(titleFragment, StringComparison.OrdinalIgnoreCase))
            {
                return window;
            }
        }

        return null;
    }

    internal static bool IsManageableWindow(NativeWindowSnapshot window)
    {
        return !string.IsNullOrWhiteSpace(window.Title) &&
               !string.IsNullOrWhiteSpace(window.ProcessName) &&
               window.Owner == 0 &&
               (window.ExtendedStyle & NativeMethods.WS_EX_TOOLWINDOW) == 0 &&
               !string.Equals(window.ClassName, "Shell_TrayWnd", StringComparison.Ordinal) &&
               !string.Equals(window.ClassName, "Progman", StringComparison.Ordinal);
    }
}
