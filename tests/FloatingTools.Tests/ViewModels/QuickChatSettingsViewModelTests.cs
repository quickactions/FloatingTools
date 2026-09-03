using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.Services.OpenAI;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.ViewModels;

public sealed class QuickChatSettingsViewModelTests
{
    [Fact]
    public void ModelOptions_ExposeAppDefaultAndSupportedModels()
    {
        var viewModel = Create(out _, out _);

        Assert.Equal(
            [null, OpenAiModelOptions.SolModel, OpenAiModelOptions.TerraModel, OpenAiModelOptions.LunaModel],
            viewModel.ModelOptions.Select(item => item.Model));
        Assert.Equal(
            ["App default", "GPT-5.6 Sol", "GPT-5.6 Terra", "GPT-5.6 Luna"],
            viewModel.ModelOptions.Select(item => item.DisplayName));
    }

    [Fact]
    public void AppDefaultAndConcreteModels_PersistQuickChatOnly()
    {
        var viewModel = Create(out var settings, out var store);
        settings.Ai.Translation.Model = OpenAiModelOptions.SolModel;

        viewModel.SelectedModel = OpenAiModelOptions.TerraModel;
        Assert.Equal(OpenAiModelOptions.TerraModel, settings.Ai.QuickChat.Model);
        Assert.Equal(OpenAiModelOptions.SolModel, settings.Ai.Translation.Model);

        viewModel.SelectedModel = null;
        Assert.Null(settings.Ai.QuickChat.Model);
        Assert.Equal(2, store.SaveCount);
    }

    [Fact]
    public void CredentialInheritance_ChangesQuickChatOnly()
    {
        var viewModel = Create(out var settings, out _);
        settings.Ai.Translation.UseAppCredentials = true;

        viewModel.UseAppCredentials = false;

        Assert.False(settings.Ai.QuickChat.UseAppCredentials);
        Assert.Equal("OpenAI", settings.Ai.QuickChat.Provider);
        Assert.True(settings.Ai.Translation.UseAppCredentials);
    }

    [Fact]
    public void CustomKey_UsesOnlyInjectedQuickChatCredentialStore()
    {
        var quickChatKey = new InMemorySecureApiKeyStore();
        var viewModel = Create(out _, out _, quickChatKey);
        viewModel.UseAppCredentials = false;

        Assert.True(viewModel.SaveApiKey("qc-secret"));
        Assert.Equal("qc-secret", quickChatKey.Load());
        Assert.True(viewModel.HasStoredApiKey);
    }

    [Fact]
    public async Task TestConnection_UsesInjectedQuickChatTester()
    {
        var tester = new RecordingConnectionTester();
        var viewModel = Create(out _, out _, connectionTester: tester);

        await viewModel.TestConnectionCommand.ExecuteAsync(null);

        Assert.Equal(1, tester.CallCount);
        Assert.Equal("Quick Chat connection okay.", viewModel.ConnectionStatusMessage);
    }

    private static QuickChatSettingsViewModel Create(
        out AppSettings settings,
        out RecordingSettingsStore settingsStore,
        ISecureApiKeyStore? credentialStore = null,
        IOpenAiConnectionTester? connectionTester = null)
    {
        settings = new AppSettings();
        settingsStore = new RecordingSettingsStore();
        return new QuickChatSettingsViewModel(
            settings,
            settingsStore,
            credentialStore ?? new InMemorySecureApiKeyStore(),
            new StaticConfigurationProvider(),
            connectionTester ?? new RecordingConnectionTester());
    }

    private sealed class RecordingSettingsStore : IAppSettingsStore
    {
        public int SaveCount { get; private set; }

        public AppSettings Load() => new();

        public bool Save(AppSettings settings)
        {
            SaveCount++;
            return true;
        }
    }

    private sealed class StaticConfigurationProvider : IOpenAiConfigurationProvider
    {
        public OpenAiTranslationConfiguration? GetConfiguration() =>
            new("key", OpenAiModelOptions.DefaultModel);
    }

    private sealed class RecordingConnectionTester : IOpenAiConnectionTester
    {
        public int CallCount { get; private set; }

        public Task<ConnectionTestResult> TestAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new ConnectionTestResult(
                true,
                "Quick Chat connection okay."));
        }
    }
}
