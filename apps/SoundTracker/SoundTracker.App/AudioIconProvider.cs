using System.Drawing;
using SoundTracker.App.Audio;
using WindowsTrayCore;

namespace SoundTracker.App;

/// <summary>
/// Renders the SoundTracker tray icon from the latest volume snapshot.
/// Tracks a render key (percent + muted + light theme flag) so
/// <c>TrayIconManager.RequestRefresh</c> can skip when state is unchanged.
/// </summary>
internal sealed class AudioIconProvider : ITrayIconProvider
{
    private EndpointVolumeSnapshot? _state;
    private RenderKey? _lastRenderedKey;

    public void Update(EndpointVolumeSnapshot snapshot)
    {
        _state = snapshot;
    }

    public bool HasChanged
    {
        get
        {
            if (_state is null) return false;
            var key = RenderKey.From(_state.Value, AppTheme.IsLightTaskbarTheme());
            return _lastRenderedKey is null || _lastRenderedKey.Value != key;
        }
    }

    public Icon Render(TrayTheme theme)
    {
        var snapshot = _state ?? throw new InvalidOperationException(
            "AudioIconProvider.Render called before Update.");
        var isLight = AppTheme.IsLightTaskbarTheme();
        _lastRenderedKey = RenderKey.From(snapshot, isLight);
        return TrayIconRenderer.Render(snapshot, isLight);
    }

    private readonly record struct RenderKey(int Percent, bool Muted, bool IsLight)
    {
        public static RenderKey From(EndpointVolumeSnapshot s, bool isLight) =>
            new(s.Percent, s.IsMuted, isLight);
    }
}
