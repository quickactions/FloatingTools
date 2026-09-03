using FloatingTools.App.Models;

namespace FloatingTools.App.Services.OpenAI;

public sealed class SettingsOpenAiConfigurationProvider(
    ISecureApiKeyStore secureApiKeyStore,
    AppSettings settings,
    Func<string?>? environmentApiKey = null)
    : IOpenAiConfigurationProvider
{
    private readonly Func<string?> _environmentApiKey = environmentApiKey
        ?? (() => Environment.GetEnvironmentVariable(
            EnvironmentOpenAiConfigurationProvider.ApiKeyVariableName));

    public OpenAiTranslationConfiguration? GetConfiguration()
    {
        string? key = null;
        try
        {
            key = secureApiKeyStore.Load();
        }
        catch
        {
            // A development environment key remains a safe fallback when the
            // user-scoped credential cannot be read.
        }

        key = string.IsNullOrWhiteSpace(key) ? _environmentApiKey() : key;
        return string.IsNullOrWhiteSpace(key)
            ? null
            : new OpenAiTranslationConfiguration(
                key.Trim(),
                OpenAiModelOptions.Normalize(settings.TranslationModel));
    }
}
