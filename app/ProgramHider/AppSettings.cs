using System.ComponentModel;
using System.Windows.Forms;

namespace ProgramHider;

internal sealed class AppSettings
{
    public HotkeySettings Hotkey { get; set; } = HotkeySettings.CreateDefault();
    public bool LaunchOnWindowsStartup { get; set; }
    public bool RequirePinToRestore { get; set; }
    public string PinHash { get; set; } = string.Empty;
    public List<string> AutoHideProcessNames { get; set; } = new();

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Hotkey = Hotkey.Clone(),
            LaunchOnWindowsStartup = LaunchOnWindowsStartup,
            RequirePinToRestore = RequirePinToRestore,
            PinHash = PinHash,
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

        AutoHideProcessNames = AutoHideProcessNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
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
