using RenPyAutoTranslate.Core.Renpy;

namespace RenPyAutoTranslate.Core.Tests;

/// <summary>
/// Parity checks for <see cref="RenpyStringUtils"/> line extraction (same cases as the legacy Python tooling).
/// </summary>
public class RenpyStringUtilsPythonParityTests
{
    public static TheoryData<string, string?> GetStringCases => new()
    {
        { "    old \"Type a image name\"", "Type a image name" },
        { "    new \"\"", null },
        { "    # game/image_viewer.rpy:13", null },
        { "    # \"narrator only\"", "narrator only" },
        { "    # pc \"speaker line\"", "speaker line" },
        { "    # \"Speaker\" \"Dialogue\"", "Speaker" },
        { "    # \"A\" \"B\" \"C\"", "A" },
        { "    pc \"bare\"", null },
        { "    \"bare quote\"", null },
        { "    old \"x\" # trailing", "x" },
        { "    # \"x\" # c", "x" },
        { "    # pc\"x\"", null },
        { "    # pc \"x\"", "x" },
    };

    [Theory]
    [MemberData(nameof(GetStringCases))]
    public void GetStringToTranslate_matches_python(string line, string? expected)
    {
        Assert.Equal(expected, RenpyStringUtils.GetStringToTranslate(line));
    }

    [Fact]
    public void ExtractSpeaker_two_quoted_hash_line_matches_python()
    {
        var line = "    # \"Speaker\" \"Dialogue\"";
        var p = RenpyStringUtils.ExtractSpeakerAndDialogueFromHashComment(line);
        Assert.NotNull(p);
        Assert.Equal("Speaker", p.Value.Speaker);
        Assert.Equal("Dialogue", p.Value.Dialogue);
    }

    [Fact]
    public void RegexUtil_matches_substring_with_leading_indent_like_python_search()
    {
        var line = "    # pc \"hello\"";
        Assert.Matches(RenpyTranslationPatterns.RegexUtil, line.TrimEnd('\r', '\n'));
    }

    [Fact]
    public void Pattern_constants_match_translation_utils_line_numbers_documented()
    {
        Assert.Equal(RenpyTranslationPatterns.OldLine, RenpyTranslationPatterns.OldLineRe.ToString());
        Assert.Equal(RenpyTranslationPatterns.CommentNarrator, RenpyTranslationPatterns.CommentNarratorRe.ToString());
        Assert.Equal(RenpyTranslationPatterns.RegexUtil, RenpyTranslationPatterns.RegexUtilRe.ToString());
    }
}
