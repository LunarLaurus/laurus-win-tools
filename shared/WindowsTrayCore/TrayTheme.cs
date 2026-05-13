using System.Drawing;
using Microsoft.Win32;

namespace WindowsTrayCore;

/// <summary>
/// System light/dark theme detection with semantic colour tokens.
/// Use <see cref="Current"/> for the shared singleton wired to system events.
/// Construct directly (<c>new TrayTheme(isLight: false)</c>) in tests.
/// </summary>
public sealed class TrayTheme
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static readonly TrayTheme _current = new();

    /// <summary>Singleton wired to <see cref="SystemEvents.UserPreferenceChanged"/>.</summary>
    public static TrayTheme Current => _current;

    private bool _isLight;

    /// <summary>Production constructor — reads from registry and subscribes to system events.</summary>
    public TrayTheme()
    {
        _isLight = ReadRegistryIsLight();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    /// <summary>Testing constructor — fixed theme value, no system-event subscription.</summary>
    public TrayTheme(bool isLight)
    {
        _isLight = isLight;
    }

    public bool IsLight => _isLight;

    /// <summary>Fires when the system theme changes. On the application message pump thread.</summary>
    public event EventHandler? Changed;

    // ── Semantic colour tokens ──────────────────────────────────────────────

    public Color Background => _isLight
        ? Color.FromArgb(0xf4, 0xf4, 0xf8)
        : Color.FromArgb(0x18, 0x18, 0x2d);

    public Color Surface => _isLight
        ? Color.FromArgb(0xff, 0xff, 0xff)
        : Color.FromArgb(0x24, 0x24, 0x36);

    public Color Text => _isLight
        ? Color.FromArgb(0x1a, 0x1a, 0x2e)
        : Color.FromArgb(0xe0, 0xde, 0xe4);

    public Color TextMuted => _isLight
        ? Color.FromArgb(0x77, 0x77, 0x90)
        : Color.FromArgb(0x6e, 0x6a, 0x86);

    public Color Accent => _isLight
        ? Color.FromArgb(0x5a, 0x4f, 0xd4)
        : Color.FromArgb(0x7c, 0x6f, 0xf7);

    public Color Success => _isLight
        ? Color.FromArgb(0x2d, 0x8a, 0x4e)
        : Color.FromArgb(0x50, 0xc8, 0x78);

    public Color Error => _isLight
        ? Color.FromArgb(0xc0, 0x29, 0x3a)
        : Color.FromArgb(0xe0, 0x55, 0x66);

    public Color Field => _isLight
        ? Color.FromArgb(0xeb, 0xeb, 0xf5)
        : Color.FromArgb(0x2a, 0x2a, 0x40);

    // ── Testing hook ───────────────────────────────────────────────────────

    /// <summary>Simulate a system preference change in tests without touching SystemEvents.</summary>
    internal void SimulatePreferenceChanged(bool isLight)
    {
        if (isLight == _isLight) return;
        _isLight = isLight;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ── Internals ──────────────────────────────────────────────────────────

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General) return;
        var newLight = ReadRegistryIsLight();
        if (newLight == _isLight) return;
        _isLight = newLight;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static bool ReadRegistryIsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey, writable: false);
            if (key?.GetValue("SystemUsesLightTheme") is int sys) return sys != 0;
            if (key?.GetValue("AppsUseLightTheme") is int app) return app != 0;
        }
        catch { }
        return false;
    }
}
