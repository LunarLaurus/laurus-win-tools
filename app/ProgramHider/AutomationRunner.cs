namespace ProgramHider;

internal sealed class AutomationRunner
{
    private readonly IWindowPlatform _platform;
    private readonly WindowHideService _hideService;

    public AutomationRunner(IWindowPlatform platform)
    {
        _platform = platform;
        _hideService = new WindowHideService(platform);
    }

    public int SmokeHideRestoreByTitle(string titleFragment, TextWriter output, string? processNameFilter = null)
    {
        if (string.IsNullOrWhiteSpace(titleFragment))
        {
            output.WriteLine("missing-title");
            return 2;
        }

        var hiddenWindows = new Dictionary<nint, HiddenWindow>();
        var targetWindow = WindowCatalog.FindFirstByTitleContains(_platform, titleFragment, processNameFilter);
        if (targetWindow is null)
        {
            output.WriteLine("window-not-found");
            return 3;
        }

        output.WriteLine($"found:{targetWindow.Value.Title}|{targetWindow.Value.ProcessName}|0x{targetWindow.Value.Handle.ToInt64():X}");

        if (!_hideService.TryHideWindow(targetWindow.Value.Handle, 0, hiddenWindows, null, out var hiddenWindow) || hiddenWindow is null)
        {
            output.WriteLine("hide-failed");
            return 4;
        }

        Thread.Sleep(500);
        var visibleAfterHide = _platform.TryCreateWindowSnapshot(targetWindow.Value.Handle) is not null;
        output.WriteLine(visibleAfterHide ? "hide-verify-failed" : "hide-verify-ok");
        if (visibleAfterHide)
        {
            return 5;
        }

        if (!_hideService.TryRestoreWindow(targetWindow.Value.Handle, hiddenWindows, restoreWithoutFocus: false, out _))
        {
            output.WriteLine("restore-failed");
            return 6;
        }

        Thread.Sleep(500);
        var restoredWindow = _platform.TryCreateWindowSnapshot(targetWindow.Value.Handle);
        if (restoredWindow is null)
        {
            output.WriteLine("restore-verify-failed");
            return 7;
        }

        output.WriteLine($"restore-verify-ok:{restoredWindow.Value.Title}|0x{restoredWindow.Value.Handle.ToInt64():X}");
        return 0;
    }

    public int FindWindowByTitle(string titleFragment, TextWriter output, string? processNameFilter = null)
    {
        if (string.IsNullOrWhiteSpace(titleFragment))
        {
            output.WriteLine("missing-title");
            return 2;
        }

        var targetWindow = WindowCatalog.FindFirstByTitleContains(_platform, titleFragment, processNameFilter);
        if (targetWindow is null)
        {
            output.WriteLine("window-not-found");
            return 3;
        }

        output.WriteLine($"found:{targetWindow.Value.Title}|{targetWindow.Value.ProcessName}|{targetWindow.Value.ClassName}|0x{targetWindow.Value.Handle.ToInt64():X}");
        return 0;
    }

    public int ListManageableWindows(TextWriter output)
    {
        foreach (var window in WindowCatalog.EnumerateManageableWindows(_platform))
        {
            output.WriteLine($"{window.ProcessName}|{window.ClassName}|{window.Title}|0x{window.Handle.ToInt64():X}");
        }

        return 0;
    }
}
