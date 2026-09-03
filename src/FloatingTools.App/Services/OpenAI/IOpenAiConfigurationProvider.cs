namespace FloatingTools.App.Services.OpenAI;

public interface IOpenAiConfigurationProvider
{
    OpenAiTranslationConfiguration? GetConfiguration();
}

public sealed record OpenAiTranslationConfiguration(
    string ApiKey,
    string Model);
