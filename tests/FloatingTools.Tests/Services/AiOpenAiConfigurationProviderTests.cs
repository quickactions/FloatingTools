using FloatingTools.App.Models;
using FloatingTools.App.Services.Ai;
using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.Tests.Services;

public sealed class AiOpenAiConfigurationProviderTests : IDisposable
{
    private readonly string _credentialDirectory = Path.Combine(
        Path.GetTempPath(),
        "FloatingTools.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Translation_UsesApplicationCredentialAndPinnedModelWithoutCreatingCustomCredential()
    {
        var settings = new AppSettings
        {
            TranslationModel = OpenAiModelOptions.FastModel,
            Ai = new AiSettings
            {
                DefaultModel = "future-application-model",
                Translation = new AiToolSettings
                {
                    UseAppCredentials = true,
                    Model = OpenAiModelOptions.FastModel
                }
            }
        };
        var stores = new WindowsDpapiAiCredentialStoreProvider(_credentialDirectory);
        stores.GetStore(AiCredentialScope.Application).Save("application-key");
        var provider = new AiOpenAiConfigurationProvider(
            new AiConfigurationResolver(settings, stores), () => null);

        var configuration = provider.GetConfiguration();

        Assert.NotNull(configuration);
        Assert.Equal("application-key", configuration.ApiKey);
        Assert.Equal(OpenAiModelOptions.FastModel, configuration.Model);
        Assert.True(File.Exists(stores.GetPath(AiCredentialScope.Application)));
        Assert.False(File.Exists(stores.GetPath(AiCredentialScope.Translation)));
    }

    [Fact]
    public void PinnedTranslationModel_DoesNotChangeWhenApplicationDefaultChanges()
    {
        var settings = new AppSettings
        {
            Ai = new AiSettings
            {
                DefaultModel = "application-model-a",
                Translation = new AiToolSettings { Model = "translation-pinned-model" }
            }
        };
        var stores = new TestCredentialStores("application-key");
        var provider = new AiOpenAiConfigurationProvider(
            new AiConfigurationResolver(settings, stores), () => null);

        var first = provider.GetConfiguration();
        settings.Ai.DefaultModel = "application-model-b";
        var second = provider.GetConfiguration();

        Assert.Equal("translation-pinned-model", first!.Model);
        Assert.Equal("translation-pinned-model", second!.Model);
    }

    [Fact]
    public void EnvironmentKey_RemainsFallbackWhenApplicationCredentialIsUnavailable()
    {
        var provider = new AiOpenAiConfigurationProvider(
            new AiConfigurationResolver(new AppSettings(), new TestCredentialStores()),
            () => "environment-key");

        var configuration = provider.GetConfiguration();

        Assert.Equal("environment-key", configuration!.ApiKey);
        Assert.Equal(OpenAiModelOptions.DefaultModel, configuration.Model);
    }

    [Fact]
    public void QuickChat_ExplicitToolResolutionInheritsApplicationCredentialAndDefaultModel()
    {
        var settings = new AppSettings();
        settings.Ai.DefaultModel = "application-default-model";
        settings.Ai.Translation.Model = "translation-only-model";
        var provider = new AiOpenAiConfigurationProvider(
            new AiConfigurationResolver(
                settings,
                new TestCredentialStores(applicationKey: "application-key")),
            AiToolId.QuickChat,
            () => null);

        var configuration = provider.GetConfiguration();

        Assert.Equal("application-key", configuration!.ApiKey);
        Assert.Equal("application-default-model", configuration.Model);
    }

    [Fact]
    public void QuickChat_CustomCredentialAndModelRemainIndependentFromTranslation()
    {
        var settings = new AppSettings();
        settings.Ai.Translation.UseAppCredentials = false;
        settings.Ai.Translation.Provider = "OpenAI";
        settings.Ai.Translation.Model = "translation-model";
        settings.Ai.QuickChat.UseAppCredentials = false;
        settings.Ai.QuickChat.Provider = "OpenAI";
        settings.Ai.QuickChat.Model = "quick-chat-model";
        var stores = new TestCredentialStores(
            translationKey: "translation-key",
            quickChatKey: "quick-chat-key");
        var resolver = new AiConfigurationResolver(settings, stores);
        var translationProvider = new AiOpenAiConfigurationProvider(
            resolver,
            AiToolId.Translation,
            () => null);
        var quickChatProvider = new AiOpenAiConfigurationProvider(
            resolver,
            AiToolId.QuickChat,
            () => null);

        var translation = translationProvider.GetConfiguration();
        var quickChat = quickChatProvider.GetConfiguration();

        Assert.Equal("translation-key", translation!.ApiKey);
        Assert.Equal("translation-model", translation.Model);
        Assert.Equal("quick-chat-key", quickChat!.ApiKey);
        Assert.Equal("quick-chat-model", quickChat.Model);
    }

    public void Dispose()
    {
        if (Directory.Exists(_credentialDirectory))
        {
            Directory.Delete(_credentialDirectory, recursive: true);
        }
    }

    private sealed class TestCredentialStores(
        string? applicationKey = null,
        string? translationKey = null,
        string? quickChatKey = null)
        : IAiCredentialStoreProvider
    {
        private readonly ISecureApiKeyStore _application =
            new InMemorySecureApiKeyStore(applicationKey);

        private readonly ISecureApiKeyStore _translation =
            new InMemorySecureApiKeyStore(translationKey);

        private readonly ISecureApiKeyStore _quickChat =
            new InMemorySecureApiKeyStore(quickChatKey);

        public ISecureApiKeyStore GetStore(AiCredentialScope scope) => scope switch
        {
            AiCredentialScope.Application => _application,
            AiCredentialScope.Translation => _translation,
            AiCredentialScope.QuickChat => _quickChat,
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
        };
    }
}
