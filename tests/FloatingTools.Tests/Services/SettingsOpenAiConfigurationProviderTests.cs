using FloatingTools.App.Models;
using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.Tests.Services;

public sealed class SettingsOpenAiConfigurationProviderTests
{
    [Fact]
    public void SecureKey_TakesPrecedenceOverEnvironmentAndUsesLiveModel()
    {
        var settings = new AppSettings { TranslationModel = OpenAiModelOptions.DefaultModel };
        var secureStore = new InMemorySecureApiKeyStore("secure-key");
        var provider = new SettingsOpenAiConfigurationProvider(
            secureStore, settings, () => "environment-key");

        var first = provider.GetConfiguration();
        settings.TranslationModel = OpenAiModelOptions.FastModel;
        var second = provider.GetConfiguration();

        Assert.Equal("secure-key", first!.ApiKey);
        Assert.Equal(OpenAiModelOptions.DefaultModel, first.Model);
        Assert.Equal(OpenAiModelOptions.FastModel, second!.Model);
    }

    [Fact]
    public void EnvironmentKey_IsUsedOnlyWhenSecureKeyIsAbsent()
    {
        var provider = new SettingsOpenAiConfigurationProvider(
            new InMemorySecureApiKeyStore(), new AppSettings(), () => "env-key");

        Assert.Equal("env-key", provider.GetConfiguration()!.ApiKey);
    }

    [Fact]
    public void NoKey_ReturnsUnconfigured()
    {
        var provider = new SettingsOpenAiConfigurationProvider(
            new InMemorySecureApiKeyStore(), new AppSettings(), () => null);

        Assert.Null(provider.GetConfiguration());
    }

    [Fact]
    public void ReplaceAndRemove_AffectFutureConfigurationReads()
    {
        var store = new InMemorySecureApiKeyStore("first");
        var provider = new SettingsOpenAiConfigurationProvider(
            store, new AppSettings(), () => null);

        store.Save("second");
        Assert.Equal("second", provider.GetConfiguration()!.ApiKey);
        store.Remove();
        Assert.Null(provider.GetConfiguration());
    }

    [Fact]
    public void LegacyProvider_ContinuesUsingTranslationModelRatherThanNewAiSettings()
    {
        var settings = new AppSettings
        {
            TranslationModel = OpenAiModelOptions.FastModel,
            Ai = new AiSettings
            {
                Translation = new AiToolSettings { Model = "future-translation-model" }
            }
        };
        var provider = new SettingsOpenAiConfigurationProvider(
            new InMemorySecureApiKeyStore("secure-key"), settings, () => null);

        var configuration = provider.GetConfiguration();

        Assert.Equal(OpenAiModelOptions.FastModel, configuration!.Model);
    }
}
