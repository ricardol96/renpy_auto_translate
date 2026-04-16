namespace RenPyAutoTranslate.Core.Translation;

public interface ITranslationProvider
{
    Task<string> TranslateAsync(
        string text,
        string sourceLang,
        string targetLang,
        CancellationToken cancellationToken = default);
}
