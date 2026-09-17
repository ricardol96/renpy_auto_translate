using System.Net;
using RenPyAutoTranslate.Core.Translation;

namespace RenPyAutoTranslate.Core.Tests;

public sealed class GoogleGtxTranslationProviderTests
{
    [Fact]
    public async Task TranslateAsync_whitespace_only_returns_input_without_http_request()
    {
        var handler = new CountingHandler();
        using var http = new HttpClient(handler);
        using var provider = new GoogleGtxTranslationProvider(http);

        var result = await provider.TranslateAsync(" ", "en", "pt");

        Assert.Equal(" ", result);
        Assert.Equal(0, handler.RequestCount);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
