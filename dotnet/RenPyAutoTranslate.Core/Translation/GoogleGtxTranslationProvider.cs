using System.Net;
using System.Text.Json;

namespace RenPyAutoTranslate.Core.Translation;

/// <summary>Google Translate via the same public gtx-style endpoint used by many free clients.</summary>
public sealed class GoogleGtxTranslationProvider : ITranslationProvider, IDisposable
{
    private readonly HttpClient _http;

    public GoogleGtxTranslationProvider(HttpClient? http = null)
    {
        _http = http ?? new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(2)
        };
        if (http is null)
            _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "RenPyAutoTranslate/1.0");
    }

    public async Task<string> TranslateAsync(
        string text,
        string sourceLang,
        string targetLang,
        CancellationToken cancellationToken = default)
    {
        if (text.Length == 0)
            return text;
        var url =
            "https://translate.googleapis.com/translate_a/single?client=gtx&dt=t" +
            $"&sl={Uri.EscapeDataString(sourceLang)}&tl={Uri.EscapeDataString(targetLang)}" +
            $"&q={Uri.EscapeDataString(text)}";
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new HttpRequestException("429 Too Many Requests", null, HttpStatusCode.TooManyRequests);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            throw new TranslationResponseException("Translation API returned an empty or invalid JSON array.");
        var first = root[0];
        if (first.ValueKind != JsonValueKind.Array)
            throw new TranslationResponseException("Translation API returned an unexpected JSON shape.");
        var sb = new System.Text.StringBuilder();
        foreach (var seg in first.EnumerateArray())
        {
            if (seg.ValueKind == JsonValueKind.Array && seg.GetArrayLength() > 0)
            {
                var part = seg[0].GetString();
                if (part is not null)
                    sb.Append(part);
            }
        }

        var result = sb.ToString();
        if (string.IsNullOrEmpty(result))
            throw new TranslationResponseException("Translation API returned empty text for non-empty input.");

        return result;
    }

    public void Dispose() => _http.Dispose();
}
