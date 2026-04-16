using RenPyAutoTranslate.Core.Renpy;

namespace RenPyAutoTranslate.Core.Tests;

public class RenpyInterpolationProtectorTests
{
    [Fact]
    public void Mask_Unmask_roundtrip_preserves_pcname()
    {
        var src = "So, we should make out. Right, [pcname]?";
        var masked = RenpyInterpolationProtector.MaskBracketInterpolations(src, out var originals);
        Assert.Single(originals);
        Assert.Equal("[pcname]", originals[0]);
        Assert.DoesNotContain("[pcname]", masked);
        Assert.Contains("__RPY_0000__", masked);

        var fakeRu = masked.Replace("So, we should make out. Right,", "Итак, нам следует разобраться. Верно,");
        var restored = RenpyInterpolationProtector.UnmaskBracketInterpolations(fakeRu, originals);
        Assert.Contains("[pcname]", restored);
        Assert.DoesNotContain("имя компьютера", restored);
    }

    [Fact]
    public void FindBracketSpans_nested_is_single_span()
    {
        var s = "x [[a]] y";
        var spans = RenpyInterpolationProtector.FindBracketSpans(s);
        Assert.Single(spans);
        Assert.Equal("[[a]]", s.Substring(spans[0].Start, spans[0].End - spans[0].Start + 1));
    }

    [Fact]
    public void FindBracketSpans_adjacent_two()
    {
        var s = "a [x] b [y] c";
        var spans = RenpyInterpolationProtector.FindBracketSpans(s);
        Assert.Equal(2, spans.Count);
        Assert.Equal("[x]", s.Substring(spans[0].Start, spans[0].End - spans[0].Start + 1));
        Assert.Equal("[y]", s.Substring(spans[1].Start, spans[1].End - spans[1].Start + 1));
    }

    [Fact]
    public void FindBracketSpans_unmatched_open_bracket_ignored()
    {
        var s = "hello [world";
        var spans = RenpyInterpolationProtector.FindBracketSpans(s);
        Assert.Empty(spans);
    }
}
