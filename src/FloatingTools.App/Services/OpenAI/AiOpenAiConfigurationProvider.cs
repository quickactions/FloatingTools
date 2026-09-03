using FloatingTools.App.Models;
using FloatingTools.App.Services.Ai;

namespace FloatingTools.App.Services.OpenAI;

public sealed class AiOpenAiConfigurationProvider : IOpenAiConfigurationProvider
{
    private readonly AiConfigurationResolver _configurationResolver;
    private readonly AiToolId _tool;
    private readonly Func<string?> _environmentApiKey;

    public AiOpenAiConfigurationProvider(
        AiConfigurationResolver configurationResolver,
        Func<string?>? environmentApiKey = null)
        : this(configurationResolver, AiToolId.Translation, environmentApiKey)
    {
    }

    public AiOpenAiConfigurationProvider(
        AiConfigurationResolver configurationResolver,
        AiToolId tool,
        Func<string?>? environmentApiKey = null)
    {
        _configurationResolver = configurationResolver
            ?? throw new ArgumentNullException(nameof(configurationResolver));
        _tool = tool;
        _environmentApiKey = environmentApiKey
            ?? (() => Environment.GetEnvironmentVariable(
                EnvironmentOpenAiConfigurationProvider.ApiKeyVariableName));
    }

    public OpenAiTranslationConfiguration? GetConfiguration()
    {
        var configuration = _configurationResolver.Resolve(_tool);
        if (!string.Equals(
                configuration.Provider,
                OpenAiTranslationService.ProviderName,
                StringComparison.Ordinal))
        {
            return null;
        }

        string? key = null;
        try
        {
            key = configuration.CredentialStore.Load();
        }
        catch
        {
            // A development environment key remains a safe fallback when the
            // user-scoped credential cannot be read.
        }

        key = string.IsNullOrWhiteSpace(key) ? _environmentApiKey() : key;
        return string.IsNullOrWhiteSpace(key)
            ? null
            : new OpenAiTranslationConfiguration(key.Trim(), configuration.Model);
    }
}
