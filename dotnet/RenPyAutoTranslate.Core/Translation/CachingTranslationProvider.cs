namespace RenPyAutoTranslate.Core.Translation;

public sealed class CachingTranslationProvider : ITranslationProvider
{
    private readonly ITranslationProvider _inner;
    private readonly LruTranslationCache _cache;

    public CachingTranslationProvider(ITranslationProvider inner, LruTranslationCache? cache = null)
    {
        _inner = inner;
        _cache = cache ?? new LruTranslationCache();
    }

    public async Task<string> TranslateAsync(
        string text,
        string sourceLang,
        string targetLang,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGet(sourceLang, targetLang, text, out var hit))
            return hit;
        var t = await _inner.TranslateAsync(text, sourceLang, targetLang, cancellationToken)
            .ConfigureAwait(false);
        _cache.Set(sourceLang, targetLang, text, t);
        return t;
    }
}
