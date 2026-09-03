using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class UnconfiguredTranslationServiceTests
{
    [Fact]
    public async Task TranslateAsync_ThrowsExpectedConfigurationError()
    {
        var service = new UnconfiguredTranslationService();

        var exception = await Assert.ThrowsAsync<TranslationProviderNotConfiguredException>(
            () => service.TranslateAsync(
                "Hello",
                sourceLanguage: "en",
                targetLanguage: "he",
                CancellationToken.None));

        Assert.Equal(
            "OpenAI API key is missing. Set OPENAI_API_KEY and restart FloatingTools.",
            exception.Message);
    }
}
