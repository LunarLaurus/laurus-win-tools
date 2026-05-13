using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace ProgramHider;

// Serializable root settings model for tray behavior, security options, and
// structured window rules.
internal sealed class AppSettings
{
    public HotkeySettings Hotkey { get; set; } = HotkeySettings.CreateDefault();
    public bool LaunchOnWindowsStartup { get; set; }
    public int StartupDelaySeconds { get; set; }
    public bool RestoreWithoutFocus { get; set; }
    public bool RequirePinToRestore { get; set; }
    public string PinHash { get; set; } = string.Empty;
    public string RestoreAllPinHash { get; set; } = string.Empty;
    public int UnlockTimeoutMinutes { get; set; } = 5;
    public bool RestoreHiddenWindowsOnSessionLock { get; set; }
    public bool RestoreHiddenWindowsOnSuspend { get; set; }
    public List<WindowRule> WindowRules { get; set; } = new();

    // Kept for v0.0.x settings migration.
    [EditorBrowsable(EditorBrowsableState.Never)]
    [JsonInclude]
    public List<string> AutoHideProcessNames { get; set; } = new();

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Hotkey = Hotkey.Clone(),
            LaunchOnWindowsStartup = LaunchOnWindowsStartup,
            StartupDelaySeconds = StartupDelaySeconds,
            RestoreWithoutFocus = RestoreWithoutFocus,
            RequirePinToRestore = RequirePinToRestore,
            PinHash = PinHash,
            RestoreAllPinHash = RestoreAllPinHash,
            UnlockTimeoutMinutes = UnlockTimeoutMinutes,
            RestoreHiddenWindowsOnSessionLock = RestoreHiddenWindowsOnSessionLock,
            RestoreHiddenWindowsOnSuspend = RestoreHiddenWindowsOnSuspend,
            WindowRules = WindowRules.Select(rule => rule.Clone()).ToList(),
            AutoHideProcessNames = AutoHideProcessNames.ToList()
        };
    }

    public void Normalize()
    {
        Hotkey ??= HotkeySettings.CreateDefault();
        Hotkey.Normalize();
        PinHash = PinHash?.Trim() ?? string.Empty;
        RestoreAllPinHash = RestoreAllPinHash?.Trim() ?? string.Empty;
        StartupDelaySeconds = Math.Clamp(StartupDelaySeconds, 0, 300);
        UnlockTimeoutMinutes = Math.Clamp(UnlockTimeoutMinutes, 0, 120);

        WindowRules = WindowRules
            .Where(rule => rule is not null)
            .Select(rule =>
            {
                rule.Normalize();
                return rule;
            })
            .Where(rule => rule.HasAnyMatchField)
            .GroupBy(rule => rule.GetIdentityKey(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(rule => rule.RuleName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!RequirePinToRestore && !WindowRules.Any(rule => rule.RequirePinOnRestore))
        {
            PinHash = string.Empty;
        }

        AutoHideProcessNames = AutoHideProcessNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var processName in AutoHideProcessNames)
        {
            if (WindowRules.Any(rule =>
                    string.Equals(rule.MatchProcessName, processName, StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrWhiteSpace(rule.MatchTitleContains) &&
                    string.IsNullOrWhiteSpace(rule.MatchClassName)))
            {
                continue;
            }

            WindowRules.Add(new WindowRule
            {
                RuleName = $"{processName} auto-hide",
                MatchProcessName = processName,
                AutoHideOnMinimize = true
            });
        }

        WindowRules = WindowRules
            .OrderBy(rule => rule.RuleName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        AutoHideProcessNames.Clear();
    }
}

// Describes the global hide hotkey and knows how to convert itself into
// Win32 modifier flags and user-facing text.
internal sealed class HotkeySettings
{
    public bool Control { get; set; } = true;
    public bool Shift { get; set; } = true;
    public bool Alt { get; set; }
    public bool Windows { get; set; }
    public Keys Key { get; set; } = Keys.H;

    public HotkeySettings Clone()
    {
        return new HotkeySettings
        {
            Control = Control,
            Shift = Shift,
            Alt = Alt,
            Windows = Windows,
            Key = Key
        };
    }

    public int ToNativeModifiers()
    {
        var modifiers = 0;
        if (Control)
        {
            modifiers |= NativeMethods.MOD_CONTROL;
        }

        if (Shift)
        {
            modifiers |= NativeMethods.MOD_SHIFT;
        }

        if (Alt)
        {
            modifiers |= NativeMethods.MOD_ALT;
        }

        if (Windows)
        {
            modifiers |= NativeMethods.MOD_WIN;
        }

        modifiers |= NativeMethods.MOD_NOREPEAT;
        return modifiers;
    }

    public string ToDisplayString()
    {
        var parts = new List<string>();
        if (Control)
        {
            parts.Add("Ctrl");
        }

        if (Shift)
        {
            parts.Add("Shift");
        }

        if (Alt)
        {
            parts.Add("Alt");
        }

        if (Windows)
        {
            parts.Add("Win");
        }

        var keyText = new KeysConverter().ConvertToString(Key) ?? Key.ToString();
        parts.Add(keyText);
        return string.Join("+", parts);
    }

    public void Normalize()
    {
        if (!Control && !Shift && !Alt && !Windows)
        {
            Control = true;
        }

        if (Key == Keys.None)
        {
            Key = Keys.H;
        }
    }

    public static HotkeySettings CreateDefault()
    {
        return new HotkeySettings();
    }
}
