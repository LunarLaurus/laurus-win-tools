using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WindowsTrayCore;

/// <summary>
/// System theme detection with Fluent-style semantic colour tokens.
/// Use <see cref="Current"/> for the shared singleton.
/// </summary>
public sealed class TrayTheme : IDisposable
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static readonly TrayTheme _current = new();

    /// <summary>Singleton wired to system theme + accent changes.</summary>
    public static TrayTheme Current => _current;

    private bool _isLight;
    private Color _accent;
    private bool _isHighContrast;
    private ThemePreference _preference = ThemePreference.Auto;
    private bool _disposed;
    private readonly MessageWindow? _messageWindow;

    private const int WM_SETTINGCHANGE = 0x001A;
    private const int WM_DWMCOLORIZATIONCOLORCHANGED = 0x0320;

    /// <summary>Production constructor; subscribes to live system theme + accent broadcasts.</summary>
    public TrayTheme()
    {
        _isLight = ReadRegistryIsLight();
        _accent = AccentColors.Read();
        _isHighContrast = SystemInformation.HighContrast;
        _messageWindow = new MessageWindow(this);
        _messageWindow.CreateHandle(new CreateParams());
    }

    /// <summary>Testing constructor; no system subscription.</summary>
    internal TrayTheme(bool isLight, Color accent, bool isHighContrast)
    {
        _isLight = isLight;
        _accent = accent;
        _isHighContrast = isHighContrast;
    }

    /// <summary>Legacy testing constructor; defaults accent to Windows blue.</summary>
    public TrayTheme(bool isLight)
        : this(isLight, Color.FromArgb(0, 0x78, 0xD4), isHighContrast: false) { }

    public bool IsLight => _isLight;
    public bool IsHighContrast => _isHighContrast;
    public ThemePreference Preference => _preference;

    /// <summary>
    /// Overrides the system theme detection. <see cref="ThemePreference.Auto"/>
    /// restores system-follow; Light and Dark force the corresponding palette
    /// regardless of system state. Fires <see cref="Changed"/> if the resulting
    /// palette changes or the preference itself changed.
    /// </summary>
    public void SetOverride(ThemePreference preference)
    {
        var changed = preference != _preference;
        _preference = preference;

        bool newLight = preference switch
        {
            ThemePreference.Light => true,
            ThemePreference.Dark => false,
            _ => ReadRegistryIsLight(),
        };

        if (newLight != _isLight)
        {
            _isLight = newLight;
            changed = true;
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Fires when the system theme or accent changes. On the message-pump thread.</summary>
    public event EventHandler? Changed;

    // ── New Fluent tokens ──────────────────────────────────────────────────

    public Color Surface => _isLight
        ? Color.FromArgb(0xF4, 0xF4, 0xF8)
        : Color.FromArgb(0x18, 0x18, 0x2D);

    public Color SurfaceAlt => _isLight
        ? Color.FromArgb(0xFF, 0xFF, 0xFF)
        : Color.FromArgb(0x24, 0x24, 0x3A);

    public Color SurfaceStroke => _isLight
        ? Color.FromArgb(0xD8, 0xD8, 0xE0)
        : Color.FromArgb(0x3A, 0x3A, 0x52);

    public Color Foreground => _isLight
        ? Color.FromArgb(0x1A, 0x1A, 0x2E)
        : Color.FromArgb(0xE0, 0xDE, 0xE4);

    public Color ForegroundAlt => _isLight
        ? Color.FromArgb(0x5A, 0x5A, 0x70)
        : Color.FromArgb(0x9A, 0x95, 0xB0);

    public Color ForegroundDim => _isLight
        ? Color.FromArgb(0x90, 0x90, 0xA4)
        : Color.FromArgb(0x6E, 0x6A, 0x86);

    public Color Accent => _accent;
    public Color AccentOn => AccentColors.DeriveOn(_accent);
    public Color AccentSubtle => AccentColors.DeriveSubtle(_accent, Surface);

    public Color Warning => _isLight
        ? Color.FromArgb(0xB4, 0x53, 0x09)
        : Color.FromArgb(0xFB, 0xBF, 0x24);

    public Color Error => _isLight
        ? Color.FromArgb(0xC0, 0x29, 0x3A)
        : Color.FromArgb(0xE0, 0x55, 0x66);

    public Color Success => _isLight
        ? Color.FromArgb(0x2D, 0x8A, 0x4E)
        : Color.FromArgb(0x50, 0xC8, 0x78);

    // ── Legacy token aliases (deleted in Task 14) ──────────────────────────
    // Kept transiently so apps build during the migration commits.

    public Color Background => Surface;
    public Color Text => Foreground;
    public Color TextMuted => ForegroundAlt;
    public Color Field => SurfaceAlt;

    // ── Testing hooks ──────────────────────────────────────────────────────

    internal void SimulatePreferenceChanged(bool isLight)
    {
        if (isLight == _isLight) return;
        _isLight = isLight;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void SimulateAccentChanged(Color accent)
    {
        if (accent == _accent) return;
        _accent = accent;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void SimulateHighContrastChanged(bool on)
    {
        if (on == _isHighContrast) return;
        _isHighContrast = on;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _messageWindow?.DestroyHandle();
    }

    // ── Live change handling ───────────────────────────────────────────────

    private sealed class MessageWindow : NativeWindow
    {
        private readonly TrayTheme _owner;
        public MessageWindow(TrayTheme owner) => _owner = owner;

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_SETTINGCHANGE:
                    _owner.OnSettingChange();
                    break;
                case WM_DWMCOLORIZATIONCOLORCHANGED:
                    _owner.OnAccentChanged();
                    break;
            }
            base.WndProc(ref m);
        }
    }

    private void OnSettingChange()
    {
        // WM_SETTINGCHANGE broadcasts cover the AppsUseLightTheme flip
        // ("ImmersiveColorSet" in lParam) and high-contrast toggles. We
        // re-read both sources and fire Changed only on a real delta.
        var hcChanged = SystemInformation.HighContrast != _isHighContrast;
        if (hcChanged)
        {
            _isHighContrast = SystemInformation.HighContrast;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        // Light/dark flips are ignored when a non-Auto override is active.
        if (_preference != ThemePreference.Auto) return;

        var newLight = ReadRegistryIsLight();
        if (newLight != _isLight)
        {
            _isLight = newLight;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnAccentChanged()
    {
        var newAccent = AccentColors.Read();
        if (newAccent == _accent) return;
        _accent = newAccent;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ── Internals ──────────────────────────────────────────────────────────

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
