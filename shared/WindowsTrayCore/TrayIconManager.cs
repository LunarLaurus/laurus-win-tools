using System.Drawing;

namespace WindowsTrayCore;

/// <summary>
/// Owns the icon lifecycle for a <see cref="NotifyIcon"/>: renders via the provider,
/// disposes previous GDI resources, and re-renders automatically on theme change.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ITrayIconProvider _provider;
    private readonly TrayTheme _theme;
    private Icon? _current;

    public TrayIconManager(NotifyIcon icon, ITrayIconProvider provider, TrayTheme theme)
    {
        _icon = icon;
        _provider = provider;
        _theme = theme;
        _theme.Changed += OnThemeChanged;
    }

    /// <summary>
    /// Renders a new icon from the provider and assigns it to the <see cref="NotifyIcon"/>.
    /// Disposes the previously held icon.
    /// </summary>
    public void ForceRefresh()
    {
        var next = _provider.Render(_theme);
        var prev = _current;
        _current = next;
        _icon.Icon = next;
        prev?.Dispose();
    }

    /// <summary>
    /// Renders a new icon only when <see cref="ITrayIconProvider.HasChanged"/> is <c>true</c>.
    /// Use this for routine domain-state updates. Theme changes always call
    /// <see cref="ForceRefresh"/> directly to bypass the dirty-flag.
    /// </summary>
    public void RequestRefresh()
    {
        if (_provider.HasChanged)
            ForceRefresh();
    }

    public void Dispose()
    {
        _theme.Changed -= OnThemeChanged;
        _current?.Dispose();
        _current = null;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ForceRefresh();
}
