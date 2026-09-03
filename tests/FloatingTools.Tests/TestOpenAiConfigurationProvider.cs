using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.Tests;

internal sealed class TestOpenAiConfigurationProvider(
    OpenAiTranslationConfiguration? configuration = null)
    : IOpenAiConfigurationProvider
{
    public OpenAiTranslationConfiguration? GetConfiguration() => configuration;
}
