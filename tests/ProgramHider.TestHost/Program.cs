using ProgramHider;
using System.Text;
using System.Windows.Forms;

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

        return Task.FromResult(RunUnitTests());
    }

    private static int RunUnitTests()
    {
        var runner = new TestRunner();
        runner.Run("WindowRule matches process title and class", WindowRuleMatchesExpectedFields);
        runner.Run("WindowRule evaluator merges flags", WindowRuleEvaluatorMergesFlags);
        runner.Run("AppSettings normalize migrates legacy process rules", AppSettingsNormalizeMigratesLegacyRules);
        runner.Run("AppSettings normalize preserves rule-level PIN hash", AppSettingsNormalizePreservesRuleProtectedPin);
        runner.Run("StartupOptions parse handles startup safe mode and delay", StartupOptionsParsesFlags);
        runner.Run("WindowCatalog find by title respects process filter and null on miss", WindowCatalogFindByTitleRespectsProcessFilter);
        runner.Run("WindowHideService hide restore and prune works", WindowHideServiceExercisesFakePlatform);
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
            0,
            0,
            false);
        var nonMatchingWindow = matchingWindow with { ProcessName = "notepad" };

        TestAssert.True(rule.Matches(matchingWindow), "Expected the rule to match the target window.");
        TestAssert.False(rule.Matches(nonMatchingWindow), "Expected the rule not to match the different process.");
    }

    private static void WindowRuleEvaluatorMergesFlags()
    {
        var window = new NativeWindowSnapshot((nint)7, "Adguard Home", "ConsoleWindowClass", "powershell", 0, 0, false);
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
        var options = StartupOptions.Parse(new[] { "--startup", "--delay=42", "--safe-mode" });

        TestAssert.True(options.IsStartupLaunch, "Expected startup flag.");
        TestAssert.Equal(42, options.DelaySeconds, "Expected delay to parse.");
        TestAssert.True(options.SafeMode, "Expected safe mode flag.");
    }

    private static void WindowCatalogFindByTitleRespectsProcessFilter()
    {
        var fakePlatform = new FakeWindowPlatform(
            new FakeWindowState(
                new NativeWindowSnapshot((nint)1, "Program Hider Smoke Window", "SmokeClass", "ProgramHider.SmokeWindow", 0, 0, false),
                IsVisible: true,
                IsAlive: true),
            new FakeWindowState(
                new NativeWindowSnapshot((nint)2, "Program Hider Smoke Window", "FirefoxClass", "firefox", 0, 0, false),
                IsVisible: true,
                IsAlive: true));

        var match = WindowCatalog.FindFirstByTitleContains(fakePlatform, "Smoke Window", "ProgramHider.SmokeWindow");
        TestAssert.NotNull(match, "Expected filtered window match.");
        TestAssert.Equal((nint)1, match!.Value.Handle, "Expected the filtered process match to be selected.");

        var noMatch = WindowCatalog.FindFirstByTitleContains(fakePlatform, "Nope", "ProgramHider.SmokeWindow");
        TestAssert.True(noMatch is null, "Expected null when no window matches the requested title.");
    }

    private static void WindowHideServiceExercisesFakePlatform()
    {
        var handle = (nint)99;
        var fakePlatform = new FakeWindowPlatform(
            new FakeWindowState(
                new NativeWindowSnapshot(handle, "Adguard Home", "ConsoleWindowClass", "powershell", 0, 0, false),
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

    public FakeWindowPlatform(params FakeWindowState[] windows)
    {
        _windows = windows.ToDictionary(window => window.Snapshot.Handle);
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
}
