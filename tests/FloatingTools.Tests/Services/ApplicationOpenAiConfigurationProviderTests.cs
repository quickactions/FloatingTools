using FloatingTools.App.Models;
using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.Tests.Services;

public sealed class ApplicationOpenAiConfigurationProviderTests
{
    [Fact]
    public void StoredApplicationCredential_UsesCurrentApplicationDefaultModel()
    {
        var settings = new AppSettings();
        settings.Ai.DefaultModel = OpenAiModelOptions.SolModel;
        var provider = new ApplicationOpenAiConfigurationProvider(
            settings,
            new InMemorySecureApiKeyStore("application-key"),
            () => "environment-key");

        var configuration = provider.GetConfiguration();

        Assert.Equal("application-key", configuration!.ApiKey);
        Assert.Equal(OpenAiModelOptions.SolModel, configuration.Model);
    }

    [Fact]
    public void EnvironmentCredential_RemainsApplicationFallback()
    {
        var provider = new ApplicationOpenAiConfigurationProvider(
            new AppSettings(),
            new InMemorySecureApiKeyStore(),
            () => "environment-key");

        var configuration = provider.GetConfiguration();

        Assert.Equal("environment-key", configuration!.ApiKey);
        Assert.Equal(OpenAiModelOptions.LunaModel, configuration.Model);
    }

    [Fact]
    public void UnsupportedApplicationProvider_IsNotTreatedAsOpenAiConfiguration()
    {
        var settings = new AppSettings();
        settings.Ai.DefaultProvider = "FutureProvider";
        var provider = new ApplicationOpenAiConfigurationProvider(
            settings,
            new InMemorySecureApiKeyStore("application-key"),
            () => null);

        Assert.Null(provider.GetConfiguration());
    }
}
