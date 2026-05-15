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
    private bool _disposed;

    /// <summary>Production constructor; reads system state. Live subscription wired in Task 5.</summary>
    public TrayTheme()
    {
        _isLight = ReadRegistryIsLight();
        _accent = AccentColors.Read();
        _isHighContrast = SystemInformation.HighContrast;
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Message-window cleanup added in Task 5.
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
