using FloatingTools.App.Models;

namespace FloatingTools.App.Services.OpenAI;

public sealed class ApplicationOpenAiConfigurationProvider(
    AppSettings settings,
    ISecureApiKeyStore credentialStore,
    Func<string?>? environmentApiKey = null)
    : IOpenAiConfigurationProvider
{
    private readonly Func<string?> _environmentApiKey = environmentApiKey
        ?? (() => Environment.GetEnvironmentVariable(
            EnvironmentOpenAiConfigurationProvider.ApiKeyVariableName));

    public OpenAiTranslationConfiguration? GetConfiguration()
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(credentialStore);

        if (!string.Equals(
                settings.Ai.DefaultProvider,
                OpenAiTranslationService.ProviderName,
                StringComparison.Ordinal))
        {
            return null;
        }

        string? key = null;
        try
        {
            key = credentialStore.Load();
        }
        catch
        {
            // A development environment key remains a safe fallback when the
            // user-scoped application credential cannot be read.
        }

        key = string.IsNullOrWhiteSpace(key) ? _environmentApiKey() : key;
        return string.IsNullOrWhiteSpace(key)
            ? null
            : new OpenAiTranslationConfiguration(
                key.Trim(),
                settings.Ai.DefaultModel);
    }
}
