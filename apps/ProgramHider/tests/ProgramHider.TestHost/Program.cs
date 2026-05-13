using ProgramHider;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WindowsAppCore;

return await TestHostProgram.RunAsync(args);

internal static class TestHostProgram
{
    public static Task<int> RunAsync(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "smoke-hide-restore", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(RunSmokeHideRestore(args.Skip(1).ToArray()));
        }

        if (args.Length > 0 && string.Equals(args[0], "find-window", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(RunFindWindow(args.Skip(1).ToArray()));
        }

        if (args.Length > 0 && string.Equals(args[0], "list-windows", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(RunListWindows());
        }

        if (args.Length > 0 && string.Equals(args[0], "foreground-window", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(RunForegroundWindow());
        }

        if (args.Length > 0 && string.Equals(args[0], "send-hotkey", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(RunSendHotkey(args.Skip(1).ToArray()));
        }

        if (args.Length > 0 && string.Equals(args[0], "focus-window", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(RunFocusWindow(args.Skip(1).ToArray()));
        }

        if (args.Length > 0 && string.Equals(args[0], "trigger-program-hider-hotkey", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(RunTriggerProgramHiderHotkey());
        }

        return Task.FromResult(RunUnitTests());
    }

    private static int RunUnitTests()
    {
        var runner = new TestRunner();
        runner.Run("WindowRule matches process title and class", WindowRuleMatchesExpectedFields);
        runner.Run("WindowRule evaluator merges flags", WindowRuleEvaluatorMergesFlags);
        runner.Run("AppSettings normalize migrates legacy process rules", AppSettingsNormalizeMigratesLegacyRules);
        runner.Run("AppSettings normalize preserves rule-level PIN hash", AppSettingsNormalizePreservesRuleProtectedPin);
        runner.Run("SettingsStore honors environment override path", SettingsStoreHonorsEnvironmentOverridePath);
        runner.Run("StartupOptions parse handles startup safe mode and delay", StartupOptionsParsesFlags);
        runner.Run("Elevation restart arguments preserve retry state", ElevationRestartArgumentsPreserveRetryState);
        runner.Run("PinSecurity hashes and verifies secrets", PinSecurityHashesAndVerifiesSecrets);
        runner.Run("HotkeySettings normalizes modifiers and default key", HotkeySettingsNormalizesDefaults);
        runner.Run("WindowCatalog find by title respects process filter and null on miss", WindowCatalogFindByTitleRespectsProcessFilter);
        runner.Run("WindowCatalog enumerate filters hidden excluded and non-manageable windows", WindowCatalogEnumerateFiltersCandidates);
        runner.Run("ActiveWindowTracker falls back to last tracked window", ActiveWindowTrackerFallsBackToLastTrackedWindow);
        runner.Run("ActiveWindowTracker clears stale fallback windows", ActiveWindowTrackerClearsStaleFallback);
        runner.Run("ActiveWindowTracker remembers explicit foreground handles", ActiveWindowTrackerTracksExplicitHandles);
        runner.Run("WindowHideService hide restore and prune works", WindowHideServiceExercisesFakePlatform);
        runner.Run("WindowHideService rejects excluded and duplicate windows", WindowHideServiceRejectsExcludedAndDuplicateWindows);
        runner.Run("Startup registration creates and removes ProgramHider Run key", TestProgramHiderStartupRegistration);
        return runner.Finish();
    }

    private static int RunSmokeHideRestore(string[] args)
    {
        var title = ParseRequiredOption(args, "--title");
        var process = ParseRequiredOption(args, "--process");
        if (string.IsNullOrWhiteSpace(title))
        {
            Console.Error.WriteLine("Missing required --title argument.");
            return 2;
        }

        var output = new StringWriter(new StringBuilder());
        var runner = new AutomationRunner(new Win32WindowPlatform());
        var exitCode = runner.SmokeHideRestoreByTitle(title, output, process);
        Console.Write(output.ToString());
        return exitCode;
    }

    private static int RunFindWindow(string[] args)
    {
        var title = ParseRequiredOption(args, "--title");
        var process = ParseRequiredOption(args, "--process");
        if (string.IsNullOrWhiteSpace(title))
        {
            Console.Error.WriteLine("Missing required --title argument.");
            return 2;
        }

        var output = new StringWriter(new StringBuilder());
        var runner = new AutomationRunner(new Win32WindowPlatform());
        var exitCode = runner.FindWindowByTitle(title, output, process);
        Console.Write(output.ToString());
        return exitCode;
    }

    private static int RunListWindows()
    {
        var output = new StringWriter(new StringBuilder());
        var runner = new AutomationRunner(new Win32WindowPlatform());
        var exitCode = runner.ListManageableWindows(output);
        Console.Write(output.ToString());
        return exitCode;
    }

    private static int RunForegroundWindow()
    {
        var platform = new Win32WindowPlatform();
        var snapshot = platform.TryCreateWindowSnapshot(platform.GetForegroundWindow());
        if (snapshot is null)
        {
            Console.WriteLine("foreground:none");
            return 1;
        }

        Console.WriteLine(
            $"foreground:{snapshot.Value.Title}|{snapshot.Value.ProcessName}|0x{snapshot.Value.Handle.ToInt64():X}");
        return 0;
    }

    private static int RunSendHotkey(string[] args)
    {
        var keyName = ParseRequiredOption(args, "--key");
        if (string.IsNullOrWhiteSpace(keyName))
        {
            Console.Error.WriteLine("Missing required --key argument.");
            return 2;
        }

        if (!Enum.TryParse<Keys>(keyName, ignoreCase: true, out var key) || key == Keys.None)
        {
            Console.Error.WriteLine($"Unsupported key '{keyName}'.");
            return 2;
        }

        NativeInput.SendHotkey(
            HasOption(args, "--control"),
            HasOption(args, "--shift"),
            HasOption(args, "--alt"),
            HasOption(args, "--windows"),
            key);
        return 0;
    }

    private static int RunFocusWindow(string[] args)
    {
        var title = ParseRequiredOption(args, "--title");
        var process = ParseRequiredOption(args, "--process");
        if (string.IsNullOrWhiteSpace(title))
        {
            Console.Error.WriteLine("Missing required --title argument.");
            return 2;
        }

        var platform = new Win32WindowPlatform();
        var snapshot = WindowCatalog.FindFirstByTitleContains(platform, title, process);
        if (snapshot is null)
        {
            Console.Error.WriteLine($"No matching window found for '{title}'.");
            return 1;
        }

        platform.ShowWindow(snapshot.Value.Handle, NativeMethods.SW_RESTORE);
        if (!platform.SetForegroundWindow(snapshot.Value.Handle))
        {
            Console.Error.WriteLine($"Unable to focus '{snapshot.Value.Title}'.");
            return 1;
        }

        Console.WriteLine($"focused:{snapshot.Value.Title}|{snapshot.Value.ProcessName}|0x{snapshot.Value.Handle.ToInt64():X}");
        return 0;
    }

    private static int RunTriggerProgramHiderHotkey()
    {
        var handle = NativeWindowProbe.FindWindowByCaption("ProgramHiderMessageWindow");
        if (handle == 0)
        {
            Console.Error.WriteLine("Program Hider message window was not found.");
            return 1;
        }

        if (!NativeWindowProbe.PostHotkeyMessage(handle, 0x1000))
        {
            Console.Error.WriteLine("Unable to post WM_HOTKEY to Program Hider.");
            return 1;
        }

        Console.WriteLine($"hotkey-posted:0x{handle.ToInt64():X}");
        return 0;
    }

    private static string ParseRequiredOption(string[] args, string optionName)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], optionName, StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Length)
            {
                return args[index + 1];
            }
        }

        return string.Empty;
    }

    private static bool HasOption(string[] args, string optionName)
    {
        return args.Any(argument => string.Equals(argument, optionName, StringComparison.OrdinalIgnoreCase));
    }

    private static void WindowRuleMatchesExpectedFields()
    {
        var rule = new WindowRule
        {
            MatchProcessName = "pwsh",
            MatchTitleContains = "Adguard Home",
            MatchClassName = "ConsoleWindowClass"
        };
        rule.Normalize();

        var matchingWindow = new NativeWindowSnapshot(
            (nint)42,
            "Adguard Home - Administrator: PowerShell",
            "ConsoleWindowClass",
            "pwsh",
            42,
            0,
            0,
            false);
        var nonMatchingWindow = matchingWindow with { ProcessName = "notepad" };

        TestAssert.True(rule.Matches(matchingWindow), "Expected the rule to match the target window.");
        TestAssert.False(rule.Matches(nonMatchingWindow), "Expected the rule not to match the different process.");
    }

    private static void WindowRuleEvaluatorMergesFlags()
    {
        var window = new NativeWindowSnapshot((nint)7, "Adguard Home", "ConsoleWindowClass", "powershell", 7, 0, 0, false);
        var rules = new[]
        {
            new WindowRule { RuleName = "auto", MatchProcessName = "powershell", AutoHideOnMinimize = true },
            new WindowRule { RuleName = "pin", MatchTitleContains = "Adguard", AutoHideOnMinimize = false, RequirePinOnRestore = true },
            new WindowRule { RuleName = "quiet", MatchClassName = "ConsoleWindowClass", AutoHideOnMinimize = false, SuppressNotifications = true }
        };
        foreach (var rule in rules)
        {
            rule.Normalize();
        }

        var result = WindowRuleMatchResult.Evaluate(rules, window);

        TestAssert.True(result.AutoHideOnMinimize, "Expected merged rules to auto-hide.");
        TestAssert.True(result.RequirePinOnRestore, "Expected merged rules to require PIN on restore.");
        TestAssert.True(result.SuppressNotifications, "Expected merged rules to suppress notifications.");
        TestAssert.Equal(3, result.MatchingRules.Count, "Expected three matching rules.");
    }

    private static void AppSettingsNormalizeMigratesLegacyRules()
    {
        var settings = new AppSettings
        {
            AutoHideProcessNames = new List<string> { "powershell", "PowerShell", "notepad" }
        };

        settings.Normalize();

        TestAssert.Equal(2, settings.WindowRules.Count, "Expected unique migrated rules.");
        TestAssert.True(settings.WindowRules.All(rule => rule.AutoHideOnMinimize), "Migrated legacy rules must auto-hide.");
        TestAssert.True(settings.AutoHideProcessNames.Count == 0, "Legacy list should be cleared after migration.");
    }

    private static void AppSettingsNormalizePreservesRuleProtectedPin()
    {
        var expectedHash = PinSecurity.HashSecret("1234");
        var settings = new AppSettings
        {
            RequirePinToRestore = false,
            PinHash = expectedHash,
            WindowRules = new List<WindowRule>
            {
                new() { MatchProcessName = "powershell", RequirePinOnRestore = true }
            }
        };

        settings.Normalize();

        TestAssert.Equal(expectedHash, settings.PinHash, "Rule-level restore PIN should preserve the stored hash.");
    }

    private static void StartupOptionsParsesFlags()
    {
        var options = StartupOptions.Parse(new[] { "--startup", "--delay=42", "--safe-mode", "--rehide=0x1A2B" });

        TestAssert.True(options.IsStartupLaunch, "Expected startup flag.");
        TestAssert.Equal(42, options.DelaySeconds, "Expected delay to parse.");
        TestAssert.True(options.SafeMode, "Expected safe mode flag.");
        TestAssert.Equal((nint)0x1A2B, options.PendingHideHandle, "Expected pending hide handle to parse.");
    }

    private static void ElevationRestartArgumentsPreserveRetryState()
    {
        var options = new StartupOptions
        {
            IsStartupLaunch = true,
            DelaySeconds = 12,
            SafeMode = true
        };

        var arguments = ElevationService.BuildRestartArguments(options, (nint)0x45AF);
        TestAssert.True(arguments.Contains("--startup", StringComparison.Ordinal), "Expected startup flag in elevated relaunch arguments.");
        TestAssert.True(arguments.Contains("--safe-mode", StringComparison.Ordinal), "Expected safe-mode flag in elevated relaunch arguments.");
        TestAssert.True(arguments.Contains("--delay=12", StringComparison.Ordinal), "Expected delay flag in elevated relaunch arguments.");
        TestAssert.True(arguments.Contains("--rehide=0x45AF", StringComparison.Ordinal), "Expected pending hide handle in elevated relaunch arguments.");
    }

    private static void SettingsStoreHonorsEnvironmentOverridePath()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ph-store-test-{Guid.NewGuid():N}");
        var original = Environment.GetEnvironmentVariable("PROGRAMHIDER_DATA");
        try
        {
            Environment.SetEnvironmentVariable("PROGRAMHIDER_DATA", tempRoot);
            var store = new JsonSettingsStore<AppSettings>("ProgramHider");
            var expectedPath = Path.Combine(tempRoot, "settings.json");
            TestAssert.Equal(expectedPath, store.SettingsPath, "Expected the PROGRAMHIDER_DATA override to redirect SettingsPath.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("PROGRAMHIDER_DATA", original);
        }
    }

    private static void PinSecurityHashesAndVerifiesSecrets()
    {
        var hash = PinSecurity.HashSecret("secret");
        TestAssert.True(!string.IsNullOrWhiteSpace(hash), "Expected a non-empty hash.");
        TestAssert.True(PinSecurity.VerifySecret("secret", hash), "Expected the hash to verify.");
        TestAssert.False(PinSecurity.VerifySecret("wrong", hash), "Expected the wrong secret not to verify.");
    }

    private static void HotkeySettingsNormalizesDefaults()
    {
        var settings = new HotkeySettings
        {
            Control = false,
            Shift = false,
            Alt = false,
            Windows = false,
            Key = Keys.None
        };

        settings.Normalize();

        TestAssert.True(settings.Control, "Expected Normalize to force at least one modifier.");
        TestAssert.Equal(Keys.H, settings.Key, "Expected Normalize to restore the default key.");
    }

    private static void WindowCatalogFindByTitleRespectsProcessFilter()
    {
        var fakePlatform = new FakeWindowPlatform(
            new FakeWindowState(
                new NativeWindowSnapshot((nint)1, "Program Hider Smoke Window", "SmokeClass", "ProgramHider.SmokeWindow", 1, 0, 0, false),
                IsVisible: true,
                IsAlive: true),
            new FakeWindowState(
                new NativeWindowSnapshot((nint)2, "Program Hider Smoke Window", "FirefoxClass", "firefox", 2, 0, 0, false),
                IsVisible: true,
                IsAlive: true));

        var match = WindowCatalog.FindFirstByTitleContains(fakePlatform, "Smoke Window", "ProgramHider.SmokeWindow");
        TestAssert.NotNull(match, "Expected filtered window match.");
        TestAssert.Equal((nint)1, match!.Value.Handle, "Expected the filtered process match to be selected.");

        var noMatch = WindowCatalog.FindFirstByTitleContains(fakePlatform, "Nope", "ProgramHider.SmokeWindow");
        TestAssert.True(noMatch is null, "Expected null when no window matches the requested title.");
    }

    private static void WindowCatalogEnumerateFiltersCandidates()
    {
        var fakePlatform = new FakeWindowPlatform(
            new FakeWindowState(
                new NativeWindowSnapshot((nint)1, "Visible Window", "NormalClass", "powershell", 1, 0, 0, false),
                IsVisible: true,
                IsAlive: true),
            new FakeWindowState(
                new NativeWindowSnapshot((nint)2, "Hidden Tracked", "NormalClass", "powershell", 2, 0, 0, false),
                IsVisible: true,
                IsAlive: true),
            new FakeWindowState(
                new NativeWindowSnapshot((nint)3, "Tool Window", "NormalClass", "powershell", 3, 0, NativeMethods.WS_EX_TOOLWINDOW, false),
                IsVisible: true,
                IsAlive: true),
            new FakeWindowState(
                new NativeWindowSnapshot((nint)4, "Owned Window", "NormalClass", "powershell", 4, (nint)123, 0, false),
                IsVisible: true,
                IsAlive: true),
            new FakeWindowState(
                new NativeWindowSnapshot((nint)5, "Progman", "Progman", "explorer", 5, 0, 0, false),
                IsVisible: true,
                IsAlive: true));

        var windows = WindowCatalog.EnumerateManageableWindows(fakePlatform, new[] { (nint)2 }, excludedHandle: (nint)99);

        TestAssert.Equal(1, windows.Count, "Expected only one manageable candidate.");
        TestAssert.Equal((nint)1, windows[0].Handle, "Expected the visible top-level window to remain.");
    }

    private static void ActiveWindowTrackerFallsBackToLastTrackedWindow()
    {
        var fakePlatform = new FakeWindowPlatform(
            new FakeWindowState(
                new NativeWindowSnapshot((nint)10, "Tracked Window", "NormalClass", "powershell", 10, 0, 0, false),
                IsVisible: true,
                IsAlive: true),
            new FakeWindowState(
                new NativeWindowSnapshot((nint)11, "Tray Host", "Shell_TrayWnd", "explorer", 11, 0, 0, false),
                IsVisible: true,
                IsAlive: true));
        var tracker = new ActiveWindowTracker(fakePlatform);

        fakePlatform.SetForegroundWindowForTest((nint)10);
        var captured = tracker.CaptureCurrentSnapshot(WindowCatalog.IsManageableWindow);
        TestAssert.NotNull(captured, "Expected the first active window to be captured.");

        fakePlatform.SetForegroundWindowForTest((nint)11);
        var resolved = tracker.ResolveSnapshot(WindowCatalog.IsManageableWindow);
        TestAssert.NotNull(resolved, "Expected fallback to the last tracked window.");
        TestAssert.Equal((nint)10, resolved!.Value.Handle, "Expected the last tracked window handle.");
    }

    private static void ActiveWindowTrackerClearsStaleFallback()
    {
        var fakePlatform = new FakeWindowPlatform(
            new FakeWindowState(
                new NativeWindowSnapshot((nint)10, "Tracked Window", "NormalClass", "powershell", 10, 0, 0, false),
                IsVisible: true,
                IsAlive: true),
            new FakeWindowState(
                new NativeWindowSnapshot((nint)11, "Tray Host", "Shell_TrayWnd", "explorer", 11, 0, 0, false),
                IsVisible: true,
                IsAlive: true));
        var tracker = new ActiveWindowTracker(fakePlatform);

        fakePlatform.SetForegroundWindowForTest((nint)10);
        tracker.CaptureCurrentSnapshot(WindowCatalog.IsManageableWindow);
        fakePlatform.SetAlive((nint)10, false);
        fakePlatform.SetForegroundWindowForTest((nint)11);

        var resolved = tracker.ResolveSnapshot(WindowCatalog.IsManageableWindow);
        TestAssert.True(resolved is null, "Expected stale tracked windows to be cleared.");
    }

    private static void ActiveWindowTrackerTracksExplicitHandles()
    {
        var fakePlatform = new FakeWindowPlatform(
            new FakeWindowState(
                new NativeWindowSnapshot((nint)21, "Tracked Window", "NormalClass", "powershell", 21, 0, 0, false),
                IsVisible: true,
                IsAlive: true),
            new FakeWindowState(
                new NativeWindowSnapshot((nint)22, "Menu Host", "Shell_TrayWnd", "explorer", 22, 0, 0, false),
                IsVisible: true,
                IsAlive: true));
        var tracker = new ActiveWindowTracker(fakePlatform);

        var tracked = tracker.CaptureSnapshotForHandle((nint)21, WindowCatalog.IsManageableWindow);
        TestAssert.NotNull(tracked, "Expected the explicit foreground handle to be tracked.");

        fakePlatform.SetForegroundWindowForTest((nint)22);
        var resolved = tracker.ResolveSnapshot(WindowCatalog.IsManageableWindow);
        TestAssert.NotNull(resolved, "Expected the last explicit handle to remain available.");
        TestAssert.Equal((nint)21, resolved!.Value.Handle, "Expected the explicitly tracked handle to be returned.");
    }

    private static void WindowHideServiceExercisesFakePlatform()
    {
        var handle = (nint)99;
        var fakePlatform = new FakeWindowPlatform(
            new FakeWindowState(
                new NativeWindowSnapshot(handle, "Adguard Home", "ConsoleWindowClass", "powershell", 99, 0, 0, false),
                IsVisible: true,
                IsAlive: true));
        var service = new WindowHideService(fakePlatform);
        var hiddenWindows = new Dictionary<nint, HiddenWindow>();

        var hidden = service.TryHideWindow(handle, 0, hiddenWindows, null, out var hiddenWindow);
        TestAssert.True(hidden, "Expected hide to succeed.");
        TestAssert.True(hiddenWindows.ContainsKey(handle), "Expected hidden state to be tracked.");
        TestAssert.NotNull(hiddenWindow, "Expected hidden window snapshot.");
        TestAssert.False(fakePlatform.IsVisible(handle), "Expected fake window to be hidden.");

        var restored = service.TryRestoreWindow(handle, hiddenWindows, restoreWithoutFocus: false, out var restoredWindow);
        TestAssert.True(restored, "Expected restore to succeed.");
        TestAssert.NotNull(restoredWindow, "Expected restored window state.");
        TestAssert.True(fakePlatform.IsVisible(handle), "Expected fake window to be visible again.");

        fakePlatform.SetAlive(handle, false);
        hiddenWindows[handle] = hiddenWindow!;
        var pruned = service.PruneDeadWindows(hiddenWindows);
        TestAssert.Equal(1, pruned, "Expected one dead window to be pruned.");
        TestAssert.False(hiddenWindows.ContainsKey(handle), "Expected dead window to be removed.");
    }

    private static void TestProgramHiderStartupRegistration()
    {
        var reg = new WindowsAppCore.RunKeyStartupRegistration(
            "ProgramHider",
            Application.ExecutablePath,
            "--startup --delay=0");

        try
        {
            reg.Register();
            TestAssert.True(reg.IsRegistered, "Expected Run key value to exist after Register.");
            reg.Unregister();
            TestAssert.False(reg.IsRegistered, "Expected Run key value to be absent after Unregister.");
        }
        finally
        {
            reg.Unregister();
        }
    }

    private static void WindowHideServiceRejectsExcludedAndDuplicateWindows()
    {
        var handle = (nint)77;
        var fakePlatform = new FakeWindowPlatform(
            new FakeWindowState(
                new NativeWindowSnapshot(handle, "Visible Window", "NormalClass", "powershell", 77, 0, 0, false),
                IsVisible: true,
                IsAlive: true));
        var service = new WindowHideService(fakePlatform);
        var hiddenWindows = new Dictionary<nint, HiddenWindow>();

        var excluded = service.TryHideWindow(handle, handle, hiddenWindows, null, out _);
        TestAssert.False(excluded, "Expected excluded handles not to hide.");

        var firstHide = service.TryHideWindow(handle, 0, hiddenWindows, null, out _);
        var duplicateHide = service.TryHideWindow(handle, 0, hiddenWindows, null, out _);
        TestAssert.True(firstHide, "Expected the first hide to work.");
        TestAssert.False(duplicateHide, "Expected duplicate hide attempts to be rejected.");
    }
}

internal sealed class TestRunner
{
    private int _passed;
    private int _failed;

    public void Run(string name, Action test)
    {
        try
        {
            test();
            _passed++;
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception exception)
        {
            _failed++;
            Console.WriteLine($"FAIL {name}");
            Console.WriteLine(exception.Message);
        }
    }

    public int Finish()
    {
        Console.WriteLine($"RESULT passed={_passed} failed={_failed}");
        return _failed == 0 ? 0 : 1;
    }
}

internal static class TestAssert
{
    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void False(bool condition, string message)
    {
        if (condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected={expected} Actual={actual}");
        }
    }

    public static void NotNull(object? value, string message)
    {
        if (value is null)
        {
            throw new InvalidOperationException(message);
        }
    }
}

internal sealed record FakeWindowState(
    NativeWindowSnapshot Snapshot,
    bool IsVisible,
    bool IsAlive,
    NativeMethods.WindowPlacement? Placement = null,
    string MonitorDeviceName = "DISPLAY1");

internal sealed class FakeWindowPlatform : IWindowPlatform
{
    private readonly Dictionary<nint, FakeWindowState> _windows;
    private nint _foregroundHandle;

    public FakeWindowPlatform(params FakeWindowState[] windows)
    {
        _windows = windows.ToDictionary(window => window.Snapshot.Handle);
        _foregroundHandle = 0;
        foreach (var window in windows)
        {
            if (window.IsAlive && window.IsVisible)
            {
                _foregroundHandle = window.Snapshot.Handle;
                break;
            }
        }
    }

    public IReadOnlyList<NativeWindowSnapshot> EnumerateTopLevelWindows()
    {
        return _windows.Values
            .Where(window => window.IsAlive && window.IsVisible)
            .Select(window => window.Snapshot)
            .ToArray();
    }

    public NativeWindowSnapshot? TryCreateWindowSnapshot(nint handle)
    {
        if (!_windows.TryGetValue(handle, out var window) || !window.IsAlive || !window.IsVisible)
        {
            return null;
        }

        return window.Snapshot;
    }

    public NativeMethods.WindowPlacement? TryGetWindowPlacement(nint handle)
    {
        return _windows.TryGetValue(handle, out var window) ? window.Placement : null;
    }

    public bool TrySetWindowPlacement(nint handle, NativeMethods.WindowPlacement placement)
    {
        if (!_windows.TryGetValue(handle, out var window))
        {
            return false;
        }

        _windows[handle] = window with { Placement = placement };
        return true;
    }

    public string TryGetMonitorDeviceNameForWindow(nint handle)
    {
        return _windows.TryGetValue(handle, out var window) ? window.MonitorDeviceName : string.Empty;
    }

    public bool ShowWindow(nint handle, int command)
    {
        if (!_windows.TryGetValue(handle, out var window) || !window.IsAlive)
        {
            return false;
        }

        var isVisible = command != NativeMethods.SW_HIDE;
        _windows[handle] = window with { IsVisible = isVisible };
        return true;
    }

    public bool SetForegroundWindow(nint handle)
    {
        return _windows.ContainsKey(handle);
    }

    public bool IsWindow(nint handle)
    {
        return _windows.TryGetValue(handle, out var window) && window.IsAlive;
    }

    public nint GetForegroundWindow()
    {
        if (_foregroundHandle != 0 &&
            _windows.TryGetValue(_foregroundHandle, out var foregroundWindow) &&
            foregroundWindow.IsAlive &&
            foregroundWindow.IsVisible)
        {
            return _foregroundHandle;
        }

        var window = _windows.Values.FirstOrDefault(window => window.IsAlive && window.IsVisible);
        return window is null ? 0 : window.Snapshot.Handle;
    }

    public bool IsVisible(nint handle)
    {
        return _windows.TryGetValue(handle, out var window) && window.IsVisible;
    }

    public void SetAlive(nint handle, bool isAlive)
    {
        if (_windows.TryGetValue(handle, out var window))
        {
            _windows[handle] = window with { IsAlive = isAlive };
        }
    }

    public void SetForegroundWindowForTest(nint handle)
    {
        _foregroundHandle = handle;
    }
}

internal static class NativeInput
{
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public static void SendHotkey(bool control, bool shift, bool alt, bool windows, Keys key)
    {
        var inputs = new List<INPUT>();

        AppendModifier(inputs, control, Keys.ControlKey, keyUp: false);
        AppendModifier(inputs, shift, Keys.ShiftKey, keyUp: false);
        AppendModifier(inputs, alt, Keys.Menu, keyUp: false);
        AppendModifier(inputs, windows, Keys.LWin, keyUp: false);

        inputs.Add(CreateKeyInput((ushort)key, 0));
        inputs.Add(CreateKeyInput((ushort)key, KEYEVENTF_KEYUP));

        AppendModifier(inputs, windows, Keys.LWin, keyUp: true);
        AppendModifier(inputs, alt, Keys.Menu, keyUp: true);
        AppendModifier(inputs, shift, Keys.ShiftKey, keyUp: true);
        AppendModifier(inputs, control, Keys.ControlKey, keyUp: true);

        var sent = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
        if (sent != (uint)inputs.Count)
        {
            throw new InvalidOperationException($"SendInput failed. Sent={sent} Expected={inputs.Count}");
        }
    }

    private static void AppendModifier(List<INPUT> inputs, bool enabled, Keys key, bool keyUp)
    {
        if (!enabled)
        {
            return;
        }

        inputs.Add(CreateKeyInput((ushort)key, keyUp ? KEYEVENTF_KEYUP : 0));
    }

    private static INPUT CreateKeyInput(ushort virtualKey, uint flags)
    {
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = virtualKey,
                    dwFlags = flags
                }
            }
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, INPUT[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nuint dwExtraInfo;
    }
}

internal static class NativeWindowProbe
{
    public static nint FindWindowByCaption(string caption)
    {
        return FindWindowW(null, caption);
    }

    public static bool PostHotkeyMessage(nint handle, int hotkeyId)
    {
        return PostMessageW(handle, NativeMethods.WM_HOTKEY, (nuint)hotkeyId, 0);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint FindWindowW(string? className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(nint handle, uint message, nuint handleParam, nint lParam);
}
