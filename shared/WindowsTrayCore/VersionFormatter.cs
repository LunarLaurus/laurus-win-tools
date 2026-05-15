namespace WindowsTrayCore;

/// <summary>
/// Helpers for rendering SemVer version strings in tray UI surfaces
/// (tooltips, About dialogs, menu items).
/// </summary>
public static class VersionFormatter
{
    /// <summary>
    /// Strips SemVer prerelease (`-alpha.1`) and build-metadata (`+abc1234`)
    /// suffixes for display. Returns "unknown" for null or whitespace input.
    ///
    /// Display-only: log records and update-comparison code should keep the
    /// full string to preserve build provenance.
    /// </summary>
    public static string TrimSemverSuffix(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "unknown";
        var idx = raw.IndexOfAny(['+', '-']);
        return idx < 0 ? raw : raw[..idx];
    }
}
