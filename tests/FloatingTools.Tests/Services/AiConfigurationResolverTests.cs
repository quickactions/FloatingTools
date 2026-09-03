using FloatingTools.App.Models;
using FloatingTools.App.Services.Ai;
using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.Tests.Services;

public sealed class AiConfigurationResolverTests
{
    [Fact]
    public void Translation_InheritsApplicationCredentials()
    {
        var stores = new TestCredentialStores();

        var result = CreateResolver(stores).Resolve(AiToolId.Translation);

        Assert.Equal("OpenAI", result.Provider);
        Assert.Equal(OpenAiModelOptions.DefaultModel, result.Model);
        Assert.Equal(AiCredentialScope.Application, result.CredentialScope);
        Assert.Same(stores.Application, result.CredentialStore);
    }

    [Fact]
    public void QuickChat_InheritsApplicationCredentials()
    {
        var stores = new TestCredentialStores();

        var result = CreateResolver(stores).Resolve(AiToolId.QuickChat);

        Assert.Equal("OpenAI", result.Provider);
        Assert.Equal(AiCredentialScope.Application, result.CredentialScope);
        Assert.Same(stores.Application, result.CredentialStore);
    }

    [Fact]
    public void Translation_UsesItsOwnCredentialsWhenConfigured()
    {
        var settings = new AppSettings();
        settings.Ai.Translation.UseAppCredentials = false;
        settings.Ai.Translation.Provider = "OpenAI";
        var stores = new TestCredentialStores();

        var result = new AiConfigurationResolver(settings, stores)
            .Resolve(AiToolId.Translation);

        Assert.Equal("OpenAI", result.Provider);
        Assert.Equal(AiCredentialScope.Translation, result.CredentialScope);
        Assert.Same(stores.Translation, result.CredentialStore);
    }

    [Fact]
    public void QuickChat_UsesItsOwnCredentialsWhenConfigured()
    {
        var settings = new AppSettings();
        settings.Ai.QuickChat.UseAppCredentials = false;
        settings.Ai.QuickChat.Provider = "OpenAI";
        var stores = new TestCredentialStores();

        var result = new AiConfigurationResolver(settings, stores)
            .Resolve(AiToolId.QuickChat);

        Assert.Equal("OpenAI", result.Provider);
        Assert.Equal(AiCredentialScope.QuickChat, result.CredentialScope);
        Assert.Same(stores.QuickChat, result.CredentialStore);
    }

    [Fact]
    public void ToolSpecificModel_OverridesApplicationDefault()
    {
        var settings = new AppSettings();
        settings.Ai.DefaultModel = "application-model";
        settings.Ai.Translation.Model = "translation-model";

        var result = new AiConfigurationResolver(settings, new TestCredentialStores())
            .Resolve(AiToolId.Translation);

        Assert.Equal("translation-model", result.Model);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyToolModel_InheritsApplicationDefault(string? toolModel)
    {
        var settings = new AppSettings();
        settings.Ai.DefaultModel = "application-model";
        settings.Ai.QuickChat.Model = toolModel;

        var result = new AiConfigurationResolver(settings, new TestCredentialStores())
            .Resolve(AiToolId.QuickChat);

        Assert.Equal("application-model", result.Model);
    }

    [Fact]
    public void ChangingApplicationDefault_UpdatesOnlyToolsThatInheritTheModel()
    {
        var settings = new AppSettings();
        settings.Ai.DefaultModel = OpenAiModelOptions.LunaModel;
        settings.Ai.Translation.Model = null;
        settings.Ai.QuickChat.Model = OpenAiModelOptions.SolModel;
        var resolver = new AiConfigurationResolver(settings, new TestCredentialStores());

        settings.Ai.DefaultModel = OpenAiModelOptions.TerraModel;
        var translation = resolver.Resolve(AiToolId.Translation);
        var quickChat = resolver.Resolve(AiToolId.QuickChat);

        Assert.Equal(OpenAiModelOptions.TerraModel, translation.Model);
        Assert.Equal(OpenAiModelOptions.SolModel, quickChat.Model);
        Assert.Equal(AiCredentialScope.Application, translation.CredentialScope);
        Assert.Equal(AiCredentialScope.Application, quickChat.CredentialScope);
    }

    [Fact]
    public void Tools_CanShareApplicationCredentialsWhileUsingDifferentModels()
    {
        var settings = new AppSettings();
        settings.Ai.Translation.Model = "translation-model";
        settings.Ai.QuickChat.Model = "quick-chat-model";
        var stores = new TestCredentialStores();
        var resolver = new AiConfigurationResolver(settings, stores);

        var translation = resolver.Resolve(AiToolId.Translation);
        var quickChat = resolver.Resolve(AiToolId.QuickChat);

        Assert.Equal(AiCredentialScope.Application, translation.CredentialScope);
        Assert.Equal(AiCredentialScope.Application, quickChat.CredentialScope);
        Assert.Same(translation.CredentialStore, quickChat.CredentialStore);
        Assert.Equal("translation-model", translation.Model);
        Assert.Equal("quick-chat-model", quickChat.Model);
    }

    [Fact]
    public void ChangingQuickChatModel_DoesNotChangeTranslationResolution()
    {
        var settings = new AppSettings();
        settings.Ai.Translation.Model = "translation-model";
        var resolver = new AiConfigurationResolver(settings, new TestCredentialStores());

        var before = resolver.Resolve(AiToolId.Translation);
        settings.Ai.QuickChat.Model = "quick-chat-model";
        var after = resolver.Resolve(AiToolId.Translation);

        Assert.Equal("translation-model", before.Model);
        Assert.Equal("translation-model", after.Model);
    }

    [Fact]
    public void ChangingTranslationModel_DoesNotChangeQuickChatResolution()
    {
        var settings = new AppSettings();
        settings.Ai.QuickChat.Model = "quick-chat-model";
        var resolver = new AiConfigurationResolver(settings, new TestCredentialStores());

        var before = resolver.Resolve(AiToolId.QuickChat);
        settings.Ai.Translation.Model = "translation-model";
        var after = resolver.Resolve(AiToolId.QuickChat);

        Assert.Equal("quick-chat-model", before.Model);
        Assert.Equal("quick-chat-model", after.Model);
    }

    [Fact]
    public void DpapiStores_UseDistinctReservedPathsWithoutCreatingCredentialFiles()
    {
        var credentialDirectory = Path.Combine(
            Path.GetTempPath(),
            "FloatingTools.Tests",
            Guid.NewGuid().ToString("N"));
        var stores = new WindowsDpapiAiCredentialStoreProvider(credentialDirectory);
        var settings = new AppSettings();
        settings.Ai.QuickChat.UseAppCredentials = false;
        settings.Ai.QuickChat.Provider = "OpenAI";
        var resolver = new AiConfigurationResolver(settings, stores);

        var application = resolver.Resolve(AiToolId.Translation);
        var quickChat = resolver.Resolve(AiToolId.QuickChat);

        var applicationPath = stores.GetPath(AiCredentialScope.Application);
        var translationPath = stores.GetPath(AiCredentialScope.Translation);
        var quickChatPath = stores.GetPath(AiCredentialScope.QuickChat);
        Assert.Equal("openai-key.dat", Path.GetFileName(applicationPath));
        Assert.Equal("translation-openai-key.dat", Path.GetFileName(translationPath));
        Assert.Equal("quick-chat-openai-key.dat", Path.GetFileName(quickChatPath));
        Assert.NotEqual(applicationPath, translationPath);
        Assert.NotEqual(applicationPath, quickChatPath);
        Assert.NotEqual(translationPath, quickChatPath);
        Assert.NotSame(application.CredentialStore, quickChat.CredentialStore);
        Assert.False(File.Exists(applicationPath));
        Assert.False(File.Exists(translationPath));
        Assert.False(File.Exists(quickChatPath));
        Assert.False(Directory.Exists(credentialDirectory));
    }

    private static AiConfigurationResolver CreateResolver(TestCredentialStores stores) =>
        new(new AppSettings(), stores);

    private sealed class TestCredentialStores : IAiCredentialStoreProvider
    {
        public ISecureApiKeyStore Application { get; } = new InMemorySecureApiKeyStore();

        public ISecureApiKeyStore Translation { get; } = new InMemorySecureApiKeyStore();

        public ISecureApiKeyStore QuickChat { get; } = new InMemorySecureApiKeyStore();

        public ISecureApiKeyStore GetStore(AiCredentialScope scope) => scope switch
        {
            AiCredentialScope.Application => Application,
            AiCredentialScope.Translation => Translation,
            AiCredentialScope.QuickChat => QuickChat,
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
        };
    }
}
