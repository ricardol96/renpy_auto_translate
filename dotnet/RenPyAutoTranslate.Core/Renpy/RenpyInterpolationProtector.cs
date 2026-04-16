namespace RenPyAutoTranslate.Core.Renpy;

/// <summary>
/// Replaces Ren'Py <c>[ ... ]</c> interpolations with stable tokens before machine translation,
/// then restores the original bracket expressions afterward so APIs do not translate variable names.
/// </summary>
public static class RenpyInterpolationProtector
{
    /// <summary>Token format; fixed width index avoids <c>__RPY_1__</c> matching inside <c>__RPY_10__</c>.</summary>
    private static string Token(int k) => $"__RPY_{k:D4}__";

    /// <summary>
    /// Finds non-overlapping spans of balanced square brackets. Nested <c>[[ ... ]]</c> is one span.
    /// </summary>
    public static List<(int Start, int End)> FindBracketSpans(string s)
    {
        var list = new List<(int Start, int End)>();
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] != '[')
                continue;
            var depth = 1;
            var j = i + 1;
            for (; j < s.Length && depth > 0; j++)
            {
                if (s[j] == '[')
                    depth++;
                else if (s[j] == ']')
                    depth--;
            }

            if (depth != 0)
                continue;
            list.Add((i, j - 1));
            i = j - 1;
        }

        return list;
    }

    /// <summary>
    /// Replaces each bracket span with <see cref="Token"/> in left-to-right order (index 0, 1, …).
    /// </summary>
    public static string MaskBracketInterpolations(string text, out List<string> originals)
    {
        originals = new List<string>();
        var spans = FindBracketSpans(text);
        if (spans.Count == 0)
            return text;

        foreach (var (start, end) in spans)
            originals.Add(text.Substring(start, end - start + 1));

        var result = text;
        for (var k = spans.Count - 1; k >= 0; k--)
        {
            var (start, end) = spans[k];
            var token = Token(k);
            result = result.Remove(start, end - start + 1).Insert(start, token);
        }

        return result;
    }

    /// <summary>
    /// Substitutes tokens back with the original bracket expressions (highest index first).
    /// </summary>
    public static string UnmaskBracketInterpolations(string translated, IReadOnlyList<string> originals)
    {
        if (originals.Count == 0)
            return translated;
        var result = translated;
        for (var k = originals.Count - 1; k >= 0; k--)
            result = result.Replace(Token(k), originals[k], StringComparison.Ordinal);
        return result;
    }
}
