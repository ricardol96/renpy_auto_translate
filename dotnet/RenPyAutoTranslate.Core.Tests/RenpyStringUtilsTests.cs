using RenPyAutoTranslate.Core.Renpy;

namespace RenPyAutoTranslate.Core.Tests;

public class RenpyStringUtilsTests
{
    [Fact]
    public void ExtractQuotedBodyAfterOld_simple()
    {
        var body = RenpyStringUtils.ExtractQuotedBodyAfterOld(@"  old ""hello""");
        Assert.Equal("hello", body);
    }

    [Fact]
    public void RenpyEscape_escapes_quotes()
    {
        var e = RenpyStringUtils.RenpyEscapeForDoubleQuotedString("a\"b");
        Assert.Equal(@"a\""b", e);
    }

    [Fact]
    public void ReplaceFirstQuotedBody_replaces_first_string()
    {
        var line = @"    new ""orig""";
        var r = RenpyStringUtils.ReplaceFirstQuotedBody(line, "x\"y");
        Assert.Equal(@"    new ""x\""y""", r);
    }
}
