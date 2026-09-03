using System.Net;
using System.Net.Http;
using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.Tests.Services;

public sealed class OpenAiConnectionTesterTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK, true, "Connection successful.")]
    [InlineData(HttpStatusCode.Unauthorized, false, "Invalid API key.")]
    [InlineData(HttpStatusCode.TooManyRequests, false, "API quota unavailable.")]
    public async Task Status_IsMappedToFriendlyResult(
        HttpStatusCode status,
        bool success,
        string message)
    {
        var client = new HttpClient(new Handler((_, _) =>
            Task.FromResult(new HttpResponseMessage(status))))
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
        var tester = new OpenAiConnectionTester(
            client,
            new ConfigurationProvider(
                new OpenAiTranslationConfiguration("secret", "model")));

        var result = await tester.TestAsync(CancellationToken.None);

        Assert.Equal(success, result.IsSuccess);
        Assert.Equal(message, result.Message);
    }

    [Fact]
    public async Task TestUsesModelEndpointAndNeverExposesKeyInResult()
    {
        HttpRequestMessage? captured = null;
        var client = new HttpClient(new Handler((request, _) =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        })) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var tester = new OpenAiConnectionTester(
            client,
            new ConfigurationProvider(
                new OpenAiTranslationConfiguration("private-key", "gpt-test")));

        var result = await tester.TestAsync(CancellationToken.None);

        Assert.Equal(
            "https://api.openai.com/v1/models/gpt-test",
            captured!.RequestUri!.OriginalString);
        Assert.Equal("private-key", captured.Headers.Authorization!.Parameter);
        Assert.DoesNotContain("private-key", result.Message);
    }

    private sealed class ConfigurationProvider(OpenAiTranslationConfiguration? value)
        : IOpenAiConfigurationProvider
    {
        public OpenAiTranslationConfiguration? GetConfiguration() => value;
    }

    private sealed class Handler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
