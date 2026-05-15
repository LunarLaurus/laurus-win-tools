using System.Linq;

namespace ClipTray;

public static class PasswordHeuristic
{
    /// <summary>
    /// Returns true if <paramref name="text"/> matches the spec's
    /// length-and-charset heuristic for a likely secret: length within
    /// [minLen, maxLen], no whitespace, and at least two of the three
    /// character classes (letter, digit, symbol).
    /// </summary>
    public static bool LooksLikeSecret(string text, int minLen = 8, int maxLen = 64)
    {
        if (text is null) return false;
        if (text.Length < minLen || text.Length > maxLen) return false;
        if (text.Any(char.IsWhiteSpace)) return false;

        int classes = 0;
        if (text.Any(char.IsLetter))                  classes++;
        if (text.Any(char.IsDigit))                   classes++;
        if (text.Any(c => !char.IsLetterOrDigit(c)))  classes++;

        return classes >= 2;
    }
}
