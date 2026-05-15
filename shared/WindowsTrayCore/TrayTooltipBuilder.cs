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

        // Required lines alone exceed budget. Keep all but the last intact;
        // truncate the last with an ellipsis at a word boundary when possible.
        return TruncateLastRequired(working);
    }

    private static string TruncateLastRequired(List<Line> required)
    {
        // Compose the prefix (all lines before the last) intact.
        // If a single required line overflows, prefix is empty.
        var prefix = required.Count > 1
            ? string.Join(LineSeparator, EnumerateExceptLast(required))
            : string.Empty;

        int prefixCost = prefix.Length + (required.Count > 1 ? 1 : 0);
        int lastBudget = MaxLength - prefixCost - Ellipsis.Length;

        if (lastBudget <= 0)
        {
            // Earlier required lines alone are at or past budget. Hard-cut
            // the whole join. Degenerate case for any realistic tooltip; the
            // contract is "fit at any cost, do not exceed".
            var combined = string.Join(LineSeparator, EnumerateAllText(required));
            return combined.Length <= MaxLength ? combined : combined[..MaxLength];
        }

        var last = required[required.Count - 1].Text;
        var truncated = WordBoundaryTruncate(last, lastBudget);
        return prefix.Length > 0
            ? prefix + LineSeparator + truncated + Ellipsis
            : truncated + Ellipsis;
    }

    private static IEnumerable<string> EnumerateExceptLast(List<Line> lines)
    {
        for (int i = 0; i < lines.Count - 1; i++) yield return lines[i].Text;
    }

    private static IEnumerable<string> EnumerateAllText(List<Line> lines)
    {
        foreach (var line in lines) yield return line.Text;
    }

    private static string WordBoundaryTruncate(string text, int budget)
    {
        if (text.Length <= budget) return text;

        // Look for the last ASCII space within the budget window.
        int lastSpace = text.LastIndexOf(' ', budget - 1, budget);
        if (lastSpace >= budget / 2)
        {
            return text[..lastSpace].TrimEnd();
        }

        return text[..budget].TrimEnd();
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
