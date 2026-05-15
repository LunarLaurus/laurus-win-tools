using System.Collections.Generic;

namespace WindowsTrayCore;

/// <summary>
/// Composes multi-line tray tooltips that fit the Win32 szTip[128] budget
/// (127 usable wide chars with NOTIFYICON_VERSION_4). Lines are tagged as
/// required or optional; optional lines drop from the tail first when over
/// budget. If required lines alone overflow, the last required line is
/// word-boundary truncated with a single-glyph ellipsis.
/// </summary>
public sealed class TrayTooltipBuilder
{
    public const int MaxLength = 127;
    public const char LineSeparator = '\n';
    public const string Ellipsis = "…";

    private readonly List<Line> _lines = new();

    public TrayTooltipBuilder AddRequired(string text)
    {
        if (text is null) throw new System.ArgumentNullException(nameof(text));
        _lines.Add(new Line(text, IsRequired: true));
        return this;
    }

    public TrayTooltipBuilder AddOptional(string text)
    {
        if (text is null) throw new System.ArgumentNullException(nameof(text));
        _lines.Add(new Line(text, IsRequired: false));
        return this;
    }

    public string Build()
    {
        if (_lines.Count == 0) return string.Empty;
        // Joining + truncation logic added in subsequent tasks.
        // For now, join with LF (covers the trivial single-line case).
        var parts = new List<string>(_lines.Count);
        foreach (var line in _lines) parts.Add(line.Text);
        return string.Join(LineSeparator, parts);
    }

    private readonly record struct Line(string Text, bool IsRequired);
}
