using System.Text.RegularExpressions;

namespace RenPyAutoTranslate.Core.Renpy;

/// <summary>Regex and string helpers ported from translation_utils.py.</summary>
public static class RenpyStringUtils
{
    /// <summary>Python <c>RENPY_OLD_LINE_RE</c>.</summary>
    public static readonly Regex RenpyOldLineRe = RenpyTranslationPatterns.OldLineRe;

    /// <summary>Python <c>RENPY_COMMENT_NARRATOR_RE</c>.</summary>
    public static readonly Regex RenpyCommentNarratorRe = RenpyTranslationPatterns.CommentNarratorRe;

    /// <summary>Python <c>REGEX_UTIL</c>.</summary>
    public static readonly Regex RegexUtil = RenpyTranslationPatterns.RegexUtilRe;

    public static string? ExtractQuotedBodyAfterOld(string line)
    {
        var s = line.TrimEnd('\r', '\n');
        var m = RenpyTranslationPatterns.OldLineRe.Match(s);
        return m.Success ? m.Groups[2].Value : null;
    }

    public static string? ExtractQuotedBodyNarratorComment(string line)
    {
        var s = line.TrimEnd('\r', '\n');
        var m = RenpyTranslationPatterns.CommentNarratorRe.Match(s);
        return m.Success ? m.Groups[1].Value : null;
    }

    public static string? ExtractFirstQuotedBody(string line)
    {
        var s = line.TrimEnd('\r', '\n');
        var m = RenpyTranslationPatterns.QuotedSegmentRe.Match(s);
        return m.Success ? m.Groups[1].Value : null;
    }

    public static IReadOnlyList<string> ExtractAllQuotedBodies(string line)
    {
        var s = line.TrimEnd('\r', '\n');
        var list = new List<string>();
        foreach (Match m in RenpyTranslationPatterns.QuotedSegmentRe.Matches(s))
            list.Add(m.Groups[1].Value);
        return list;
    }

    /// <summary># "Speaker" "Dialogue" — two+ quoted strings on a # line.</summary>
    public static (string Speaker, string Dialogue)? ExtractSpeakerAndDialogueFromHashComment(string line)
    {
        var s = line.TrimEnd('\r', '\n');
        if (!s.TrimStart().StartsWith("#", StringComparison.Ordinal))
            return null;
        var bodies = ExtractAllQuotedBodies(s);
        if (bodies.Count >= 2)
            return (bodies[0], bodies[1]);
        return null;
    }

    public static string RenpyEscapeForDoubleQuotedString(string? s)
    {
        if (s is null)
            return "";
        s = s.Replace("\\", "\\\\", StringComparison.Ordinal);
        s = s.Replace("\"", "\\\"", StringComparison.Ordinal);
        s = s.Replace("\r\n", "\n", StringComparison.Ordinal);
        s = s.Replace("\r", "\n", StringComparison.Ordinal);
        s = s.Replace("\n", "\\n", StringComparison.Ordinal);
        return s;
    }

    /// <summary>Python <c>replace_first_quoted_body</c> — uses <c>re.search</c> on the raw line (no rstrip).</summary>
    public static string? ReplaceFirstQuotedBody(string line, string translatedRaw)
    {
        var escaped = RenpyEscapeForDoubleQuotedString(translatedRaw);
        var m = RenpyTranslationPatterns.QuotedSegmentRe.Match(line);
        if (!m.Success)
            return null;
        return line[..m.Index] + '"' + escaped + '"' + line[(m.Index + m.Length)..];
    }

    public static string? FillNewEmptyStringLine(string line, string translatedRaw)
    {
        var escaped = RenpyEscapeForDoubleQuotedString(translatedRaw);
        const string emptyPair = "\"\"";
        var idx = line.IndexOf(emptyPair, StringComparison.Ordinal);
        if (idx < 0)
            return null;
        var replacement = '"' + escaped + '"';
        return line[..idx] + replacement + line[(idx + emptyPair.Length)..];
    }

    public static string? FillNewDoubleSpeakerLine(string line, string speakerTr, string dialogueTr)
    {
        var m = RenpyTranslationPatterns.NewDoubleSpeakerLineRe.Match(line);
        if (!m.Success)
            return null;
        var indent = m.Groups[1].Value;
        var escS = RenpyEscapeForDoubleQuotedString(speakerTr);
        var escD = RenpyEscapeForDoubleQuotedString(dialogueTr);
        var rest = line[m.Length..];
        return $"{indent}\"{escS}\" \"{escD}\"{rest}";
    }

    /// <summary>Python <c>get_string_to_translate</c> — same branch order (old → narrator → REGEX_UTIL + first quote).</summary>
    public static string? GetStringToTranslate(string line)
    {
        var body = ExtractQuotedBodyAfterOld(line);
        if (body is not null)
            return body;
        body = ExtractQuotedBodyNarratorComment(line);
        if (body is not null)
            return body;
        var s = line.TrimEnd('\r', '\n');
        if (RenpyTranslationPatterns.RegexUtilRe.IsMatch(s))
            return ExtractFirstQuotedBody(line);
        return null;
    }

    public static bool RpyHasUnfilledEmptyNewStrings(string fileContent)
    {
        return RenpyTranslationPatterns.UnfilledEmptyNewRe.IsMatch(fileContent);
    }
}
