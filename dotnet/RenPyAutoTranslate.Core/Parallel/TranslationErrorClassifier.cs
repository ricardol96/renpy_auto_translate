using System.Net;
using System.Net.Http;
using RenPyAutoTranslate.Core.Translation;

namespace RenPyAutoTranslate.Core.Parallel;

/// <summary>Matches Python classify logic in translate_rpy_file_with_retry.</summary>
public static class TranslationErrorClassifier
{
    public static bool IsRateLimitError(Exception exc)
    {
        if (exc is HttpRequestException h && h.StatusCode == HttpStatusCode.TooManyRequests)
            return true;
        var msg = exc.Message.ToLowerInvariant();
        return msg.Contains("429")
               || msg.Contains("too many")
               || msg.Contains("rate limit")
               || msg.Contains("quota");
    }

    public static bool IsRetriableException(Exception exc)
    {
        if (exc is HttpRequestException or TaskCanceledException or HttpIOException)
            return true;
        if (exc is IOException)
            return true;
        if (exc is TranslationResponseException)
            return true;
        var msg = exc.Message.ToLowerInvariant();
        return msg.Contains("429")
               || msg.Contains("too many")
               || msg.Contains("timeout")
               || msg.Contains("connection");
    }
}
