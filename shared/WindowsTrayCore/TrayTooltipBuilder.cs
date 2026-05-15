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

        // Work on a copy so Build() is idempotent.
        var working = new List<Line>(_lines);

        if (TotalLength(working) <= MaxLength)
            return JoinLines(working);

        // Drop optional lines from the tail (last-added optional first).
        for (int i = working.Count - 1; i >= 0 && TotalLength(working) > MaxLength; i--)
        {
            if (!working[i].IsRequired)
            {
                working.RemoveAt(i);
            }
        }

        if (TotalLength(working) <= MaxLength)
            return JoinLines(working);

        // Required-line overflow falls through to Task 4's truncation logic.
        // For now, return the partial join so the over-budget tests in Task 3
        // observe correct optional-drop behaviour. The required-overflow
        // tests are added in Task 4.
        return JoinLines(working);
    }

    private static int TotalLength(List<Line> lines)
    {
        if (lines.Count == 0) return 0;
        int total = lines.Count - 1; // separators
        foreach (var line in lines) total += line.Text.Length;
        return total;
    }

    private static string JoinLines(List<Line> lines)
    {
        if (lines.Count == 0) return string.Empty;
        var parts = new string[lines.Count];
        for (int i = 0; i < lines.Count; i++) parts[i] = lines[i].Text;
        return string.Join(LineSeparator, parts);
    }

    private readonly record struct Line(string Text, bool IsRequired);
}
