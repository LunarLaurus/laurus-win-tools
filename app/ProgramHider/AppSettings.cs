using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace ProgramHider;

internal sealed class AppSettings
{
    public HotkeySettings Hotkey { get; set; } = HotkeySettings.CreateDefault();
    public bool LaunchOnWindowsStartup { get; set; }
    public bool RequirePinToRestore { get; set; }
    public string PinHash { get; set; } = string.Empty;
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
            RequirePinToRestore = RequirePinToRestore,
            PinHash = PinHash,
            WindowRules = WindowRules.Select(rule => rule.Clone()).ToList(),
            AutoHideProcessNames = AutoHideProcessNames.ToList()
        };
    }

    public void Normalize()
    {
        Hotkey ??= HotkeySettings.CreateDefault();
        Hotkey.Normalize();
        PinHash = PinHash?.Trim() ?? string.Empty;

        if (!RequirePinToRestore)
        {
            PinHash = string.Empty;
        }

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
