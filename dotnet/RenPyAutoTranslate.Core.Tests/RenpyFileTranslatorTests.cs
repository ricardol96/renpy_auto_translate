using RenPyAutoTranslate.Core.Renpy;
using RenPyAutoTranslate.Core.Translation;

namespace RenPyAutoTranslate.Core.Tests;

public class RenpyFileTranslatorTests : IDisposable
{
    private readonly string _tempRoot;

    public RenpyFileTranslatorTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "rpat_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, true);
        }
        catch
        {
            /* ignore */
        }
    }

    private sealed class EchoProvider : ITranslationProvider
    {
        public Task<string> TranslateAsync(
            string text,
            string sourceLang,
            string targetLang,
            CancellationToken cancellationToken = default) =>
            Task.FromResult("T:" + text);
    }

    [Fact]
    public async Task TranslateFileAsync_preserves_bracket_interpolation_in_dialogue()
    {
        var tl = Path.Combine(_tempRoot, "game", "tl");
        var lang = Path.Combine(tl, "english");
        Directory.CreateDirectory(lang);
        var origin = Path.Combine(lang, "vars.rpy");
        await File.WriteAllTextAsync(origin,
            "    old \"So, [pcname]?\"\n    new \"\"\n");

        var outRoot = Path.Combine(_tempRoot, "out_vars");
        var translator = new RenpyFileTranslator(new EchoProvider());
        await translator.TranslateFileAsync(origin, tl, outRoot, "en", "en");

        var rel = Path.GetRelativePath(tl, origin);
        var written = await File.ReadAllTextAsync(Path.Combine(outRoot, rel));
        Assert.Contains("T:So, [pcname]?", written);
    }

    [Fact]
    public async Task TranslateFileAsync_writes_new_line_with_translation()
    {
        var tl = Path.Combine(_tempRoot, "game", "tl");
        var lang = Path.Combine(tl, "english");
        Directory.CreateDirectory(lang);
        var origin = Path.Combine(lang, "script.rpy");
        await File.WriteAllTextAsync(origin,
            "    old \"hello\"\n    new \"\"\n");

        var outRoot = Path.Combine(_tempRoot, "out");
        var translator = new RenpyFileTranslator(new EchoProvider());
        await translator.TranslateFileAsync(origin, tl, outRoot, "en", "en");

        var rel = Path.GetRelativePath(tl, origin);
        var written = await File.ReadAllTextAsync(Path.Combine(outRoot, rel));
        Assert.Contains("old \"hello\"", written);
        Assert.Contains("new \"T:hello\"", written);
    }

    private sealed class ThrowingProvider : ITranslationProvider
    {
        public Task<string> TranslateAsync(
            string text,
            string sourceLang,
            string targetLang,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("simulated API failure");
    }

    [Fact]
    public async Task TranslateFileAsync_provider_throws_does_not_create_output_file()
    {
        var tl = Path.Combine(_tempRoot, "game", "tl");
        var lang = Path.Combine(tl, "english");
        Directory.CreateDirectory(lang);
        var origin = Path.Combine(lang, "bad.rpy");
        await File.WriteAllTextAsync(origin,
            "    old \"hello\"\n    new \"\"\n");

        var outRoot = Path.Combine(_tempRoot, "out_throw");
        var translator = new RenpyFileTranslator(new ThrowingProvider());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            translator.TranslateFileAsync(origin, tl, outRoot, "en", "pt"));

        var rel = Path.GetRelativePath(tl, origin);
        var outPath = Path.Combine(outRoot, rel);
        Assert.False(File.Exists(outPath), "Final .rpy must not exist when translation fails.");
        Assert.False(File.Exists(outPath + ".tmp"), "Temp file must be removed on failure.");
    }

    [Fact]
    public async Task TranslateFileAsync_eof_with_pending_throws_and_no_output()
    {
        var tl = Path.Combine(_tempRoot, "game", "tl");
        var lang = Path.Combine(tl, "english");
        Directory.CreateDirectory(lang);
        var origin = Path.Combine(lang, "truncated.rpy");
        await File.WriteAllTextAsync(origin, "    old \"truncated\"\n");

        var outRoot = Path.Combine(_tempRoot, "out_trunc");
        var translator = new RenpyFileTranslator(new EchoProvider());
        await Assert.ThrowsAsync<RenpyLineFillException>(() =>
            translator.TranslateFileAsync(origin, tl, outRoot, "en", "en"));

        var rel = Path.GetRelativePath(tl, origin);
        Assert.False(File.Exists(Path.Combine(outRoot, rel)));
    }

    [Fact]
    public async Task TranslateFileAsync_unquoted_line_after_old_no_eof_dialogue_throws_and_no_output()
    {
        var tl = Path.Combine(_tempRoot, "game", "tl");
        var lang = Path.Combine(tl, "english");
        Directory.CreateDirectory(lang);
        var origin = Path.Combine(lang, "nofill.rpy");
        await File.WriteAllTextAsync(origin,
            "    old \"hello\"\n    plain line without quotes\n");

        var outRoot = Path.Combine(_tempRoot, "out_nofill");
        var translator = new RenpyFileTranslator(new EchoProvider());
        await Assert.ThrowsAsync<RenpyLineFillException>(() =>
            translator.TranslateFileAsync(origin, tl, outRoot, "en", "en"));

        var rel = Path.GetRelativePath(tl, origin);
        Assert.False(File.Exists(Path.Combine(outRoot, rel)));
    }

    [Fact]
    public async Task TranslateFileAsync_voice_line_before_dialogue_merges_on_next_quoted_line()
    {
        var tl = Path.Combine(_tempRoot, "game", "tl");
        var lang = Path.Combine(tl, "english");
        Directory.CreateDirectory(lang);
        var origin = Path.Combine(lang, "voice.rpy");
        await File.WriteAllTextAsync(origin,
            "    # \"hello\"\n    voice dana_seeyoulater\n    pc \"hello\"\n");

        var outRoot = Path.Combine(_tempRoot, "out_voice");
        var translator = new RenpyFileTranslator(new EchoProvider());
        await translator.TranslateFileAsync(origin, tl, outRoot, "en", "en");

        var rel = Path.GetRelativePath(tl, origin);
        var written = (await File.ReadAllTextAsync(Path.Combine(outRoot, rel)))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("voice dana_seeyoulater", written);
        Assert.Contains("pc \"T:hello\"", written);
    }
}
