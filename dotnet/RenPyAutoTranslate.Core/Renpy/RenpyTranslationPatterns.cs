using System.Text.RegularExpressions;

namespace RenPyAutoTranslate.Core.Renpy;

/// <summary>
/// Regex pattern strings for Ren'Py translation line extraction (legacy Python parity; same order and semantics).
/// Keep patterns in sync if extraction rules change.
/// </summary>
public static class RenpyTranslationPatterns
{
    /// <summary>Python <c>RENPY_OLD_LINE_RE</c> (lines 8–10).</summary>
    public const string OldLine =
        @"^(\s*)old\s+""((?:[^""\\]|\\.)*)""\s*(?:#.*)?$";

    /// <summary>Python <c>RENPY_COMMENT_NARRATOR_RE</c> (lines 14–16).</summary>
    public const string CommentNarrator =
        @"^\s*#\s*""((?:[^""\\]|\\.)*)""\s*(?:#.*)?$";

    /// <summary>Python <c>REGEX_UTIL</c> (line 19).</summary>
    public const string RegexUtil = @"#\s.*\s"".*""";

    /// <summary>Python <c>extract_first_quoted_body</c> / <c>replace_first_quoted_body</c> (lines 43, 92).</summary>
    public const string QuotedSegment =
        @"""((?:[^""\\]|\\.)*)""";

    /// <summary>Python <c>fill_new_double_speaker_line</c> (line 113).</summary>
    public const string NewDoubleSpeakerPrefix =
        @"^(\s*)""((?:[^""\\]|\\.)*)""\s*""""";

    /// <summary>Python <c>parallel_translate._RPY_UNFILLED_EMPTY_NEW</c> (line 97).</summary>
    public const string UnfilledEmptyNewLine = @"^\s*""""\s*$";

    public static readonly Regex OldLineRe = new(OldLine, RegexOptions.Compiled);
    public static readonly Regex CommentNarratorRe = new(CommentNarrator, RegexOptions.Compiled);
    public static readonly Regex RegexUtilRe = new(RegexUtil, RegexOptions.Compiled);
    public static readonly Regex QuotedSegmentRe = new(QuotedSegment, RegexOptions.Compiled);
    public static readonly Regex NewDoubleSpeakerLineRe = new(NewDoubleSpeakerPrefix, RegexOptions.Compiled);
    public static readonly Regex UnfilledEmptyNewRe = new(UnfilledEmptyNewLine, RegexOptions.Multiline | RegexOptions.Compiled);
}
