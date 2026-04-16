namespace RenPyAutoTranslate.Core.Translation;

/// <summary>Raised when the translation provider returns an unusable response (empty, malformed, etc.).</summary>
public sealed class TranslationResponseException : InvalidOperationException
{
    public TranslationResponseException(string message) : base(message)
    {
    }
}
