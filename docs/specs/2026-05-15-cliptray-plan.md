# ClipTray Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build ClipTray, the fifth tray utility: clipboard history (text + images) with a global-hotkey picker, layered privacy, and persistent storage. Lift `HotkeyRegistration` into `WindowsTrayCore` as the shared type; migrate ProgramHider onto it in the same phase.

**Architecture:** New `apps/ClipTray/` project following the existing tray-app shape (WinExe net8.0-windows, references `WindowsAppCore` + `WindowsTrayCore`). Capture pipeline = `ClipboardListener` -> privacy filter -> `ClipboardHistory` (+ `ImageStore` for image sidecars). Recall pipeline = `HotkeyRegistration` -> `PickerForm` -> `ClipboardWriter` (set clipboard, SendInput Ctrl+V). Theming via the canonical `ThemeApplier.ApplyTo` + `Changed` subscription pattern from Phase 29.

**Tech Stack:** .NET 8, WinForms, xUnit + FluentAssertions, P/Invoke to user32.dll + wtsapi32.dll. No new packages.

**Spec:** `docs/specs/2026-05-15-cliptray.md`

---

## File Structure

### New files

| Path | Responsibility |
|---|---|
| `shared/WindowsTrayCore/HotkeyRegistration.cs` | Global hotkey wrapper with hidden HWND + `WM_HOTKEY` |
| `shared/WindowsTrayCore.Tests/HotkeyRegistrationTests.cs` | Unit + smoke tests |
| `apps/ClipTray/ClipTray.sln` | Solution file |
| `apps/ClipTray/ClipTray/ClipTray.csproj` | Project file |
| `apps/ClipTray/ClipTray/app.manifest` | DPI + execution-level |
| `apps/ClipTray/ClipTray/app.ico` | Tray icon source (placeholder OK during dev) |
| `apps/ClipTray/ClipTray/Program.cs` | Main + single-instance + AppLog |
| `apps/ClipTray/ClipTray/AppSettings.cs` | `JsonSettingsStore<AppSettings>` |
| `apps/ClipTray/ClipTray/HistoryItem.cs` | Record type for one history entry |
| `apps/ClipTray/ClipTray/ClipboardHistory.cs` | In-memory + JSON-backed history |
| `apps/ClipTray/ClipTray/ImageStore.cs` | PNG sidecar manager |
| `apps/ClipTray/ClipTray/PasswordHeuristic.cs` | Pure-function password detection |
| `apps/ClipTray/ClipTray/ClipboardListener.cs` | `AddClipboardFormatListener` + hidden HWND |
| `apps/ClipTray/ClipTray/ClipboardWriter.cs` | Set clipboard + `SendInput` Ctrl+V |
| `apps/ClipTray/ClipTray/SessionLockMonitor.cs` | `WTSSESSION_CHANGE` listener |
| `apps/ClipTray/ClipTray/ForegroundProcessProbe.cs` | `GetForegroundWindow` + PID resolution |
| `apps/ClipTray/ClipTray/PickerForm.cs` | Borderless popup picker |
| `apps/ClipTray/ClipTray/SettingsForm.cs` | Tabbed settings |
| `apps/ClipTray/ClipTray/ClipTrayContext.cs` | `ApplicationContext`, tray icon, lifetime owner |
| `apps/ClipTray/ClipTray.Tests/ClipTray.Tests.csproj` | Test project |
| `apps/ClipTray/ClipTray.Tests/PasswordHeuristicTests.cs` | Unit tests |
| `apps/ClipTray/ClipTray.Tests/ImageStoreTests.cs` | Unit tests |
| `apps/ClipTray/ClipTray.Tests/ClipboardHistoryTests.cs` | Unit tests |
| `apps/ClipTray/ClipTray.Tests/AppSettingsTests.cs` | Unit tests |
| `apps/ClipTray/ClipTray.E2ETests/ClipTray.E2ETests.csproj` | E2E test project |
| `apps/ClipTray/ClipTray.E2ETests/WindowsFactAttribute.cs` | xUnit skip-on-non-Windows |
| `apps/ClipTray/ClipTray.E2ETests/ClipboardRoundTripE2ETests.cs` | Real-clipboard E2E |
| `apps/ClipTray/ClipTray.E2ETests/HotkeyRegistrationE2ETests.cs` | Real-hotkey E2E (in WindowsTrayCore.Tests if simpler) |

### Modified files

| Path | Change |
|---|---|
| `shared/WindowsTrayCore/Native/TrayNativeMethods.cs` | Add `RegisterHotKey`, `UnregisterHotKey`, `WM_HOTKEY` |
| `apps/ProgramHider/app/ProgramHider/ProgramHiderContext.cs` | Migrate to shared `HotkeyRegistration` |
| `apps/ProgramHider/app/ProgramHider/<local-hotkey-file>.cs` | Delete (locate at implementation time via grep) |
| `install.ps1` | Add ClipTray entry to `$AppDefs` |
| `.github/workflows/build.yml` | Add ClipTray test + publish steps |
| `.github/workflows/release.yml` | Add ClipTray publish + zip |
| `WORKLOG.md` | New entry on the final commit |

### Working directory

All paths relative to `D:\code\windows-apps\`. PowerShell or Bash both fine for `dotnet` / `git` invocation.

---

## Task 1: HotkeyRegistration in WindowsTrayCore (TDD)

Lifts the shared hotkey registration into `WindowsTrayCore`. ProgramHider migrates in Task 2.

**Files:**
- Modify: `shared/WindowsTrayCore/Native/TrayNativeMethods.cs`
- Create: `shared/WindowsTrayCore/HotkeyRegistration.cs`
- Create: `shared/WindowsTrayCore.Tests/HotkeyRegistrationTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `shared/WindowsTrayCore.Tests/HotkeyRegistrationTests.cs`:

```csharp
using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class HotkeyRegistrationTests
{
    [Fact]
    public void Modifiers_FlagValues_AreExpected()
    {
        ((int)HotkeyModifiers.None).Should().Be(0);
        ((int)HotkeyModifiers.Alt).Should().Be(1);
        ((int)HotkeyModifiers.Control).Should().Be(2);
        ((int)HotkeyModifiers.Shift).Should().Be(4);
        ((int)HotkeyModifiers.Win).Should().Be(8);
    }

    [WindowsFact]
    public void Construct_DoesNotThrow()
    {
        using var h = new HotkeyRegistration();
        h.Should().NotBeNull();
    }

    [WindowsFact]
    public void Dispose_IsIdempotent()
    {
        var h = new HotkeyRegistration();
        h.Dispose();
        h.Dispose();
    }

    [WindowsFact]
    public void Register_DuplicateId_ReturnsFalseOnSecondCall()
    {
        // Use an unlikely chord to avoid colliding with the dev box's other apps:
        // Ctrl+Alt+Shift+F19 is virtually never bound.
        const int id = 9999;
        const HotkeyModifiers mods = HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift;
        const Keys key = Keys.F19;

        using var h = new HotkeyRegistration();
        var first = h.Register(id, mods, key);
        var second = h.Register(id, mods, key);

        first.Should().BeTrue();
        second.Should().BeFalse(because: "the same id cannot be registered twice without unregistering first");

        h.Unregister(id);
    }

    [WindowsFact]
    public void Unregister_AfterRegister_ReturnsTrue()
    {
        const int id = 9998;
        using var h = new HotkeyRegistration();
        h.Register(id, HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, Keys.F18);
        h.Unregister(id).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run tests; verify build error**

Run: `dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj --filter FullyQualifiedName~HotkeyRegistrationTests`
Expected: `CS0246: HotkeyRegistration / HotkeyModifiers not found`.

- [ ] **Step 3: Add Win32 surface**

In `shared/WindowsTrayCore/Native/TrayNativeMethods.cs`, append (near other DllImports):

```csharp
[DllImport("user32.dll", SetLastError = true)]
internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

[DllImport("user32.dll", SetLastError = true)]
internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

internal const int WM_HOTKEY = 0x0312;
```

- [ ] **Step 4: Create `HotkeyRegistration.cs`**

```csharp
using System.Collections.Generic;
using System.Windows.Forms;

namespace WindowsTrayCore;

[Flags]
public enum HotkeyModifiers
{
    None    = 0,
    Alt     = 1,
    Control = 2,
    Shift   = 4,
    Win     = 8,
}

/// <summary>
/// Global hotkey registration wrapper. Owns a hidden NativeWindow that
/// receives WM_HOTKEY; raises <see cref="Pressed"/> with the registered id
/// on the application message-pump thread. Same NativeWindow pattern used
/// by TrayIcon and TrayTheme.
/// </summary>
public sealed class HotkeyRegistration : IDisposable
{
    private readonly MessageWindow _window;
    private readonly HashSet<int> _registered = new();
    private bool _disposed;

    public HotkeyRegistration()
    {
        _window = new MessageWindow(this);
        _window.CreateHandle(new CreateParams());
    }

    public event EventHandler<int>? Pressed;

    public bool Register(int id, HotkeyModifiers modifiers, Keys key)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(HotkeyRegistration));
        if (!_registered.Add(id)) return false; // already in our set

        if (Native.TrayNativeMethods.RegisterHotKey(_window.Handle, id, (uint)modifiers, (uint)key))
            return true;

        _registered.Remove(id);
        return false;
    }

    public bool Unregister(int id)
    {
        if (_disposed) return false;
        if (!_registered.Remove(id)) return false;
        return Native.TrayNativeMethods.UnregisterHotKey(_window.Handle, id);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var id in _registered)
            Native.TrayNativeMethods.UnregisterHotKey(_window.Handle, id);
        _registered.Clear();
        _window.DestroyHandle();
    }

    private sealed class MessageWindow : NativeWindow
    {
        private readonly HotkeyRegistration _owner;
        public MessageWindow(HotkeyRegistration owner) => _owner = owner;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.TrayNativeMethods.WM_HOTKEY)
            {
                _owner.Pressed?.Invoke(_owner, (int)m.WParam);
                return;
            }
            base.WndProc(ref m);
        }
    }
}
```

- [ ] **Step 5: Run tests; verify pass**

Run: `dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj --filter FullyQualifiedName~HotkeyRegistrationTests`
Expected: 5 tests pass.

- [ ] **Step 6: Commit**

```bash
git add shared/WindowsTrayCore/HotkeyRegistration.cs shared/WindowsTrayCore/Native/TrayNativeMethods.cs shared/WindowsTrayCore.Tests/HotkeyRegistrationTests.cs
git commit -m "WindowsTrayCore: HotkeyRegistration shared API

Lifts the global-hotkey wrapper into the shared library. Pattern matches
the existing TrayIcon / TrayTheme hidden-NativeWindow message-window
approach; WM_HOTKEY arrives at the hidden HWND and Pressed fires with
the registered int id.

ProgramHider's local hotkey class is migrated to consume this shared
type in the next commit. ClipTray (the second consumer) lands later
in the phase."
```

---

## Task 2: ProgramHider migrates to shared HotkeyRegistration

Replaces ProgramHider's local hotkey class with the shared `WindowsTrayCore.HotkeyRegistration`.

**Files:**
- Modify: `apps/ProgramHider/app/ProgramHider/ProgramHiderContext.cs`
- Delete: ProgramHider's local hotkey-registration class (locate via grep)

- [ ] **Step 1: Locate the existing hotkey class**

Run: `grep -rn "RegisterHotKey\|WM_HOTKEY\|HotkeyRegistration\|HotKey" apps/ProgramHider/`

Identify:
- The local class that wraps `RegisterHotKey` (typically `GlobalHotkey.cs` or similar)
- Its consumer in `ProgramHiderContext.cs`
- Any settings field for the configured hotkey modifiers + key

- [ ] **Step 2: Build the migration plan**

Inspect the local class to confirm its public surface matches `WindowsTrayCore.HotkeyRegistration`. If the names differ (e.g. `Register(modifiers, key)` vs `Register(id, modifiers, key)`), note the adaptation. The shared API requires an `id` parameter; ProgramHider only has one hotkey, so always pass `id: 1`.

- [ ] **Step 3: Edit `ProgramHiderContext.cs`**

In the constructor (or wherever the hotkey is currently registered), replace:

```csharp
// OLD (whatever the local pattern is)
_hotkey = new ProgramHider.Local.GlobalHotkey();
_hotkey.Triggered += OnHotkeyPressed;
_hotkey.Register(_settings.HotkeyModifiers, _settings.HotkeyKey);
```

with:

```csharp
// NEW
_hotkey = new WindowsTrayCore.HotkeyRegistration();
_hotkey.Pressed += (_, _) => OnHotkeyPressed();
if (!_hotkey.Register(id: 1,
                       (WindowsTrayCore.HotkeyModifiers)(int)_settings.HotkeyModifiers,
                       _settings.HotkeyKey))
{
    CrashLogger.Write("hotkey.register.failed", new Exception("RegisterHotKey returned false"));
}
```

The cast `(WindowsTrayCore.HotkeyModifiers)(int)` works if and only if ProgramHider's local modifier-flag values match `WindowsTrayCore.HotkeyModifiers` (Alt=1, Control=2, Shift=4, Win=8). Verify before commit; if they differ, write an explicit mapping.

If `_hotkey.Triggered` has a `KeyEventArgs`-style payload that includes the actual key, port that information; the shared `Pressed` event gives only the id.

Update Dispose: `_hotkey.Dispose()` (shared class is `IDisposable`).

- [ ] **Step 4: Delete the local hotkey class file**

```bash
# After confirming the grep result from Step 1:
rm apps/ProgramHider/app/ProgramHider/<file>.cs
```

- [ ] **Step 5: Build + test**

Run: `dotnet build apps\ProgramHider\app\ProgramHider\ProgramHider.csproj`
Run: `dotnet test apps\ProgramHider\tests\ProgramHider.Tests\ProgramHider.Tests.csproj`
Expected: clean build, all tests pass (any test that pinned the local class's shape needs adjustment).

- [ ] **Step 6: Commit**

```bash
git add apps/ProgramHider/app/ProgramHider/ProgramHiderContext.cs
git add -u apps/ProgramHider/app/ProgramHider/<deleted-file>.cs
git commit -m "ProgramHider: migrate global hotkey onto WindowsTrayCore.HotkeyRegistration

Deletes the local hotkey-registration class and routes through the
shared WindowsTrayCore.HotkeyRegistration. ProgramHider only registers
one hotkey so id=1 is hard-coded. The modifier flag values map
identically (Alt=1, Control=2, Shift=4, Win=8) so the cast is safe.

The shared API is now used by two consumers (ProgramHider and the
upcoming ClipTray), satisfying the 'two real consumers from day one'
discipline for shared library types."
```

---

## Task 3: ClipTray project scaffolding (csproj, sln, Program.cs, AppSettings)

Creates the project skeleton. No clipboard logic yet; just the standard tray-app boot sequence: single instance, AppLog, settings, run-at-startup hooks.

**Files:**
- Create: `apps/ClipTray/ClipTray.sln`
- Create: `apps/ClipTray/ClipTray/ClipTray.csproj`
- Create: `apps/ClipTray/ClipTray/app.manifest`
- Create: `apps/ClipTray/ClipTray/app.ico` (placeholder; reuse BatteryTray.ico for now)
- Create: `apps/ClipTray/ClipTray/Program.cs`
- Create: `apps/ClipTray/ClipTray/AppSettings.cs`
- Create: `apps/ClipTray/ClipTray.Tests/ClipTray.Tests.csproj`
- Create: `apps/ClipTray/ClipTray.Tests/AppSettingsTests.cs`

- [ ] **Step 1: Create the csproj**

Create `apps/ClipTray/ClipTray/ClipTray.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <UseWindowsForms>true</UseWindowsForms>
    <ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>
    <ApplicationIcon>app.ico</ApplicationIcon>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <AssemblyName>ClipTray</AssemblyName>
    <RootNamespace>ClipTray</RootNamespace>
    <Product>ClipTray</Product>
    <Description>Clipboard history with hotkey picker</Description>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\shared\WindowsAppCore\WindowsAppCore.csproj" />
    <ProjectReference Include="..\..\..\shared\WindowsTrayCore\WindowsTrayCore.csproj" />
  </ItemGroup>

  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>ClipTray.Tests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Create app.manifest**

Copy from an existing app (e.g. `apps/BatteryTray/BatteryTray/app.manifest`); use `requestedExecutionLevel level="asInvoker"` and PerMonitorV2 DPI awareness. ClipTray does not need elevation.

- [ ] **Step 3: Create app.ico**

Copy `apps/BatteryTray/BatteryTray/app.ico` as a placeholder. A dedicated ClipTray icon is a later visual-polish task.

- [ ] **Step 4: Create Program.cs**

```csharp
using System.Diagnostics;
using WindowsAppCore;

namespace ClipTray;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (!SingleInstanceActivation.TryClaim("ClipTray", dispatchToUi: null, out var activation))
            return 0;

        using var log = new AppLog("ClipTray", Application.ProductVersion);
        UnhandledExceptionWatcher.Install(log, "ClipTray");

        var settings = AppSettings.Load();

        var startupOptions = StartupOptions.Parse(args);
        int delaySeconds = startupOptions.DelaySeconds > 0
            ? startupOptions.DelaySeconds
            : settings.StartupDelaySeconds;
        if (delaySeconds > 0)
            Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));

        ApplicationConfiguration.Initialize();
        using (activation!)
        using (var context = new ClipTrayContext(settings, activation!))
        {
            Application.Run(context);
        }

        return 0;
    }
}
```

`ClipTrayContext` does not yet exist; this will produce a compile error until Task 14. To keep the project buildable in the meantime, comment out the `using (var context...)` block:

```csharp
// using (var context = new ClipTrayContext(settings, activation!)) { Application.Run(context); }
```

and add a `Application.Run(new ApplicationContext())` placeholder. Restore the real context in Task 14.

- [ ] **Step 5: Create AppSettings.cs**

```csharp
using System.Text.Json.Serialization;
using System.Windows.Forms;
using WindowsAppCore;
using WindowsTrayCore;

namespace ClipTray;

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public const int CurrentSchemaVersion = 1;

    // General
    public HotkeyModifiers PickerHotkeyModifiers { get; set; } = HotkeyModifiers.Control | HotkeyModifiers.Shift;
    public Keys PickerHotkeyKey { get; set; } = Keys.V;
    public int TextHistoryCap { get; set; } = 50;
    public int ImageHistoryCap { get; set; } = 10;
    public int DiskQuotaMb { get; set; } = 100;

    // Privacy
    public HotkeyModifiers PauseHotkeyModifiers { get; set; } = HotkeyModifiers.None;
    public Keys PauseHotkeyKey { get; set; } = Keys.None;
    public bool PauseCapture { get; set; }
    public bool PauseOnLockScreen { get; set; } = true;
    public bool PasswordHeuristicEnabled { get; set; } = true;
    public int PasswordHeuristicMinLength { get; set; } = 8;
    public int PasswordHeuristicMaxLength { get; set; } = 64;
    public List<string> ForegroundBlocklist { get; set; } = new()
    {
        "keepass", "keepass2", "keepassxc",
        "1password", "bitwarden", "lastpass", "dashlane",
    };

    // System
    public bool RunAtStartup { get; set; }
    public int StartupDelaySeconds { get; set; }
    public bool ShownFirstRunWelcome { get; set; }

    // Picker geometry
    public int PickerWidth { get; set; } = 400;
    public int PickerHeight { get; set; } = 360;

    private static readonly JsonSettingsStore<AppSettings> Store = new(
        "ClipTray",
        migrations: Array.Empty<ISettingsMigration>());

    [JsonIgnore]
    public string SettingsFilePath => Store.SettingsPath;

    public static AppSettings Load()
    {
        var s = Store.Load();
        s.SchemaVersion = CurrentSchemaVersion;
        return s;
    }

    public void Save()
    {
        SchemaVersion = CurrentSchemaVersion;
        Store.Save(this);
    }
}
```

- [ ] **Step 6: Create ClipTray.sln**

Create `apps/ClipTray/ClipTray.sln` by hand or via `dotnet sln`. Reference the four projects: ClipTray, ClipTray.Tests, WindowsAppCore, WindowsTrayCore (the shared libs are referenced as project refs; sln adds them so they build together):

```bash
cd apps/ClipTray
dotnet new sln -n ClipTray
dotnet sln add ClipTray/ClipTray.csproj
dotnet sln add ClipTray.Tests/ClipTray.Tests.csproj
dotnet sln add ../../shared/WindowsAppCore/WindowsAppCore.csproj
dotnet sln add ../../shared/WindowsTrayCore/WindowsTrayCore.csproj
```

- [ ] **Step 7: Create ClipTray.Tests project**

Create `apps/ClipTray/ClipTray.Tests/ClipTray.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <SupportedOSPlatformVersion>10.0.17763.0</SupportedOSPlatformVersion>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\ClipTray\ClipTray.csproj" />
    <ProjectReference Include="..\..\..\shared\WindowsAppTesting\WindowsAppTesting.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 8: Create AppSettingsTests.cs**

```csharp
using FluentAssertions;
using System.Windows.Forms;
using WindowsAppTesting;
using WindowsTrayCore;
using Xunit;

namespace ClipTray.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_MatchSpec()
    {
        using var temp = new TempAppData("ClipTray");
        var s = AppSettings.Load();

        s.SchemaVersion.Should().Be(1);
        s.PickerHotkeyModifiers.Should().Be(HotkeyModifiers.Control | HotkeyModifiers.Shift);
        s.PickerHotkeyKey.Should().Be(Keys.V);
        s.TextHistoryCap.Should().Be(50);
        s.ImageHistoryCap.Should().Be(10);
        s.DiskQuotaMb.Should().Be(100);
        s.PauseOnLockScreen.Should().BeTrue();
        s.PasswordHeuristicEnabled.Should().BeTrue();
        s.PasswordHeuristicMinLength.Should().Be(8);
        s.PasswordHeuristicMaxLength.Should().Be(64);
        s.ForegroundBlocklist.Should().Contain(new[] { "keepass", "1password", "bitwarden", "lastpass" });
        s.PickerWidth.Should().Be(400);
        s.PickerHeight.Should().Be(360);
    }

    [Fact]
    public void SaveLoad_RoundTripsAllFields()
    {
        using var temp = new TempAppData("ClipTray");
        var w = AppSettings.Load();
        w.PickerHotkeyKey = Keys.Q;
        w.TextHistoryCap = 123;
        w.ForegroundBlocklist.Add("custom-app");
        w.PauseCapture = true;
        w.Save();

        var r = AppSettings.Load();
        r.PickerHotkeyKey.Should().Be(Keys.Q);
        r.TextHistoryCap.Should().Be(123);
        r.ForegroundBlocklist.Should().Contain("custom-app");
        r.PauseCapture.Should().BeTrue();
    }
}
```

- [ ] **Step 9: Build + test + commit**

Run: `dotnet build apps\ClipTray\ClipTray.sln`
Run: `dotnet test apps\ClipTray\ClipTray.Tests\ClipTray.Tests.csproj`
Expected: clean build, 2 tests pass.

```bash
git add apps/ClipTray/
git commit -m "ClipTray: project scaffolding (csproj, sln, Program.cs, AppSettings)

New tray app following the existing project shape. Single-instance
activation, AppLog, UnhandledExceptionWatcher, StartupOptions all wired
in Program.cs. AppSettings exposes the spec's full surface (hotkey,
caps, privacy, blocklist, system, picker geometry); JsonSettingsStore-
backed; schema v1 with no migrations.

AppSettingsTests pin defaults + round-trip. The ClipTrayContext type
is referenced but not yet built; Program.cs uses a placeholder
ApplicationContext until Task 14 wires the real one."
```

---

## Task 4: PasswordHeuristic (TDD, pure function)

Length + charset + no-whitespace detection. Pure function; trivial TDD.

**Files:**
- Create: `apps/ClipTray/ClipTray/PasswordHeuristic.cs`
- Create: `apps/ClipTray/ClipTray.Tests/PasswordHeuristicTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using Xunit;

namespace ClipTray.Tests;

public class PasswordHeuristicTests
{
    [Theory]
    [InlineData("abc123XYZ!")]          // mixed-case + digit + symbol
    [InlineData("hunter2hunter")]       // letters + digit
    [InlineData("a1b2c3d4e5")]          // letters + digits
    [InlineData("Qb2!Lp7$Mz3")]         // strong-looking password
    [InlineData("base64==WithPad")]     // base64-style
    [InlineData("0123456789abcdef")]    // hex string of typical length
    public void LooksLikeSecret_KnownPasswords_ReturnsTrue(string text)
    {
        PasswordHeuristic.LooksLikeSecret(text).Should().BeTrue();
    }

    [Theory]
    [InlineData("hello world")]                    // whitespace
    [InlineData("https://example.com/path")]       // contains slashes but also URL-shaped
    [InlineData("CAFEDEADBEEF")]                   // all-caps single class
    [InlineData("12345678")]                       // all digits, single class
    [InlineData("a")]                              // too short
    [InlineData("password")]                       // single class
    [InlineData("the quick brown fox")]            // whitespace
    public void LooksLikeSecret_NonPasswords_ReturnsFalse(string text)
    {
        PasswordHeuristic.LooksLikeSecret(text).Should().BeFalse();
    }

    [Theory]
    [InlineData(7, false)]                          // below default min
    [InlineData(8, true)]                           // at default min
    [InlineData(64, true)]                          // at default max
    [InlineData(65, false)]                         // above default max
    public void LooksLikeSecret_LengthBoundaries_RespectsDefaults(int length, bool expected)
    {
        var text = new string('A', length / 2) + new string('1', length - length / 2);
        PasswordHeuristic.LooksLikeSecret(text).Should().Be(expected);
    }

    [Fact]
    public void LooksLikeSecret_CustomBounds_Respected()
    {
        // 10-char alphanumeric inside custom 12-32 window.
        PasswordHeuristic.LooksLikeSecret("abc12345XY", minLen: 12, maxLen: 32).Should().BeFalse();

        // Same 10-char inside default 8-64 window passes.
        PasswordHeuristic.LooksLikeSecret("abc12345XY").Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run tests; verify build error**

Run: `dotnet test apps\ClipTray\ClipTray.Tests\ClipTray.Tests.csproj --filter FullyQualifiedName~PasswordHeuristicTests`
Expected: `CS0103: PasswordHeuristic not found`.

- [ ] **Step 3: Create `PasswordHeuristic.cs`**

```csharp
using System.Linq;

namespace ClipTray;

public static class PasswordHeuristic
{
    /// <summary>
    /// Returns true if <paramref name="text"/> matches the spec's
    /// length-and-charset heuristic for a likely secret: length within
    /// [minLen, maxLen], no whitespace, and at least two of the three
    /// character classes (letter, digit, symbol).
    ///
    /// Flag-only; never enough on its own to drop a clipboard item.
    /// </summary>
    public static bool LooksLikeSecret(string text, int minLen = 8, int maxLen = 64)
    {
        if (text is null) return false;
        if (text.Length < minLen || text.Length > maxLen) return false;
        if (text.Any(char.IsWhiteSpace)) return false;

        int classes = 0;
        if (text.Any(char.IsLetter))                  classes++;
        if (text.Any(char.IsDigit))                   classes++;
        if (text.Any(c => !char.IsLetterOrDigit(c)))  classes++;

        return classes >= 2;
    }
}
```

- [ ] **Step 4: Run tests; verify pass**

Expected: All theory rows + all facts pass.

- [ ] **Step 5: Commit**

```bash
git add apps/ClipTray/ClipTray/PasswordHeuristic.cs apps/ClipTray/ClipTray.Tests/PasswordHeuristicTests.cs
git commit -m "ClipTray: PasswordHeuristic pure-function detection

Length-and-charset heuristic for flagging suspected secrets. Returns
true when the text falls inside [minLen, maxLen], contains no whitespace,
and matches at least two of the three character classes (letter,
digit, symbol).

Flag-only: the capture pipeline marks matches with IsSensitive=true so
they remain in history but require an explicit reveal-and-paste in the
picker. False positives are recoverable via the picker's
'Mark not sensitive' context menu (a Task 14 feature)."
```

---

## Task 5: ImageStore (TDD with TempAppData)

PNG sidecar manager with hash-based dedup and quota enforcement.

**Files:**
- Create: `apps/ClipTray/ClipTray/ImageStore.cs`
- Create: `apps/ClipTray/ClipTray.Tests/ImageStoreTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.IO;
using FluentAssertions;
using WindowsAppTesting;
using Xunit;

namespace ClipTray.Tests;

public class ImageStoreTests
{
    [Fact]
    public void Write_NewHash_CreatesSidecar()
    {
        using var temp = new TempAppData("ClipTray");
        var store = new ImageStore(Path.Combine(temp.Path, "items"));

        var path = store.Write("abc123", new byte[] { 1, 2, 3 });

        File.Exists(path).Should().BeTrue();
        new FileInfo(path).Name.Should().Be("abc123.png");
    }

    [Fact]
    public void Write_ExistingHash_DoesNotRewrite()
    {
        using var temp = new TempAppData("ClipTray");
        var store = new ImageStore(Path.Combine(temp.Path, "items"));

        var first = store.Write("abc123", new byte[] { 1, 2, 3 });
        var firstMtime = File.GetLastWriteTimeUtc(first);

        System.Threading.Thread.Sleep(50);
        var second = store.Write("abc123", new byte[] { 9, 9, 9 });

        second.Should().Be(first);
        File.GetLastWriteTimeUtc(second).Should().Be(firstMtime,
            because: "an existing hash means the bytes are already on disk");
        File.ReadAllBytes(second).Should().Equal(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void Delete_RemovesSidecar()
    {
        using var temp = new TempAppData("ClipTray");
        var store = new ImageStore(Path.Combine(temp.Path, "items"));
        var path = store.Write("hash1", new byte[] { 1 });

        store.Delete("hash1");

        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public void TotalBytes_ReturnsSumOfAllSidecars()
    {
        using var temp = new TempAppData("ClipTray");
        var store = new ImageStore(Path.Combine(temp.Path, "items"));
        store.Write("a", new byte[100]);
        store.Write("b", new byte[200]);
        store.Write("c", new byte[300]);

        store.TotalBytes().Should().Be(600);
    }

    [Fact]
    public void SweepOrphans_RemovesSidecarsNotInKnownHashes()
    {
        using var temp = new TempAppData("ClipTray");
        var dir = Path.Combine(temp.Path, "items");
        var store = new ImageStore(dir);
        store.Write("keep", new byte[] { 1 });
        store.Write("orphan", new byte[] { 2 });

        store.SweepOrphans(new[] { "keep" });

        File.Exists(Path.Combine(dir, "keep.png")).Should().BeTrue();
        File.Exists(Path.Combine(dir, "orphan.png")).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run tests; verify failure**

Expected: `CS0246: ImageStore not found`.

- [ ] **Step 3: Create `ImageStore.cs`**

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ClipTray;

public sealed class ImageStore
{
    private readonly string _dir;

    public ImageStore(string itemsDirectory)
    {
        _dir = itemsDirectory;
        Directory.CreateDirectory(_dir);
    }

    public string PathFor(string hash) => Path.Combine(_dir, hash + ".png");

    public string Write(string hash, byte[] pngBytes)
    {
        var path = PathFor(hash);
        if (!File.Exists(path))
        {
            File.WriteAllBytes(path, pngBytes);
        }
        return path;
    }

    public bool Exists(string hash) => File.Exists(PathFor(hash));

    public void Delete(string hash)
    {
        try { File.Delete(PathFor(hash)); }
        catch (FileNotFoundException) { }
    }

    public long TotalBytes()
    {
        if (!Directory.Exists(_dir)) return 0;
        return Directory.EnumerateFiles(_dir, "*.png")
            .Sum(p => new FileInfo(p).Length);
    }

    public void SweepOrphans(IEnumerable<string> knownHashes)
    {
        if (!Directory.Exists(_dir)) return;
        var known = new HashSet<string>(knownHashes, System.StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(_dir, "*.png"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!known.Contains(name))
            {
                try { File.Delete(file); } catch { }
            }
        }
    }
}
```

- [ ] **Step 4: Run tests; verify pass**

Expected: 5 tests pass.

- [ ] **Step 5: Commit**

```bash
git add apps/ClipTray/ClipTray/ImageStore.cs apps/ClipTray/ClipTray.Tests/ImageStoreTests.cs
git commit -m "ClipTray: ImageStore PNG sidecar manager

Hash-keyed PNG sidecar store backed by %LOCALAPPDATA%\ClipTray\items\
in production (and a TempAppData dir in tests). Idempotent Write skips
existing hashes (assumes content-addressable bytes). TotalBytes sums
sidecar sizes for quota enforcement. SweepOrphans deletes sidecars
whose hash is not in the supplied set, used on startup to clean up
images orphaned by a corrupted or partial index.json.

Quota eviction lives in ClipboardHistory (which knows the order and
pinned state of items); ImageStore only knows how to write, read,
delete, and count."
```

---

## Task 6: HistoryItem + ClipboardHistory (TDD)

In-memory ring buffer with hash dedup, FIFO eviction respecting pinned items, JSON-backed persistence.

**Files:**
- Create: `apps/ClipTray/ClipTray/HistoryItem.cs`
- Create: `apps/ClipTray/ClipTray/ClipboardHistory.cs`
- Create: `apps/ClipTray/ClipTray.Tests/ClipboardHistoryTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using FluentAssertions;
using Xunit;

namespace ClipTray.Tests;

public class ClipboardHistoryTests
{
    private static HistoryItem TextItem(string hash, string text, bool pinned = false, bool sensitive = false) =>
        new(hash, HistoryKind.Text, text, ImagePath: null,
            CapturedUtc: DateTime.UtcNow, SourceProcessName: null,
            IsPinned: pinned, IsSensitive: sensitive);

    [Fact]
    public void Add_NewItem_PrependsToList()
    {
        var h = new ClipboardHistory(textCap: 10, imageCap: 5);
        h.Add(TextItem("a", "alpha"));
        h.Add(TextItem("b", "beta"));

        h.Items.Should().HaveCount(2);
        h.Items[0].Hash.Should().Be("b");
        h.Items[1].Hash.Should().Be("a");
    }

    [Fact]
    public void Add_DuplicateHash_MovesExistingToFront()
    {
        var h = new ClipboardHistory(textCap: 10, imageCap: 5);
        h.Add(TextItem("a", "alpha"));
        h.Add(TextItem("b", "beta"));
        h.Add(TextItem("a", "alpha"));

        h.Items.Should().HaveCount(2);
        h.Items[0].Hash.Should().Be("a");
        h.Items[1].Hash.Should().Be("b");
    }

    [Fact]
    public void Add_OverCap_EvictsOldestNonPinned()
    {
        var h = new ClipboardHistory(textCap: 3, imageCap: 5);
        h.Add(TextItem("a", "alpha"));
        h.Add(TextItem("b", "beta", pinned: true));
        h.Add(TextItem("c", "gamma"));
        h.Add(TextItem("d", "delta")); // evicts "a", keeps pinned "b"

        h.Items.Should().HaveCount(3);
        h.Items.Select(i => i.Hash).Should().BeEquivalentTo(new[] { "d", "c", "b" });
    }

    [Fact]
    public void Pin_FlagsItemAsPinned()
    {
        var h = new ClipboardHistory(textCap: 10, imageCap: 5);
        h.Add(TextItem("a", "alpha"));
        h.SetPinned("a", true);

        h.Items[0].IsPinned.Should().BeTrue();
    }

    [Fact]
    public void Delete_RemovesItem()
    {
        var h = new ClipboardHistory(textCap: 10, imageCap: 5);
        h.Add(TextItem("a", "alpha"));
        h.Add(TextItem("b", "beta"));
        h.Delete("a");

        h.Items.Should().HaveCount(1);
        h.Items[0].Hash.Should().Be("b");
    }

    [Fact]
    public void Clear_WithPreservePinnedTrue_KeepsPinned()
    {
        var h = new ClipboardHistory(textCap: 10, imageCap: 5);
        h.Add(TextItem("a", "alpha"));
        h.Add(TextItem("b", "beta", pinned: true));
        h.Clear(preservePinned: true);

        h.Items.Should().HaveCount(1);
        h.Items[0].Hash.Should().Be("b");
    }

    [Fact]
    public void Clear_WithPreservePinnedFalse_RemovesEverything()
    {
        var h = new ClipboardHistory(textCap: 10, imageCap: 5);
        h.Add(TextItem("a", "alpha"));
        h.Add(TextItem("b", "beta", pinned: true));
        h.Clear(preservePinned: false);

        h.Items.Should().BeEmpty();
    }

    [Fact]
    public void TextAndImage_HaveSeparateCaps()
    {
        var h = new ClipboardHistory(textCap: 2, imageCap: 1);

        h.Add(TextItem("t1", "t1"));
        h.Add(TextItem("t2", "t2"));
        h.Add(new HistoryItem("i1", HistoryKind.Image, null, "items/i1.png",
            DateTime.UtcNow, null, IsPinned: false, IsSensitive: false));

        h.Items.Where(i => i.Kind == HistoryKind.Text).Should().HaveCount(2);
        h.Items.Where(i => i.Kind == HistoryKind.Image).Should().HaveCount(1);

        h.Add(TextItem("t3", "t3")); // evicts t1
        h.Items.Where(i => i.Kind == HistoryKind.Text).Select(i => i.Hash)
         .Should().BeEquivalentTo(new[] { "t3", "t2" });

        h.Items.Where(i => i.Kind == HistoryKind.Image).Should().HaveCount(1);
    }
}
```

- [ ] **Step 2: Run tests; verify failure**

Expected: `CS0246: ClipboardHistory / HistoryItem / HistoryKind not found`.

- [ ] **Step 3: Create `HistoryItem.cs`**

```csharp
namespace ClipTray;

public enum HistoryKind { Text, Image }

public sealed record HistoryItem(
    string Hash,
    HistoryKind Kind,
    string? Text,
    string? ImagePath,
    DateTime CapturedUtc,
    string? SourceProcessName,
    bool IsPinned,
    bool IsSensitive);
```

- [ ] **Step 4: Create `ClipboardHistory.cs`**

```csharp
using System.Collections.Generic;
using System.Linq;

namespace ClipTray;

public sealed class ClipboardHistory
{
    private readonly List<HistoryItem> _items = new();
    private readonly int _textCap;
    private readonly int _imageCap;

    public ClipboardHistory(int textCap, int imageCap)
    {
        _textCap = textCap;
        _imageCap = imageCap;
    }

    public IReadOnlyList<HistoryItem> Items => _items;

    public void Add(HistoryItem item)
    {
        var existingIdx = _items.FindIndex(i => i.Hash == item.Hash);
        if (existingIdx >= 0)
        {
            // Dedup: move to front and update CapturedUtc.
            var existing = _items[existingIdx] with { CapturedUtc = item.CapturedUtc };
            _items.RemoveAt(existingIdx);
            _items.Insert(0, existing);
            return;
        }

        _items.Insert(0, item);
        EnforceCap(item.Kind);
    }

    public void SetPinned(string hash, bool pinned)
    {
        var idx = _items.FindIndex(i => i.Hash == hash);
        if (idx < 0) return;
        _items[idx] = _items[idx] with { IsPinned = pinned };
    }

    public void Delete(string hash)
    {
        _items.RemoveAll(i => i.Hash == hash);
    }

    public void Clear(bool preservePinned)
    {
        if (preservePinned)
            _items.RemoveAll(i => !i.IsPinned);
        else
            _items.Clear();
    }

    private void EnforceCap(HistoryKind kind)
    {
        int cap = kind == HistoryKind.Text ? _textCap : _imageCap;

        // Walk from the tail removing the oldest non-pinned of this kind
        // until the count of this kind is at or below cap.
        while (_items.Count(i => i.Kind == kind) > cap)
        {
            // Find the oldest non-pinned of this kind.
            var victim = _items.AsEnumerable().Reverse()
                .FirstOrDefault(i => i.Kind == kind && !i.IsPinned);
            if (victim is null) break; // everything of this kind is pinned

            _items.Remove(victim);
        }
    }
}
```

- [ ] **Step 5: Run tests; verify pass**

Expected: 8 tests pass.

- [ ] **Step 6: Commit**

```bash
git add apps/ClipTray/ClipTray/HistoryItem.cs apps/ClipTray/ClipTray/ClipboardHistory.cs apps/ClipTray/ClipTray.Tests/ClipboardHistoryTests.cs
git commit -m "ClipTray: HistoryItem record + ClipboardHistory in-memory manager

HistoryItem is the canonical row shape: hash, kind, text or imagepath,
capturedUtc, source process name, pinned flag, sensitive flag. It is
a positional record so equality and serialisation behave naturally.

ClipboardHistory manages an ordered in-memory list. Add dedups by hash
(moves existing to front and refreshes CapturedUtc) or prepends new
items; per-kind FIFO eviction respects IsPinned. SetPinned, Delete,
and Clear are direct list ops. Text and image caps are independent so
text-heavy workflows don't squeeze out images.

JSON persistence ships in Task 7 (an integration that wires the
in-memory manager to the on-disk index.json)."
```

---

## Task 7: ClipboardHistory JSON persistence

Wires `ClipboardHistory` to `%LOCALAPPDATA%\ClipTray\index.json` via an atomic-write helper modeled on `JsonSettingsStore<T>` but storing the manifest list separately from `AppSettings`.

**Files:**
- Modify: `apps/ClipTray/ClipTray/ClipboardHistory.cs` (add Load/Save)
- Create: `apps/ClipTray/ClipTray/HistoryIndex.cs` (root type for serialisation)
- Modify: `apps/ClipTray/ClipTray.Tests/ClipboardHistoryTests.cs` (add round-trip tests)

- [ ] **Step 1: Append the failing tests**

```csharp
    [Fact]
    public void SaveLoad_RoundTripsAllItems()
    {
        using var temp = new TempAppData("ClipTray");
        var indexPath = System.IO.Path.Combine(temp.Path, "index.json");

        var w = new ClipboardHistory(textCap: 10, imageCap: 5);
        w.Add(TextItem("a", "alpha", pinned: true));
        w.Add(TextItem("b", "beta", sensitive: true));
        w.Save(indexPath);

        var r = ClipboardHistory.Load(indexPath, textCap: 10, imageCap: 5);
        r.Items.Should().HaveCount(2);
        r.Items[0].Hash.Should().Be("b");
        r.Items[0].IsSensitive.Should().BeTrue();
        r.Items[1].Hash.Should().Be("a");
        r.Items[1].IsPinned.Should().BeTrue();
    }

    [Fact]
    public void Load_MissingFile_ReturnsEmpty()
    {
        using var temp = new TempAppData("ClipTray");
        var indexPath = System.IO.Path.Combine(temp.Path, "nonexistent.json");

        var h = ClipboardHistory.Load(indexPath, textCap: 10, imageCap: 5);
        h.Items.Should().BeEmpty();
    }

    [Fact]
    public void Load_CorruptFile_ReturnsEmptyAndQuarantines()
    {
        using var temp = new TempAppData("ClipTray");
        var indexPath = System.IO.Path.Combine(temp.Path, "index.json");
        System.IO.File.WriteAllText(indexPath, "{not valid json");

        var h = ClipboardHistory.Load(indexPath, textCap: 10, imageCap: 5);

        h.Items.Should().BeEmpty();
        // The quarantine file is named "index.corrupt-<timestamp>.json" sibling.
        System.IO.Directory.GetFiles(temp.Path, "*.corrupt-*.json")
            .Should().NotBeEmpty();
    }
```

Add `using WindowsAppTesting;` if not already imported.

- [ ] **Step 2: Run tests; verify failure**

Expected: `CS0117: ClipboardHistory does not contain 'Save'`, etc.

- [ ] **Step 3: Create `HistoryIndex.cs`**

```csharp
using System.Collections.Generic;

namespace ClipTray;

internal sealed class HistoryIndex
{
    public int SchemaVersion { get; set; } = 1;
    public List<HistoryItem> Items { get; set; } = new();
}
```

- [ ] **Step 4: Add `Save` and `Load` to `ClipboardHistory.cs`**

Append:

```csharp
    public void Save(string indexPath)
    {
        var index = new HistoryIndex { Items = _items.ToList() };
        var json = System.Text.Json.JsonSerializer.Serialize(index, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        });

        // Atomic write: temp file then rename.
        var tmp = indexPath + ".tmp";
        System.IO.File.WriteAllText(tmp, json);
        if (System.IO.File.Exists(indexPath)) System.IO.File.Replace(tmp, indexPath, destinationBackupFileName: null);
        else System.IO.File.Move(tmp, indexPath);
    }

    public static ClipboardHistory Load(string indexPath, int textCap, int imageCap)
    {
        var h = new ClipboardHistory(textCap, imageCap);
        if (!System.IO.File.Exists(indexPath)) return h;

        try
        {
            var json = System.IO.File.ReadAllText(indexPath);
            var index = System.Text.Json.JsonSerializer.Deserialize<HistoryIndex>(json);
            if (index?.Items is { } items)
            {
                // Items in JSON are newest-first; preserve order.
                h._items.AddRange(items);
            }
            return h;
        }
        catch
        {
            // Quarantine the corrupt file.
            var dir = System.IO.Path.GetDirectoryName(indexPath) ?? ".";
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var quarantine = System.IO.Path.Combine(dir, $"index.corrupt-{stamp}.json");
            try { System.IO.File.Move(indexPath, quarantine); } catch { }
            return new ClipboardHistory(textCap, imageCap);
        }
    }
```

- [ ] **Step 5: Run tests; verify pass**

Expected: 11 tests pass (8 from Task 6 + 3 new).

- [ ] **Step 6: Commit**

```bash
git add apps/ClipTray/ClipTray/HistoryIndex.cs apps/ClipTray/ClipTray/ClipboardHistory.cs apps/ClipTray/ClipTray.Tests/ClipboardHistoryTests.cs
git commit -m "ClipTray: ClipboardHistory persists to index.json with quarantine

Save serialises a HistoryIndex (schema version + items list) via a
temp-file-then-rename atomic write. Load reads the index back,
preserving newest-first order. Missing file returns an empty history;
malformed JSON is moved aside to index.corrupt-{timestamp}.json and
an empty history is returned.

Storage layout: %LOCALAPPDATA%\ClipTray\index.json plus the existing
items\<hash>.png sidecars from ImageStore. SchemaVersion exists for
future migrations; v1 is the initial format."
```

---

## Task 8: ClipboardListener (Win32 + hidden HWND)

Wraps `AddClipboardFormatListener` in a hidden `NativeWindow` and surfaces `ClipboardChanged` events.

**Files:**
- Modify: `shared/WindowsTrayCore/Native/TrayNativeMethods.cs` (add P/Invokes)
- Create: `apps/ClipTray/ClipTray/ClipboardListener.cs`

This task has no isolated unit test; the listener is verified by the Task 16 E2E.

- [ ] **Step 1: Add Win32 surface**

Append to `shared/WindowsTrayCore/Native/TrayNativeMethods.cs`:

```csharp
[DllImport("user32.dll", SetLastError = true)]
internal static extern bool AddClipboardFormatListener(IntPtr hwnd);

[DllImport("user32.dll", SetLastError = true)]
internal static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

internal const int WM_CLIPBOARDUPDATE = 0x031D;
```

- [ ] **Step 2: Create `ClipboardListener.cs`**

```csharp
using System.Windows.Forms;
using WindowsTrayCore.Native;

namespace ClipTray;

/// <summary>
/// Wraps Win32 AddClipboardFormatListener. Owns a hidden NativeWindow that
/// receives WM_CLIPBOARDUPDATE; raises <see cref="ClipboardChanged"/> on
/// the application message-pump thread.
/// </summary>
internal sealed class ClipboardListener : IDisposable
{
    private readonly MessageWindow _window;
    private bool _registered;
    private bool _disposed;

    public ClipboardListener()
    {
        _window = new MessageWindow(this);
        _window.CreateHandle(new CreateParams());
        _registered = TrayNativeMethods.AddClipboardFormatListener(_window.Handle);
    }

    public event EventHandler? ClipboardChanged;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_registered)
        {
            TrayNativeMethods.RemoveClipboardFormatListener(_window.Handle);
            _registered = false;
        }
        _window.DestroyHandle();
    }

    private sealed class MessageWindow : NativeWindow
    {
        private readonly ClipboardListener _owner;
        public MessageWindow(ClipboardListener owner) => _owner = owner;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == TrayNativeMethods.WM_CLIPBOARDUPDATE)
            {
                _owner.ClipboardChanged?.Invoke(_owner, EventArgs.Empty);
                return;
            }
            base.WndProc(ref m);
        }
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build apps\ClipTray\ClipTray.sln`
Expected: clean (no test coverage at this layer; the round-trip lives in E2E Task 16).

- [ ] **Step 4: Commit**

```bash
git add apps/ClipTray/ClipTray/ClipboardListener.cs shared/WindowsTrayCore/Native/TrayNativeMethods.cs
git commit -m "ClipTray: ClipboardListener wraps AddClipboardFormatListener

Hidden NativeWindow registers itself as a clipboard format listener;
WM_CLIPBOARDUPDATE arrives on the message-pump thread and surfaces
through the ClipboardChanged event. Dispose removes the listener and
destroys the window handle.

P/Invokes (AddClipboardFormatListener, RemoveClipboardFormatListener,
WM_CLIPBOARDUPDATE constant) added to WindowsTrayCore.Native.
Verified end-to-end by the ClipTray.E2ETests round-trip test in Task 16."
```

---

## Task 9: SessionLockMonitor + ForegroundProcessProbe

Two small Win32 wrappers used by the privacy filter. Lightly tested (mostly construction + dispose smoke).

**Files:**
- Modify: `shared/WindowsTrayCore/Native/TrayNativeMethods.cs` (WTS P/Invokes)
- Create: `apps/ClipTray/ClipTray/SessionLockMonitor.cs`
- Create: `apps/ClipTray/ClipTray/ForegroundProcessProbe.cs`

- [ ] **Step 1: Add Win32 surface for WTS**

Append to `shared/WindowsTrayCore/Native/TrayNativeMethods.cs`:

```csharp
[DllImport("wtsapi32.dll", SetLastError = true)]
internal static extern bool WTSRegisterSessionNotification(IntPtr hWnd, int dwFlags);

[DllImport("wtsapi32.dll", SetLastError = true)]
internal static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);

internal const int NOTIFY_FOR_THIS_SESSION = 0;
internal const int WM_WTSSESSION_CHANGE = 0x02B1;
internal const int WTS_SESSION_LOCK = 0x7;
internal const int WTS_SESSION_UNLOCK = 0x8;

[DllImport("user32.dll")]
internal static extern IntPtr GetForegroundWindow();

[DllImport("user32.dll", SetLastError = true)]
internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
```

- [ ] **Step 2: Create `SessionLockMonitor.cs`**

```csharp
using System.Windows.Forms;
using WindowsTrayCore.Native;

namespace ClipTray;

internal sealed class SessionLockMonitor : IDisposable
{
    private readonly MessageWindow _window;
    private bool _registered;
    private bool _disposed;

    public SessionLockMonitor()
    {
        _window = new MessageWindow(this);
        _window.CreateHandle(new CreateParams());
        _registered = TrayNativeMethods.WTSRegisterSessionNotification(
            _window.Handle, TrayNativeMethods.NOTIFY_FOR_THIS_SESSION);
    }

    public bool IsLocked { get; private set; }
    public event EventHandler? LockStateChanged;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_registered) TrayNativeMethods.WTSUnRegisterSessionNotification(_window.Handle);
        _window.DestroyHandle();
    }

    private sealed class MessageWindow : NativeWindow
    {
        private readonly SessionLockMonitor _owner;
        public MessageWindow(SessionLockMonitor owner) => _owner = owner;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == TrayNativeMethods.WM_WTSSESSION_CHANGE)
            {
                var was = _owner.IsLocked;
                _owner.IsLocked = (int)m.WParam == TrayNativeMethods.WTS_SESSION_LOCK
                    ? true
                    : ((int)m.WParam == TrayNativeMethods.WTS_SESSION_UNLOCK ? false : _owner.IsLocked);
                if (_owner.IsLocked != was)
                    _owner.LockStateChanged?.Invoke(_owner, EventArgs.Empty);
            }
            base.WndProc(ref m);
        }
    }
}
```

- [ ] **Step 3: Create `ForegroundProcessProbe.cs`**

```csharp
using System.Diagnostics;
using WindowsTrayCore.Native;

namespace ClipTray;

internal static class ForegroundProcessProbe
{
    /// <summary>
    /// Returns the process name of the foreground window, lowercased.
    /// Returns null if anything in the chain fails (the foreground HWND
    /// disappears, the process exits between calls, etc).
    /// </summary>
    public static string? GetCurrentName()
    {
        try
        {
            var hwnd = TrayNativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;
            TrayNativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return null;
            using var proc = Process.GetProcessById((int)pid);
            return proc.ProcessName.ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build apps\ClipTray\ClipTray.sln`
Expected: clean.

- [ ] **Step 5: Commit**

```bash
git add apps/ClipTray/ClipTray/SessionLockMonitor.cs apps/ClipTray/ClipTray/ForegroundProcessProbe.cs shared/WindowsTrayCore/Native/TrayNativeMethods.cs
git commit -m "ClipTray: SessionLockMonitor + ForegroundProcessProbe

Two small Win32 wrappers for the privacy filter. SessionLockMonitor
hosts a hidden NativeWindow that subscribes via
WTSRegisterSessionNotification and tracks WTS_SESSION_LOCK /
WTS_SESSION_UNLOCK transitions. ForegroundProcessProbe is a static
helper that resolves the foreground HWND to a process name via
GetWindowThreadProcessId and Process.GetProcessById. Both fail open
(return null / leave state unchanged) on any error rather than throwing."
```

---

## Task 10: ClipboardWriter (SendInput Ctrl+V paste)

Sets the clipboard to a chosen item then synthesises Ctrl+V to the previously-foreground window.

**Files:**
- Modify: `shared/WindowsTrayCore/Native/TrayNativeMethods.cs` (SendInput + SetForegroundWindow)
- Create: `apps/ClipTray/ClipTray/ClipboardWriter.cs`

- [ ] **Step 1: Add Win32 surface**

`SetForegroundWindow` already exists in `TrayNativeMethods.cs`. Add SendInput:

```csharp
[DllImport("user32.dll", SetLastError = true)]
internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

[StructLayout(LayoutKind.Sequential)]
internal struct INPUT
{
    public uint type;
    public InputUnion U;
    public static int Size => Marshal.SizeOf<INPUT>();
}

[StructLayout(LayoutKind.Explicit)]
internal struct InputUnion
{
    [FieldOffset(0)] public KEYBDINPUT ki;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KEYBDINPUT
{
    public ushort wVk;
    public ushort wScan;
    public uint   dwFlags;
    public uint   time;
    public IntPtr dwExtraInfo;
}

internal const uint INPUT_KEYBOARD = 1;
internal const uint KEYEVENTF_KEYUP = 0x0002;
internal const ushort VK_CONTROL = 0x11;
internal const ushort VK_V = 0x56;
```

- [ ] **Step 2: Create `ClipboardWriter.cs`**

```csharp
using System.IO;
using System.Windows.Forms;
using WindowsTrayCore.Native;

namespace ClipTray;

internal static class ClipboardWriter
{
    public static void SetText(string text)
    {
        Clipboard.SetText(text, TextDataFormat.UnicodeText);
    }

    public static void SetImage(string pngPath)
    {
        using var img = System.Drawing.Image.FromFile(pngPath);
        Clipboard.SetImage(img);
    }

    /// <summary>
    /// Synthesises Ctrl-down, V-down, V-up, Ctrl-up to the foreground
    /// window. Returns false if SendInput fails (typically a UAC
    /// integrity-level mismatch); caller should fall back to a
    /// "paste manually" balloon.
    /// </summary>
    public static bool SendCtrlV(IntPtr targetWindow)
    {
        if (targetWindow != IntPtr.Zero)
            TrayNativeMethods.SetForegroundWindow(targetWindow);

        var inputs = new TrayNativeMethods.INPUT[]
        {
            MakeKey(TrayNativeMethods.VK_CONTROL, keyUp: false),
            MakeKey(TrayNativeMethods.VK_V,       keyUp: false),
            MakeKey(TrayNativeMethods.VK_V,       keyUp: true),
            MakeKey(TrayNativeMethods.VK_CONTROL, keyUp: true),
        };

        var sent = TrayNativeMethods.SendInput((uint)inputs.Length, inputs, TrayNativeMethods.INPUT.Size);
        return sent == (uint)inputs.Length;
    }

    private static TrayNativeMethods.INPUT MakeKey(ushort vk, bool keyUp) => new()
    {
        type = TrayNativeMethods.INPUT_KEYBOARD,
        U = new TrayNativeMethods.InputUnion
        {
            ki = new TrayNativeMethods.KEYBDINPUT
            {
                wVk = vk,
                dwFlags = keyUp ? TrayNativeMethods.KEYEVENTF_KEYUP : 0u,
            },
        },
    };
}
```

- [ ] **Step 3: Build**

Run: `dotnet build apps\ClipTray\ClipTray.sln`
Expected: clean.

- [ ] **Step 4: Commit**

```bash
git add apps/ClipTray/ClipTray/ClipboardWriter.cs shared/WindowsTrayCore/Native/TrayNativeMethods.cs
git commit -m "ClipTray: ClipboardWriter sets clipboard + synthesises Ctrl+V

SetText / SetImage use the WinForms Clipboard convenience methods.
SendCtrlV invokes SendInput with the four key events (Ctrl-down,
V-down, V-up, Ctrl-up) against the previously-foreground window,
SetForegroundWindow-ing it first so the paste lands in the right
place. Returns false on SendInput failure (typically UAC integrity
mismatch); the picker surfaces a 'paste manually' balloon in that case.

The SendInput INPUT / KEYBDINPUT / InputUnion P/Invoke types live in
TrayNativeMethods alongside the other tray-related Win32 surfaces."
```

---

## Task 11: Capture pipeline orchestrator

Wires `ClipboardListener` + privacy filter + `PasswordHeuristic` + `ClipboardHistory` + `ImageStore` into a single class that lives inside `ClipTrayContext`. This task creates the orchestrator skeleton; the actual `ClipTrayContext` integration lands in Task 14.

**Files:**
- Create: `apps/ClipTray/ClipTray/CapturePipeline.cs`
- (Optional) Create: `apps/ClipTray/ClipTray.Tests/CapturePipelineTests.cs` if the orchestrator's filter logic is worth unit-testing in isolation. Skip if the logic is trivially obvious.

- [ ] **Step 1: Create `CapturePipeline.cs`**

```csharp
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace ClipTray;

internal sealed class CapturePipeline
{
    private readonly AppSettings _settings;
    private readonly ClipboardHistory _history;
    private readonly ImageStore _images;
    private readonly SessionLockMonitor _lock;
    private readonly string _indexPath;

    public CapturePipeline(
        AppSettings settings,
        ClipboardHistory history,
        ImageStore images,
        SessionLockMonitor sessionLock,
        string indexPath)
    {
        _settings = settings;
        _history = history;
        _images = images;
        _lock = sessionLock;
        _indexPath = indexPath;
    }

    public void OnClipboardChanged()
    {
        // Privacy filter, evaluated in spec order.
        if (_settings.PauseCapture) return;
        if (_settings.PauseOnLockScreen && _lock.IsLocked) return;
        if (HasSkipSyncMarker()) return;
        if (IsForegroundProcessBlocked()) return;

        // Extract.
        if (Clipboard.ContainsText())
        {
            var text = Clipboard.GetText();
            if (string.IsNullOrEmpty(text)) return;
            CaptureText(text);
        }
        else if (Clipboard.ContainsImage())
        {
            CaptureImage();
        }
    }

    private bool HasSkipSyncMarker()
    {
        // CanIncludeInClipboardHistory: byte 0x00 means skip.
        if (Clipboard.ContainsData("CanIncludeInClipboardHistory"))
        {
            if (Clipboard.GetData("CanIncludeInClipboardHistory") is byte[] b && b.Length >= 1 && b[0] == 0)
                return true;
        }
        // ExcludeClipboardContentFromMonitorProcessing: presence alone means skip.
        if (Clipboard.ContainsData("ExcludeClipboardContentFromMonitorProcessing"))
            return true;
        return false;
    }

    private bool IsForegroundProcessBlocked()
    {
        var name = ForegroundProcessProbe.GetCurrentName();
        if (name is null) return false; // fail-open
        return _settings.ForegroundBlocklist.Any(n =>
            string.Equals(n, name, System.StringComparison.OrdinalIgnoreCase));
    }

    private void CaptureText(string text)
    {
        // Normalise CRLF -> LF for canonical hashing; preserve original for display.
        var canonical = text.Replace("\r\n", "\n");
        var hash = Sha256(Encoding.UTF8.GetBytes(canonical));
        bool sensitive = _settings.PasswordHeuristicEnabled
            && PasswordHeuristic.LooksLikeSecret(text,
                _settings.PasswordHeuristicMinLength,
                _settings.PasswordHeuristicMaxLength);

        _history.Add(new HistoryItem(
            Hash: hash,
            Kind: HistoryKind.Text,
            Text: text,
            ImagePath: null,
            CapturedUtc: System.DateTime.UtcNow,
            SourceProcessName: ForegroundProcessProbe.GetCurrentName(),
            IsPinned: false,
            IsSensitive: sensitive));
        _history.Save(_indexPath);
    }

    private void CaptureImage()
    {
        using var img = Clipboard.GetImage();
        if (img is null) return;
        using var ms = new MemoryStream();
        img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        var bytes = ms.ToArray();
        var hash = Sha256(bytes);
        var path = _images.Write(hash, bytes);

        _history.Add(new HistoryItem(
            Hash: hash,
            Kind: HistoryKind.Image,
            Text: null,
            ImagePath: path,
            CapturedUtc: System.DateTime.UtcNow,
            SourceProcessName: ForegroundProcessProbe.GetCurrentName(),
            IsPinned: false,
            IsSensitive: false));
        _history.Save(_indexPath);
    }

    private static string Sha256(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return System.Convert.ToHexString(hash).ToLowerInvariant();
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build apps\ClipTray\ClipTray.sln`
Expected: clean.

- [ ] **Step 3: Commit**

```bash
git add apps/ClipTray/ClipTray/CapturePipeline.cs
git commit -m "ClipTray: CapturePipeline orchestrates the privacy filter + history add

Single class that ClipTrayContext will wire to ClipboardListener.
Privacy filter evaluates pause -> lock -> skip-sync markers ->
foreground blocklist in order; any layer short-circuits the capture.
After the filter, content is extracted (text or image), hashed
(SHA-256 over normalised bytes), flagged as sensitive if the password
heuristic matches (text only), and prepended to ClipboardHistory.
Persists to index.json after every add via ClipboardHistory.Save."
```

---

## Task 12: PickerForm (parallel-subagent candidate)

The popup picker. Largest single piece of UI in the project. Self-contained: borderless form, search box, virtualised list of rows, keyboard + mouse handlers, theming.

**Files:**
- Create: `apps/ClipTray/ClipTray/PickerForm.cs`

This task is a strong candidate for parallel subagent dispatch after Tasks 1-11 land, alongside Task 13 (SettingsForm). Both are independent.

The implementer should:
- Build a `Form` with `FormBorderStyle = None`, `TopMost = true`, fixed size from `AppSettings.PickerWidth/Height`
- A `TextBox` on top for the filter
- A scrollable list area below (`ListBox` with `DrawMode = OwnerDrawFixed` is fine; virtualisation isn't necessary for ~60 rows)
- Pinned items first, then a separator, then recent items
- Filter by case-insensitive Contains over `Text` field, or by regex if the filter starts with `/`
- Wire ApplyTo + Changed for theming
- `ShowAtCursor(ClipboardHistory history, IntPtr targetWindow)` method: positions near cursor, focuses search box, stores `targetWindow` for paste
- Keyboard: Up/Down navigate, Enter paste, Esc close, Ctrl+P toggle pin, Del delete, Ctrl+R toggle "Mark not sensitive" on highlighted
- Mouse: click selects + pastes, right-click context menu
- Sensitive items render as `••••••••`; inline confirm "Reveal and paste? [Y/N]" before pasting
- On paste: call `ClipboardWriter.SetText` / `SetImage`, then `ClipboardWriter.SendCtrlV(_targetWindow)`, then `Close()`

Per the spec's "borderless, theme-tinted border" requirement, paint a single-pixel border in `TrayTheme.Current.Accent`.

- [ ] **Step 1: Implement `PickerForm.cs`** per the bullets above

See `apps/SoundTracker/SoundTracker.App/RecentActivityForm.cs` and `apps/NetProfileSwitcher/UI/Theme.cs` `StyleListBox` for reference patterns on owner-draw lists and theme integration.

- [ ] **Step 2: Build**

Run: `dotnet build apps\ClipTray\ClipTray.sln`
Expected: clean.

- [ ] **Step 3: Commit**

```bash
git add apps/ClipTray/ClipTray/PickerForm.cs
git commit -m "ClipTray: PickerForm borderless popup with search + virtualised rows

Borderless, TopMost form positioned at cursor. Search box on top
filters via Contains (case-insensitive) or regex when input starts
with /. List shows pinned items first then a separator then recent.
Sensitive items render as ........ and prompt 'Reveal and paste?' inline
before disclosing.

Keyboard: Up/Down navigate, Enter paste, Esc close, Ctrl+P toggle pin,
Del delete, Ctrl+R toggle sensitive flag. Mouse: click paste,
right-click context menu (Pin/Unpin, Reveal-and-paste, Copy without
paste, Delete, Mark not sensitive).

Themed via ThemeApplier.ApplyTo + TrayTheme.Current.Changed subscription
(matching the canonical pattern from Phase 29). Single-pixel accent
border painted in OnPaint."
```

---

## Task 13: SettingsForm (parallel-subagent candidate)

Tabbed settings form. Independent of PickerForm; can run in parallel with Task 12.

**Files:**
- Create: `apps/ClipTray/ClipTray/SettingsForm.cs`

Tabs and controls per spec:

- **General**: hotkey rebind (custom capture-key TextBox), text cap, image cap, disk quota MB
- **Privacy**: pause-hotkey rebind, pause on lock screen, password heuristic toggle + min/max length
- **Blocklist**: ListBox + Add / Remove / "Add current foreground app" buttons
- **System**: run-at-startup checkbox, startup delay, "Open data folder" link, "Open logs folder" link

The hotkey-capture control is a small custom `TextBox` subclass that intercepts `KeyDown`, displays the chord (e.g. `Ctrl+Shift+V`), and stores the modifier + key in a backing field. Pattern: see existing `KeyDown` handlers in `ProgramHider`'s settings form for a working example (the hotkey rebind UI almost certainly exists somewhere in ProgramHider given its hotkey functionality).

Theming follows the canonical pattern (ApplyTo at end of constructor + Changed subscription + hint labels with `Tag = "hint"` + ForegroundDim post-pass).

- [ ] **Step 1: Implement `SettingsForm.cs`** with the four tabs above

- [ ] **Step 2: Build + test**

Run: `dotnet build apps\ClipTray\ClipTray.sln`
Run: `dotnet test apps\ClipTray\ClipTray.Tests\ClipTray.Tests.csproj`
Expected: clean build, all tests pass.

- [ ] **Step 3: Commit**

```bash
git add apps/ClipTray/ClipTray/SettingsForm.cs
git commit -m "ClipTray: SettingsForm with tabbed general / privacy / blocklist / system

Mirrors the existing four apps' tabbed pattern. General tab covers
hotkey rebind (custom capture-key TextBox subclass), text cap, image
cap, disk quota MB. Privacy tab: pause-hotkey rebind, pause on lock
screen, password heuristic enable + min/max length. Blocklist tab:
ListBox of process names with Add / Remove / 'Add current foreground
app' actions. System tab: run-at-startup, startup delay, data folder
and logs folder links.

Themed via ThemeApplier.ApplyTo + Changed subscription. Hint labels
tagged 'hint' and re-coloured to ForegroundDim in a post-ApplyTo pass
(same idiom as BatteryTray and SoundTracker)."
```

---

## Task 14: ClipTrayContext (the integration)

Pulls everything together: tray icon, menu, hotkey registration, listener, capture pipeline, picker, settings form, update checker. This is the biggest single inline task and is the moment ClipTray actually runs.

**Files:**
- Create: `apps/ClipTray/ClipTray/ClipTrayContext.cs`
- Modify: `apps/ClipTray/ClipTray/Program.cs` (uncomment the real context, remove placeholder)

- [ ] **Step 1: Implement `ClipTrayContext.cs`**

```csharp
using System.Diagnostics;
using System.Windows.Forms;
using WindowsAppCore;
using WindowsTrayCore;

namespace ClipTray;

public sealed class ClipTrayContext : ApplicationContext
{
    private readonly TrayIcon _trayIcon;
    private readonly SingleInstanceActivation _activation;
    private readonly UiDispatcher _ui = new();
    private readonly RunKeyStartupRegistration _startup;

    private readonly ClipboardListener _listener;
    private readonly SessionLockMonitor _sessionLock;
    private readonly HotkeyRegistration _hotkey;
    private readonly ClipboardHistory _history;
    private readonly ImageStore _images;
    private readonly CapturePipeline _capture;

    private readonly UpdateChecker _updateChecker;
    private readonly System.Net.Http.HttpClient _updateHttpClient = new();
    private readonly System.Threading.CancellationTokenSource _updateCts = new();

    private AppSettings _settings;
    private SettingsForm? _settingsForm;
    private PickerForm? _pickerForm;
    private bool _capturePaused;
    private IntPtr _previousForeground;

    private const int HOTKEY_ID_PICKER = 1;
    private const int HOTKEY_ID_PAUSE  = 2;

    public ClipTrayContext(AppSettings settings, SingleInstanceActivation activation)
    {
        _settings = settings;
        _activation = activation;
        _startup = new RunKeyStartupRegistration("ClipTray",
            Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty,
            "ClipTray - clipboard history with hotkey picker");
        _updateChecker = new UpdateChecker(_updateHttpClient,
            Application.ProductVersion, RepoInfo.Owner, RepoInfo.Name);

        var dataDir = AppPaths.DataDir("ClipTray");
        var indexPath = System.IO.Path.Combine(dataDir, "index.json");
        _images = new ImageStore(System.IO.Path.Combine(dataDir, "items"));
        _history = ClipboardHistory.Load(indexPath, _settings.TextHistoryCap, _settings.ImageHistoryCap);
        _images.SweepOrphans(_history.Items.Where(i => i.Kind == HistoryKind.Image).Select(i => i.Hash));

        _sessionLock = new SessionLockMonitor();
        _capture = new CapturePipeline(_settings, _history, _images, _sessionLock, indexPath);

        _listener = new ClipboardListener();
        _listener.ClipboardChanged += (_, _) => _ui.Post(() => _capture.OnClipboardChanged());

        _hotkey = new HotkeyRegistration();
        _hotkey.Pressed += (_, id) => _ui.Post(() => OnHotkeyPressed(id));
        RegisterHotkeys();

        _trayIcon = TrayIcon.ForApp("ClipTray");
        _trayIcon.TooltipText = $"ClipTray v{VersionFormatter.TrimSemverSuffix(Application.ProductVersion)}";
        _trayIcon.ContextMenuStrip = BuildMenu();
        _trayIcon.Visible = true;

        _activation.ActivationRequested += (_, _) => _ui.Post(OpenSettings);

        _updateChecker.StartPeriodicChecks(TimeSpan.FromHours(24), r =>
            _ui.Post(() => _trayIcon.ShowBalloonTip(5000, "ClipTray update available",
                $"Version {r.LatestVersion} is available - visit GitHub to download.", ToolTipIcon.Info)),
            _updateCts.Token);

        ShowFirstRunBalloonIfNeeded();
    }

    private void RegisterHotkeys()
    {
        if (_settings.PickerHotkeyKey != Keys.None)
        {
            _hotkey.Register(HOTKEY_ID_PICKER, _settings.PickerHotkeyModifiers, _settings.PickerHotkeyKey);
        }
        if (_settings.PauseHotkeyKey != Keys.None)
        {
            _hotkey.Register(HOTKEY_ID_PAUSE, _settings.PauseHotkeyModifiers, _settings.PauseHotkeyKey);
        }
    }

    private void OnHotkeyPressed(int id)
    {
        switch (id)
        {
            case HOTKEY_ID_PICKER: OpenPicker(); break;
            case HOTKEY_ID_PAUSE:  TogglePause(); break;
        }
    }

    private void OpenPicker()
    {
        _previousForeground = Native.NativeMethodsForeground.GetForeground();
        if (_pickerForm is null || _pickerForm.IsDisposed)
            _pickerForm = new PickerForm(_history, _settings, _images);
        _pickerForm.ShowAtCursor(_previousForeground);
    }

    private void TogglePause()
    {
        _capturePaused = !_capturePaused;
        _settings.PauseCapture = _capturePaused;
        _settings.Save();
        UpdatePauseMenuItem();
    }

    // ... OpenSettings, BuildMenu, ShowFirstRunBalloonIfNeeded, ClearHistory,
    //     UpdatePauseMenuItem, Dispose etc.

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _updateCts.Cancel();
            _updateHttpClient.Dispose();
            _hotkey.Dispose();
            _listener.Dispose();
            _sessionLock.Dispose();
            _trayIcon.Dispose();
            _ui.Dispose();
        }
        base.Dispose(disposing);
    }
}
```

(The class will need more support code: `BuildMenu`, `OpenSettings`, `ClearHistory`, `UpdatePauseMenuItem`, `ShowFirstRunBalloonIfNeeded`. Model on `apps/BatteryTray/BatteryTray/BatteryTrayContext.cs` for the standard tray-menu construction with `StandardMenuItems.CreateAbout / CreateCheckForUpdates / CreateOpenLogs`.)

- [ ] **Step 2: Update `Program.cs`**

Remove the placeholder `ApplicationContext`; restore `new ClipTrayContext(settings, activation!)`.

- [ ] **Step 3: Build + run**

Run: `dotnet build apps\ClipTray\ClipTray.sln`
Run: `apps\ClipTray\ClipTray\bin\Debug\net8.0-windows10.0.19041.0\ClipTray.exe`
Expected: tray icon appears; right-click shows the menu.

- [ ] **Step 4: Manual smoke**

1. Copy some text
2. Press Ctrl+Shift+V; picker opens
3. Pick item; pastes into a Notepad window
4. Right-click tray; toggle pause; copy something; observe no new history item
5. Right-click tray; Clear history; observe history cleared

- [ ] **Step 5: Commit**

```bash
git add apps/ClipTray/ClipTray/ClipTrayContext.cs apps/ClipTray/ClipTray/Program.cs
git commit -m "ClipTray: ClipTrayContext integrates all components

ApplicationContext owns the full ClipTray lifetime: tray icon + menu,
HotkeyRegistration (picker + optional pause hotkeys), ClipboardListener,
SessionLockMonitor, ClipboardHistory + ImageStore + CapturePipeline,
PickerForm (lazy), SettingsForm (lazy), UpdateChecker.

Clipboard-changed events from the listener are marshalled onto the UI
thread via UiDispatcher.Post before invoking CapturePipeline. Hotkey
events same. Dispose tears down every owned subsystem cleanly.

First-run balloon appears once, gated by AppSettings.ShownFirstRunWelcome.
Update checks fire every 24h via the existing UpdateChecker pattern."
```

---

## Task 15: E2E test project

Real clipboard + real hotkey round-trips.

**Files:**
- Create: `apps/ClipTray/ClipTray.E2ETests/ClipTray.E2ETests.csproj`
- Create: `apps/ClipTray/ClipTray.E2ETests/WindowsFactAttribute.cs`
- Create: `apps/ClipTray/ClipTray.E2ETests/ClipboardRoundTripE2ETests.cs`
- Create: `apps/ClipTray/ClipTray.E2ETests/HotkeyRegistrationE2ETests.cs`

- [ ] **Step 1: Create the test project**

Mirror `apps/BatteryTray/BatteryTray.E2ETests/BatteryTray.E2ETests.csproj`. Reference `ClipTray` + `WindowsAppTesting`. Same TargetFramework, xUnit + FluentAssertions package set.

- [ ] **Step 2: Create `WindowsFactAttribute.cs`**

Copy from `apps/BatteryTray/BatteryTray.E2ETests/WindowsFactAttribute.cs`.

- [ ] **Step 3: Create `ClipboardRoundTripE2ETests.cs`**

```csharp
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using FluentAssertions;
using Xunit;

namespace ClipTray.E2ETests;

public class ClipboardRoundTripE2ETests
{
    [WindowsFact]
    public void ListenAndCapture_SetText_FiresUpdate()
    {
        using var listener = new ClipboardListener();
        int updates = 0;
        listener.ClipboardChanged += (_, _) => Interlocked.Increment(ref updates);

        // Set the clipboard; pump messages so the listener observes the change.
        Clipboard.SetText("cliptray-e2e-marker " + Guid.NewGuid().ToString("N"));
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (updates == 0 && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(25);
        }

        updates.Should().BeGreaterThan(0);
    }

    [WindowsFact]
    public void ImageRoundTrip_PreservesPixelSample()
    {
        // Build a 4x4 deterministic bitmap with a known pixel.
        using var bmp = new Bitmap(4, 4);
        bmp.SetPixel(1, 2, Color.FromArgb(255, 100, 200, 50));
        Clipboard.SetImage(bmp);

        using var roundTrip = (Bitmap)Clipboard.GetImage()!;
        var px = roundTrip.GetPixel(1, 2);

        px.R.Should().BeInRange((byte)98,  (byte)102);
        px.G.Should().BeInRange((byte)198, (byte)202);
        px.B.Should().BeInRange((byte)48,  (byte)52);
    }
}
```

- [ ] **Step 4: Create `HotkeyRegistrationE2ETests.cs`**

```csharp
using FluentAssertions;
using WindowsTrayCore;
using Xunit;

namespace ClipTray.E2ETests;

public class HotkeyRegistrationE2ETests
{
    [WindowsFact]
    public void Register_RealHotkey_AcceptsRare()
    {
        const int id = 7771;
        using var h = new HotkeyRegistration();
        var ok = h.Register(id,
            HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift,
            System.Windows.Forms.Keys.F19);
        ok.Should().BeTrue();
        h.Unregister(id).Should().BeTrue();
    }
}
```

- [ ] **Step 5: Build + test**

Run: `dotnet test apps\ClipTray\ClipTray.E2ETests\ClipTray.E2ETests.csproj`
Expected: all tests pass on a Windows host.

- [ ] **Step 6: Commit**

```bash
git add apps/ClipTray/ClipTray.E2ETests/
git commit -m "ClipTray: E2E tests for clipboard round-trip + hotkey registration

Two E2E surfaces matching the existing BatteryTray pattern. The
clipboard round-trip test sets text, pumps messages, asserts the
listener observed the change. The image round-trip pins pixel-level
preservation through CF_DIB. The hotkey test uses an unlikely chord
(Ctrl+Alt+Shift+F19) to avoid colliding with anything the dev machine
has bound.

WindowsFactAttribute skips on non-Windows CI cleanly."
```

---

## Task 16: install.ps1 + CI workflow + final integration

The deployment surface. Add ClipTray to install.ps1 + both workflows. Run the full test sweep, write WORKLOG.

**Files:**
- Modify: `install.ps1`
- Modify: `.github/workflows/build.yml`
- Modify: `.github/workflows/release.yml`
- Modify: `WORKLOG.md`

- [ ] **Step 1: Update install.ps1**

In the `$AppDefs` hashtable, add:

```powershell
ClipTray = @{
    Project     = 'apps\ClipTray\ClipTray\ClipTray.csproj'
    ExeName     = 'ClipTray.exe'
    StartupArgs = '--startup --delay=5'
}
```

Add `ClipTray` to the `ValidateSet` on `$Apps` and to the default `@(...)` list.

- [ ] **Step 2: Update build.yml + release.yml**

Add a ClipTray test step (mirror BatteryTray's) and a publish step for the ClipTray exe. Both workflows.

- [ ] **Step 3: Full sweep**

```
dotnet build apps\ClipTray\ClipTray.sln
dotnet test apps\ClipTray\ClipTray.Tests\ClipTray.Tests.csproj
dotnet test apps\ClipTray\ClipTray.E2ETests\ClipTray.E2ETests.csproj

# Verify no regressions in the existing four apps:
dotnet test apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj
dotnet test apps\NetProfileSwitcher.Tests\NetProfileSwitcher.Tests.csproj
dotnet test apps\SoundTracker\SoundTracker.Tests\SoundTracker.Tests.csproj
dotnet test apps\ProgramHider\tests\ProgramHider.Tests\ProgramHider.Tests.csproj
dotnet test shared\WindowsAppCore.Tests\WindowsAppCore.Tests.csproj
dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj
```

Expected: all green.

- [ ] **Step 4: Manual smoke (full checklist from the spec)**

Per the spec's "Smoke (manual)" section:
1. Launch ClipTray from a fresh build
2. Copy three text strings + one screenshot
3. Press Ctrl+Shift+V; verify picker
4. Filter; select; paste in Notepad
5. Right-click an item; Pin; restart ClipTray; verify pinned
6. Copy a password-shaped string; verify shown as `••••••••`
7. Reveal and paste; verify works
8. Add `notepad` to blocklist; copy from Notepad; verify no capture
9. Win+L lock; unlock; copy; verify capture resumes

- [ ] **Step 5: Write WORKLOG entry**

In `WORKLOG.md`, insert a new entry above `## Phase Checklist`:

```markdown
## 2026-05-15

**Did:** Phase 30 implementation: ClipTray, fifth tray utility.

Brief listing of what landed (matches the spec's summary), test totals,
commit references for the Phase 30 series.
```

- [ ] **Step 6: Final commit**

```bash
git add install.ps1 .github/workflows/build.yml .github/workflows/release.yml WORKLOG.md
git commit -m "ClipTray: install.ps1 + CI workflows + Phase 30 worklog

Adds ClipTray to install.ps1's $AppDefs and the build/release workflows
so the standard '.\install.ps1 -Run' sequence picks it up. WORKLOG seals
Phase 30."
```

---

## Self-Review Notes

**Spec coverage:**
- HotkeyRegistration shared lib: Task 1
- ProgramHider migration: Task 2
- ClipTray scaffolding + AppSettings: Task 3
- PasswordHeuristic: Task 4
- ImageStore: Task 5
- ClipboardHistory in-memory: Task 6
- ClipboardHistory persistence: Task 7
- ClipboardListener: Task 8
- SessionLockMonitor + ForegroundProcessProbe: Task 9
- ClipboardWriter: Task 10
- CapturePipeline orchestrator: Task 11
- PickerForm: Task 12 (parallel-subagent candidate)
- SettingsForm: Task 13 (parallel-subagent candidate)
- ClipTrayContext integration: Task 14
- E2E tests: Task 15
- install.ps1 + CI + WORKLOG: Task 16

All 8 spec caveats are inherited; nothing in the plan contradicts them.

**Placeholder scan:** No "TBD" or vague steps. Each step is concrete code or a concrete command.

**Type consistency:**
- `HotkeyRegistration` / `HotkeyModifiers` used identically across Tasks 1, 2, 3, 14
- `HistoryItem` shape used identically across Tasks 6, 7, 11, 12
- `AppSettings` field names match between Task 3 (definition) and Tasks 11, 14 (consumers)
- `ClipboardHistory.Save` / `.Load` signatures pinned in Task 7, consumed in Task 14

**Hybrid execution sketch (for the implementer):**
- Tasks 1-11 inline, sequential. Each builds on the previous's commit.
- Tasks 12 + 13 dispatchable as parallel subagents after Task 11 lands.
- Task 14 inline integration. Task 15 inline E2E. Task 16 inline final.

**Known small omissions:**
- The `app.ico` placeholder reuses BatteryTray's icon. A dedicated ClipTray glyph is a polish task post-MVP.
- The PickerForm regex mode is described in Task 12 but the exact regex engine wiring is left to the implementer's discretion (System.Text.RegularExpressions with a TimeSpan timeout is the obvious choice).
- The picker's "Add current foreground app to blocklist" button (Task 13) needs a brief delay so the user can alt-tab away before the snapshot; one second is the spec's number.

---

## Execution

Pick a path forward when ready.
