using System.Drawing;
using WindowsTrayCore;

namespace BatteryTray;

/// <summary>
/// Renders the battery tray icon from the latest <see cref="BatteryState"/>.
/// Uses <see cref="BatteryRenderKey"/> to compute a dirty flag so
/// <see cref="TrayIconManager.RequestRefresh"/> can short-circuit when the
/// rendered output would be identical.
/// </summary>
internal sealed class BatteryIconProvider : ITrayIconProvider
{
    private readonly Func<AppSettings> _settingsAccessor;
    private BatteryState? _state;
    private BatteryRenderKey? _lastRenderedKey;

    public BatteryIconProvider(Func<AppSettings> settingsAccessor)
    {
        _settingsAccessor = settingsAccessor;
    }

    /// <summary>
    /// Update the current battery state. Marks the provider dirty when the
    /// render key changes; <c>RequestRefresh</c> will then re-render.
    /// </summary>
    public void Update(BatteryState state)
    {
        _state = state;
    }

    public bool HasChanged
    {
        get
        {
            if (_state is null) return false;
            var key = BatteryRenderKey.From(_state.Value, _settingsAccessor());
            return _lastRenderedKey is null || _lastRenderedKey.Value != key;
        }
    }

    public Icon Render(TrayTheme theme)
    {
        var state = _state ?? throw new InvalidOperationException(
            "BatteryIconProvider.Render called before Update.");
        var settings = _settingsAccessor();
        _lastRenderedKey = BatteryRenderKey.From(state, settings);
        return IconRenderer.Create(state, settings);
    }
}
