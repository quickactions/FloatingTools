using FloatingTools.App.Models;
using FloatingTools.App.Services.Ai;
using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.Tests.Services;

public sealed class WindowsDpapiAiCredentialStoreProviderTests : IDisposable
{
    private readonly string _credentialDirectory = Path.Combine(
        Path.GetTempPath(),
        "FloatingTools.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Scopes_ResolveToDistinctStoresAndApprovedPaths()
    {
        var provider = new WindowsDpapiAiCredentialStoreProvider(_credentialDirectory);

        var application = provider.GetStore(AiCredentialScope.Application);
        var translation = provider.GetStore(AiCredentialScope.Translation);
        var quickChat = provider.GetStore(AiCredentialScope.QuickChat);

        Assert.IsType<WindowsDpapiApiKeyStore>(application);
        Assert.IsType<WindowsDpapiApiKeyStore>(translation);
        Assert.IsType<WindowsDpapiApiKeyStore>(quickChat);
        Assert.NotSame(application, translation);
        Assert.NotSame(application, quickChat);
        Assert.NotSame(translation, quickChat);
        Assert.Equal("openai-key.dat", Path.GetFileName(
            provider.GetPath(AiCredentialScope.Application)));
        Assert.Equal("translation-openai-key.dat", Path.GetFileName(
            provider.GetPath(AiCredentialScope.Translation)));
        Assert.Equal("quick-chat-openai-key.dat", Path.GetFileName(
            provider.GetPath(AiCredentialScope.QuickChat)));
    }

    [Fact]
    public void ApplicationComposition_CreatesStoresAndResolverWithoutCreatingCredentialFiles()
    {
        var provider = new WindowsDpapiAiCredentialStoreProvider(_credentialDirectory);
        var resolver = new AiConfigurationResolver(new AppSettings(), provider);

        resolver.Resolve(AiToolId.Translation);
        resolver.Resolve(AiToolId.QuickChat);

        Assert.False(Directory.Exists(_credentialDirectory));
        Assert.False(File.Exists(provider.GetPath(AiCredentialScope.Application)));
        Assert.False(File.Exists(provider.GetPath(AiCredentialScope.Translation)));
        Assert.False(File.Exists(provider.GetPath(AiCredentialScope.QuickChat)));
    }

    [Fact]
    public void SavingTranslationCustomCredential_DoesNotAffectQuickChatOrApplicationCredentials()
    {
        var provider = new WindowsDpapiAiCredentialStoreProvider(_credentialDirectory);
        var application = provider.GetStore(AiCredentialScope.Application);
        var translation = provider.GetStore(AiCredentialScope.Translation);
        var quickChat = provider.GetStore(AiCredentialScope.QuickChat);

        translation.Save("translation-custom-key");

        Assert.Equal("translation-custom-key", translation.Load());
        Assert.False(application.HasKey);
        Assert.False(quickChat.HasKey);
        Assert.Null(application.Load());
        Assert.Null(quickChat.Load());
        Assert.True(File.Exists(provider.GetPath(AiCredentialScope.Translation)));
        Assert.False(File.Exists(provider.GetPath(AiCredentialScope.Application)));
        Assert.False(File.Exists(provider.GetPath(AiCredentialScope.QuickChat)));
    }

    [Fact]
    public void ApplicationScope_PreservesExistingStoreBehavior()
    {
        var provider = new WindowsDpapiAiCredentialStoreProvider(_credentialDirectory);
        var application = provider.GetStore(AiCredentialScope.Application);

        application.Save("application-key");

        Assert.True(application.HasKey);
        Assert.Equal("application-key", application.Load());
        Assert.True(File.Exists(provider.GetPath(AiCredentialScope.Application)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_credentialDirectory))
        {
            Directory.Delete(_credentialDirectory, recursive: true);
        }
    }
}
