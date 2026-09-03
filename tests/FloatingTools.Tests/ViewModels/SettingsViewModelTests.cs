using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.Services.Ai;
using FloatingTools.App.Services.OpenAI;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.ViewModels;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void ApplicationAi_DefaultsToLunaAndOffersOnlyConcreteSupportedModels()
    {
        var viewModel = Create();

        Assert.Equal(OpenAiModelOptions.LunaModel, viewModel.SelectedApplicationDefaultModel);
        Assert.Equal(
            OpenAiModelOptions.Supported,
            viewModel.ApplicationDefaultModelOptions.Select(option => option.Model));
        Assert.All(
            viewModel.ApplicationDefaultModelOptions,
            option => Assert.True(option.IsSelectable));
        Assert.DoesNotContain(
            viewModel.ApplicationDefaultModelOptions,
            option => option.Model is null);
    }

    [Theory]
    [InlineData(OpenAiModelOptions.SolModel)]
    [InlineData(OpenAiModelOptions.TerraModel)]
    [InlineData(OpenAiModelOptions.LunaModel)]
    public void ApplicationDefaultModel_PersistsWithoutChangingToolOverrides(
        string model)
    {
        var settings = new AppSettings();
        settings.Ai.Translation.Model = OpenAiModelOptions.TerraModel;
        settings.Ai.QuickChat.Model = OpenAiModelOptions.SolModel;
        var viewModel = Create(settings);

        viewModel.SelectedApplicationDefaultModel = model;

        Assert.Equal(model, settings.Ai.DefaultModel);
        Assert.Equal(OpenAiModelOptions.TerraModel, settings.Ai.Translation.Model);
        Assert.Equal(OpenAiModelOptions.SolModel, settings.Ai.QuickChat.Model);
    }

    [Fact]
    public void ApplicationLegacyNano_RemainsVisibleOnlyAsDisabledCurrentValue()
    {
        var settings = new AppSettings();
        settings.Ai.DefaultModel = OpenAiModelOptions.LegacyNanoModel;
        var viewModel = Create(settings);
        var legacy = Assert.Single(
            viewModel.ApplicationDefaultModelOptions,
            option => option.Model == OpenAiModelOptions.LegacyNanoModel);

        Assert.Equal(OpenAiModelOptions.LegacyNanoModel,
            viewModel.SelectedApplicationDefaultModel);
        Assert.False(legacy.IsSelectable);
        Assert.Equal("GPT-5.4 Nano (legacy)", legacy.DisplayName);
    }

    [Fact]
    public void SavingKey_ClearsExposureAndShowsOnlyMask()
    {
        var translationStore = new InMemorySecureApiKeyStore();
        var viewModel = Create(translationStore: translationStore);
        viewModel.UseAppCredentials = false;
        viewModel.BeginApiKeyEditCommand.Execute(null);

        Assert.True(viewModel.SaveApiKey("sk-private"));

        Assert.Equal("sk-private", translationStore.Load());
        Assert.Equal("••••••••••••••", viewModel.ApiKeyStatus);
        Assert.DoesNotContain("sk-private", viewModel.ApiKeyStatus);
        Assert.False(viewModel.IsApiKeyEditing);
        Assert.True(viewModel.IsApiKeyDisplayMode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApiKeyInput_IsHiddenUntilAddOrReplaceStarts(bool configured)
    {
        var translationStore = new InMemorySecureApiKeyStore(
            configured ? "secret" : null);
        var viewModel = Create(translationStore: translationStore);
        viewModel.UseAppCredentials = false;

        Assert.False(viewModel.IsApiKeyEditing);
        Assert.True(viewModel.IsApiKeyDisplayMode);

        viewModel.BeginApiKeyEditCommand.Execute(null);

        Assert.True(viewModel.IsApiKeyEditing);
        Assert.False(viewModel.IsApiKeyDisplayMode);
    }

    [Fact]
    public void CancelEdit_HidesInputWithoutChangingStoredKey()
    {
        var translationStore = new InMemorySecureApiKeyStore("original-secret");
        var viewModel = Create(translationStore: translationStore);
        viewModel.UseAppCredentials = false;
        viewModel.BeginApiKeyEditCommand.Execute(null);

        viewModel.CancelApiKeyEditCommand.Execute(null);

        Assert.False(viewModel.IsApiKeyEditing);
        Assert.Equal("original-secret", translationStore.Load());
        Assert.Equal("••••••••••••••", viewModel.ApiKeyStatus);
    }

    [Fact]
    public void RemoveCustomKey_DoesNotTouchApplicationCredential()
    {
        var settings = new AppSettings();
        var applicationStore = new InMemorySecureApiKeyStore("application-secret");
        var translationStore = new InMemorySecureApiKeyStore("translation-secret");
        var viewModel = Create(
            settings,
            applicationStore,
            translationStore: translationStore);
        viewModel.UseAppCredentials = false;

        viewModel.RemoveApiKeyCommand.Execute(null);

        Assert.Equal("application-secret", applicationStore.Load());
        Assert.Null(translationStore.Load());
    }

    [Fact]
    public void TranslationModelPersistsAndChangesLegacyProviderConfiguration()
    {
        var settings = new AppSettings();
        var settingsStore = new RecordingSettingsStore(settings);
        var keyStore = new InMemorySecureApiKeyStore("key");
        var provider = new SettingsOpenAiConfigurationProvider(
            keyStore, settings, () => null);
        var viewModel = Create(settings, keyStore, provider, settingsStore);

        viewModel.SelectedTranslationModel = OpenAiModelOptions.FastModel;
        Assert.Equal(OpenAiModelOptions.FastModel, provider.GetConfiguration()!.Model);
        Assert.Equal(OpenAiModelOptions.FastModel, settings.TranslationModel);
        Assert.Equal(OpenAiModelOptions.FastModel, settings.Ai.Translation.Model);
        Assert.Equal(1, settingsStore.SaveCount);
    }

    [Fact]
    public void MigratedTranslationSettings_ShowApplicationCredentialsAndPreviousConcreteModel()
    {
        var settings = new AppSettings
        {
            TranslationModel = OpenAiModelOptions.FastModel,
            Ai = new AiSettings
            {
                Translation = new AiToolSettings
                {
                    UseAppCredentials = true,
                    Model = OpenAiModelOptions.FastModel,
                    IsModelSelectionInitialized = true
                }
            }
        };

        var viewModel = Create(settings);

        Assert.True(viewModel.UseAppCredentials);
        Assert.False(viewModel.IsUsingCustomCredentials);
        Assert.Equal(OpenAiModelOptions.FastModel, viewModel.SelectedTranslationAiModel);
    }

    [Fact]
    public void SelectingAppDefaultModel_PersistsNullWithoutOverwritingLegacyCompatibilityModel()
    {
        var settings = new AppSettings
        {
            TranslationModel = OpenAiModelOptions.FastModel,
            Ai = new AiSettings
            {
                Translation = new AiToolSettings
                {
                    Model = OpenAiModelOptions.FastModel,
                    IsModelSelectionInitialized = true
                }
            }
        };
        var viewModel = Create(settings);

        viewModel.SelectedTranslationAiModel = null;

        Assert.Null(settings.Ai.Translation.Model);
        Assert.True(settings.Ai.Translation.IsModelSelectionInitialized);
        Assert.Equal(OpenAiModelOptions.FastModel, settings.TranslationModel);
    }

    [Fact]
    public void SelectingConcreteTranslationModel_PinsAndSynchronizesCompatibilityModel()
    {
        var settings = new AppSettings();
        var viewModel = Create(settings);

        viewModel.SelectedTranslationAiModel = OpenAiModelOptions.FastModel;

        Assert.Equal(OpenAiModelOptions.FastModel, settings.Ai.Translation.Model);
        Assert.Equal(OpenAiModelOptions.FastModel, settings.TranslationModel);
        Assert.True(settings.Ai.Translation.IsModelSelectionInitialized);
    }

    [Fact]
    public void TranslationModelOptions_ExposeHumanReadableDisplayNamesWithoutChangingModelValues()
    {
        var viewModel = Create();

        Assert.Collection(
            viewModel.TranslationModelOptions,
            option =>
            {
                Assert.Null(option.Model);
                Assert.Equal("App default", option.DisplayName);
            },
            option =>
            {
                Assert.Equal(OpenAiModelOptions.SolModel, option.Model);
                Assert.Equal("GPT-5.6 Sol", option.DisplayName);
            },
            option =>
            {
                Assert.Equal(OpenAiModelOptions.TerraModel, option.Model);
                Assert.Equal("GPT-5.6 Terra", option.DisplayName);
            },
            option =>
            {
                Assert.Equal(OpenAiModelOptions.LunaModel, option.Model);
                Assert.Equal("GPT-5.6 Luna", option.DisplayName);
            });
    }

    [Fact]
    public void PersistedNano_IsShownOnlyAsDisabledLegacySelection()
    {
        var settings = new AppSettings
        {
            Ai = new AiSettings
            {
                Translation = new AiToolSettings
                {
                    Model = OpenAiModelOptions.LegacyNanoModel,
                    IsModelSelectionInitialized = true
                }
            }
        };

        var viewModel = Create(settings);
        var legacy = Assert.Single(
            viewModel.TranslationModelOptions,
            option => option.Model == OpenAiModelOptions.LegacyNanoModel);

        Assert.Equal("GPT-5.4 Nano (legacy)", legacy.DisplayName);
        Assert.False(legacy.IsSelectable);
        Assert.Equal(
            OpenAiModelOptions.LegacyNanoModel,
            viewModel.SelectedTranslationAiModel);
        Assert.Equal(
            OpenAiModelOptions.LegacyNanoModel,
            settings.Ai.Translation.Model);
    }

    [Fact]
    public void SelectingTranslationModel_DoesNotChangeQuickChatModel()
    {
        var settings = new AppSettings
        {
            Ai = new AiSettings
            {
                QuickChat = new AiToolSettings
                {
                    Model = OpenAiModelOptions.SolModel
                }
            }
        };
        var viewModel = Create(settings);

        viewModel.SelectedTranslationAiModel = OpenAiModelOptions.TerraModel;

        Assert.Equal(OpenAiModelOptions.TerraModel, settings.Ai.Translation.Model);
        Assert.Equal(OpenAiModelOptions.SolModel, settings.Ai.QuickChat.Model);
    }

    [Fact]
    public void LanguageModeOptions_ExposeHumanReadableDisplayNamesWithoutChangingValues()
    {
        var viewModel = Create();

        Assert.Collection(
            viewModel.LanguageModes,
            option =>
            {
                Assert.Equal(TranslationLanguageMode.Automatic, option.Value);
                Assert.Equal("Automatic Hebrew ↔ English", option.DisplayName);
            },
            option =>
            {
                Assert.Equal(TranslationLanguageMode.HebrewToEnglish, option.Value);
                Assert.Equal("Hebrew → English", option.DisplayName);
            },
            option =>
            {
                Assert.Equal(TranslationLanguageMode.EnglishToHebrew, option.Value);
                Assert.Equal("English → Hebrew", option.DisplayName);
            });
    }

    [Fact]
    public void SelectingLanguageMode_PersistsTheEnumValueUnchanged()
    {
        var settings = new AppSettings();
        var viewModel = Create(settings);

        viewModel.SelectedLanguageMode = TranslationLanguageMode.EnglishToHebrew;

        Assert.Equal(TranslationLanguageMode.EnglishToHebrew, settings.LanguageMode);
        Assert.Equal(TranslationLanguageMode.EnglishToHebrew, viewModel.SelectedLanguageMode);
    }

    [Fact]
    public void AppCredentials_HideCustomCredentialEditingState()
    {
        var viewModel = Create();

        viewModel.BeginApiKeyEditCommand.Execute(null);

        Assert.True(viewModel.UseAppCredentials);
        Assert.False(viewModel.IsUsingCustomCredentials);
        Assert.False(viewModel.IsApiKeyEditing);
        Assert.Equal("Using app credentials", viewModel.ApiKeyStatus);
    }

    [Fact]
    public void CustomCredentials_ExposeTranslationSpecificCredentialState()
    {
        var translationStore = new InMemorySecureApiKeyStore();
        var viewModel = Create(translationStore: translationStore);

        viewModel.UseAppCredentials = false;
        viewModel.BeginApiKeyEditCommand.Execute(null);

        Assert.False(viewModel.UseAppCredentials);
        Assert.True(viewModel.IsUsingCustomCredentials);
        Assert.Equal(OpenAiTranslationService.ProviderName, viewModel.ProviderDisplayName);
        Assert.True(viewModel.IsApiKeyEditing);
    }

    [Fact]
    public void SavingCustomKey_WritesOnlyTheTranslationStore()
    {
        var applicationStore = new InMemorySecureApiKeyStore();
        var translationStore = new InMemorySecureApiKeyStore();
        var viewModel = Create(
            store: applicationStore,
            translationStore: translationStore);
        viewModel.UseAppCredentials = false;

        Assert.True(viewModel.SaveApiKey("translation-key"));

        Assert.Null(applicationStore.Load());
        Assert.Equal("translation-key", translationStore.Load());
    }

    [Fact]
    public void SavingApplicationKey_WritesOnlyApplicationStore()
    {
        var applicationStore = new InMemorySecureApiKeyStore();
        var translationStore = new InMemorySecureApiKeyStore();
        var viewModel = Create(
            store: applicationStore,
            translationStore: translationStore);

        Assert.True(viewModel.SaveApplicationApiKey("application-key"));

        Assert.Equal("application-key", applicationStore.Load());
        Assert.Null(translationStore.Load());
        Assert.Equal("••••••••••••••", viewModel.ApplicationApiKeyStatus);
        Assert.DoesNotContain("application-key", viewModel.ApplicationApiKeyStatus);
    }

    [Fact]
    public void RemovingApplicationKey_DoesNotTouchTranslationCredential()
    {
        var applicationStore = new InMemorySecureApiKeyStore("application-key");
        var translationStore = new InMemorySecureApiKeyStore("translation-key");
        var viewModel = Create(
            store: applicationStore,
            translationStore: translationStore);

        viewModel.RemoveApplicationApiKeyCommand.Execute(null);

        Assert.Null(applicationStore.Load());
        Assert.Equal("translation-key", translationStore.Load());
    }

    [Fact]
    public void ApplicationCredential_UsesOnlyOpenAiKeyFileAndNeverSettingsJson()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "FloatingTools.Tests",
            Guid.NewGuid().ToString("N"));
        try
        {
            var settings = new AppSettings();
            var settingsPath = Path.Combine(directory, "settings.json");
            var settingsStore = new SettingsService(settingsPath);
            Assert.True(settingsStore.Save(settings));
            var stores = new WindowsDpapiAiCredentialStoreProvider(directory);
            var applicationStore = stores.GetStore(AiCredentialScope.Application);
            var translationStore = stores.GetStore(AiCredentialScope.Translation);
            var applicationProvider = new ApplicationOpenAiConfigurationProvider(
                settings,
                applicationStore,
                () => null);
            var translationProvider = new AiOpenAiConfigurationProvider(
                new AiConfigurationResolver(settings, stores),
                () => null);
            var viewModel = new SettingsViewModel(
                settings,
                settingsStore,
                applicationStore,
                translationProvider,
                new NullOpenAiConnectionTester(),
                _ => { },
                translationStore,
                applicationProvider);

            Assert.True(viewModel.SaveApplicationApiKey("application-secret"));

            Assert.True(File.Exists(stores.GetPath(AiCredentialScope.Application)));
            Assert.False(File.Exists(stores.GetPath(AiCredentialScope.Translation)));
            Assert.False(File.Exists(stores.GetPath(AiCredentialScope.QuickChat)));
            Assert.DoesNotContain("application-secret", File.ReadAllText(settingsPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void OpeningSettings_DoesNotCreateAnyCredentialFile()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "FloatingTools.Tests",
            Guid.NewGuid().ToString("N"));
        var settings = new AppSettings();
        var stores = new WindowsDpapiAiCredentialStoreProvider(directory);
        var applicationStore = stores.GetStore(AiCredentialScope.Application);
        var translationStore = stores.GetStore(AiCredentialScope.Translation);
        var applicationProvider = new ApplicationOpenAiConfigurationProvider(
            settings,
            applicationStore,
            () => null);
        var translationProvider = new AiOpenAiConfigurationProvider(
            new AiConfigurationResolver(settings, stores),
            () => null);

        var viewModel = new SettingsViewModel(
            settings,
            new InMemoryAppSettingsStore(settings),
            applicationStore,
            translationProvider,
            new NullOpenAiConnectionTester(),
            _ => { },
            translationStore,
            applicationProvider);

        _ = viewModel.ApplicationApiKeyStatus;
        _ = viewModel.ApiKeyStatus;
        Assert.False(File.Exists(stores.GetPath(AiCredentialScope.Application)));
        Assert.False(File.Exists(stores.GetPath(AiCredentialScope.Translation)));
        Assert.False(File.Exists(stores.GetPath(AiCredentialScope.QuickChat)));
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void SavingAndRemovingCustomKey_UsesOnlyTheTranslationCredentialPath()
    {
        var credentialDirectory = Path.Combine(
            Path.GetTempPath(),
            "FloatingTools.Tests",
            Guid.NewGuid().ToString("N"));
        try
        {
            var settings = new AppSettings
            {
                Ai = new AiSettings
                {
                    Translation = new AiToolSettings
                    {
                        UseAppCredentials = false,
                        Provider = OpenAiTranslationService.ProviderName,
                        IsModelSelectionInitialized = true
                    }
                }
            };
            var stores = new WindowsDpapiAiCredentialStoreProvider(credentialDirectory);
            var applicationStore = stores.GetStore(AiCredentialScope.Application);
            var translationStore = stores.GetStore(AiCredentialScope.Translation);
            applicationStore.Save("application-key");
            var configurationProvider = new AiOpenAiConfigurationProvider(
                new AiConfigurationResolver(settings, stores), () => null);
            var viewModel = new SettingsViewModel(
                settings,
                new InMemoryAppSettingsStore(settings),
                applicationStore,
                configurationProvider,
                new NullOpenAiConnectionTester(),
                _ => { },
                translationStore);

            Assert.True(viewModel.SaveApiKey("translation-key"));
            Assert.True(File.Exists(stores.GetPath(AiCredentialScope.Application)));
            Assert.True(File.Exists(stores.GetPath(AiCredentialScope.Translation)));
            Assert.Equal("application-key", applicationStore.Load());
            Assert.Equal("translation-key", translationStore.Load());

            viewModel.RemoveApiKeyCommand.Execute(null);

            Assert.True(File.Exists(stores.GetPath(AiCredentialScope.Application)));
            Assert.False(File.Exists(stores.GetPath(AiCredentialScope.Translation)));
            Assert.Equal("application-key", applicationStore.Load());
        }
        finally
        {
            if (Directory.Exists(credentialDirectory))
            {
                Directory.Delete(credentialDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(true, "translation-model", "translation-model", "application-key")]
    [InlineData(true, null, "application-model", "application-key")]
    [InlineData(false, "translation-model", "translation-model", "translation-key")]
    [InlineData(false, null, "application-model", "translation-key")]
    public async Task TestConnection_UsesEffectiveCredentialAndModel(
        bool useAppCredentials,
        string? translationModel,
        string expectedModel,
        string expectedApiKey)
    {
        var settings = new AppSettings
        {
            Ai = new AiSettings
            {
                DefaultModel = "application-model",
                Translation = new AiToolSettings
                {
                    UseAppCredentials = useAppCredentials,
                    Provider = useAppCredentials ? null : OpenAiTranslationService.ProviderName,
                    Model = translationModel,
                    IsModelSelectionInitialized = true
                }
            }
        };
        var applicationStore = new InMemorySecureApiKeyStore("application-key");
        var translationStore = new InMemorySecureApiKeyStore("translation-key");
        var credentialStores = new TranslationCredentialStores(
            applicationStore,
            translationStore);
        var configurationProvider = new AiOpenAiConfigurationProvider(
            new AiConfigurationResolver(settings, credentialStores), () => null);
        var tester = new RecordingConnectionTester(configurationProvider);
        var viewModel = Create(
            settings,
            applicationStore,
            configurationProvider,
            tester: tester,
            translationStore: translationStore);

        await viewModel.TestConnectionCommand.ExecuteAsync(null);

        Assert.Equal(expectedModel, tester.Configuration!.Model);
        Assert.Equal(expectedApiKey, tester.Configuration.ApiKey);
    }

    [Fact]
    public async Task ApplicationAndTranslationConnectionTests_UseTheirOwnEffectiveConfigurations()
    {
        var settings = new AppSettings
        {
            Ai = new AiSettings
            {
                DefaultModel = OpenAiModelOptions.SolModel,
                Translation = new AiToolSettings
                {
                    UseAppCredentials = false,
                    Provider = OpenAiTranslationService.ProviderName,
                    Model = OpenAiModelOptions.TerraModel,
                    IsModelSelectionInitialized = true
                }
            }
        };
        var applicationStore = new InMemorySecureApiKeyStore("application-key");
        var translationStore = new InMemorySecureApiKeyStore("translation-key");
        var credentialStores = new TranslationCredentialStores(
            applicationStore,
            translationStore);
        var applicationProvider = new ApplicationOpenAiConfigurationProvider(
            settings,
            applicationStore,
            () => null);
        var translationProvider = new AiOpenAiConfigurationProvider(
            new AiConfigurationResolver(settings, credentialStores),
            () => null);
        var applicationTester = new RecordingConnectionTester(applicationProvider);
        var translationTester = new RecordingConnectionTester(translationProvider);
        var viewModel = Create(
            settings,
            applicationStore,
            translationProvider,
            tester: translationTester,
            translationStore: translationStore,
            applicationProvider: applicationProvider,
            applicationTester: applicationTester);

        await viewModel.TestApplicationConnectionCommand.ExecuteAsync(null);
        await viewModel.TestConnectionCommand.ExecuteAsync(null);

        Assert.Equal("application-key", applicationTester.Configuration!.ApiKey);
        Assert.Equal(OpenAiModelOptions.SolModel, applicationTester.Configuration.Model);
        Assert.Equal("translation-key", translationTester.Configuration!.ApiKey);
        Assert.Equal(OpenAiModelOptions.TerraModel, translationTester.Configuration.Model);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(500)]
    public void HistoryLimit_PersistsAndAppliesImmediately(int limit)
    {
        var applied = 0;
        var initial = limit == HistoryLimitOptions.Default ? 50 : HistoryLimitOptions.Default;
        var viewModel = Create(
            new AppSettings { HistoryLimit = initial },
            historyLimitChanged: value => applied = value);

        viewModel.SelectedHistoryLimit = limit;

        Assert.Equal(limit, viewModel.SelectedHistoryLimit);
        Assert.Equal(limit, applied);
    }

    [Fact]
    public void ApplicationLanguage_DefaultsToSystemAndExposesAllSupportedChoices()
    {
        var viewModel = Create();

        Assert.Equal(ApplicationLanguageMode.System,
            viewModel.SelectedApplicationLanguage);
        Assert.Collection(
            viewModel.ApplicationLanguageChoices,
            choice => Assert.Equal(
                (ApplicationLanguageMode.System, "System"),
                (choice.Value, choice.DisplayName)),
            choice => Assert.Equal(
                (ApplicationLanguageMode.English, "English"),
                (choice.Value, choice.DisplayName)),
            choice => Assert.Equal(
                (ApplicationLanguageMode.Hebrew, "Hebrew"),
                (choice.Value, choice.DisplayName)));
    }

    [Fact]
    public void ApplicationLanguage_PersistsAndNotifiesCurrentConsumersImmediately()
    {
        var settings = new AppSettings();
        var store = new RecordingSettingsStore(settings);
        var viewModel = Create(settings, settingsStore: store);
        ApplicationLanguageMode? changed = null;
        viewModel.ApplicationLanguageChanged += value => changed = value;

        viewModel.SelectedApplicationLanguage = ApplicationLanguageMode.Hebrew;

        Assert.Equal(ApplicationLanguageMode.Hebrew, settings.ApplicationLanguage);
        Assert.Equal(ApplicationLanguageMode.Hebrew,
            viewModel.SelectedApplicationLanguage);
        Assert.Equal(ApplicationLanguageMode.Hebrew, changed);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public void ApplicationLanguage_InvalidSelectionNormalizesToSystem()
    {
        var settings = new AppSettings
        {
            ApplicationLanguage = ApplicationLanguageMode.Hebrew
        };
        var viewModel = Create(settings);

        viewModel.SelectedApplicationLanguage = (ApplicationLanguageMode)999;

        Assert.Equal(ApplicationLanguageMode.System, settings.ApplicationLanguage);
        Assert.Equal(ApplicationLanguageMode.System,
            viewModel.SelectedApplicationLanguage);
    }

    [Fact]
    public void Appearance_DefaultsToSystemAndExposesAllSupportedChoices()
    {
        var viewModel = Create();

        Assert.Equal(AppAppearanceMode.System, viewModel.SelectedAppearance);
        Assert.Collection(
            viewModel.AppearanceChoices,
            choice => Assert.Equal(
                (AppAppearanceMode.System, "System"),
                (choice.Value, choice.DisplayName)),
            choice => Assert.Equal(
                (AppAppearanceMode.Dark, "Dark"),
                (choice.Value, choice.DisplayName)),
            choice => Assert.Equal(
                (AppAppearanceMode.Light, "Light"),
                (choice.Value, choice.DisplayName)));
    }

    [Fact]
    public void Appearance_PersistsAndNotifiesCurrentConsumersImmediately()
    {
        var settings = new AppSettings();
        var store = new RecordingSettingsStore(settings);
        var viewModel = Create(settings, settingsStore: store);
        AppAppearanceMode? changed = null;
        viewModel.AppearanceChanged += value => changed = value;

        viewModel.SelectedAppearance = AppAppearanceMode.Light;

        Assert.Equal(AppAppearanceMode.Light, settings.Appearance);
        Assert.Equal(AppAppearanceMode.Light, viewModel.SelectedAppearance);
        Assert.Equal(AppAppearanceMode.Light, changed);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public void Appearance_InvalidSelectionNormalizesToSystem()
    {
        var settings = new AppSettings { Appearance = AppAppearanceMode.Dark };
        var viewModel = Create(settings);

        viewModel.SelectedAppearance = (AppAppearanceMode)999;

        Assert.Equal(AppAppearanceMode.System, settings.Appearance);
        Assert.Equal(AppAppearanceMode.System, viewModel.SelectedAppearance);
    }

    [Fact]
    public async Task TestConnection_UsesTesterAndOnlyUpdatesStatus()
    {
        var tester = new StubConnectionTester(
            new ConnectionTestResult(true, "Connection successful."));
        var viewModel = Create(tester: tester);

        await viewModel.TestConnectionCommand.ExecuteAsync(null);

        Assert.Equal(1, tester.Calls);
        Assert.Equal("Connection successful.", viewModel.ConnectionStatusMessage);
    }

    private static SettingsViewModel Create(
        AppSettings? settings = null,
        ISecureApiKeyStore? store = null,
        IOpenAiConfigurationProvider? provider = null,
        IAppSettingsStore? settingsStore = null,
        IOpenAiConnectionTester? tester = null,
        Action<int>? historyLimitChanged = null,
        ISecureApiKeyStore? translationStore = null,
        IOpenAiConfigurationProvider? applicationProvider = null,
        IOpenAiConnectionTester? applicationTester = null)
    {
        settings ??= new AppSettings();
        store ??= new InMemorySecureApiKeyStore();
        provider ??= new SettingsOpenAiConfigurationProvider(store, settings, () => null);
        return new SettingsViewModel(
            settings,
            settingsStore ?? new InMemoryAppSettingsStore(settings),
            store,
            provider,
            tester ?? new NullOpenAiConnectionTester(),
            historyLimitChanged ?? (_ => { }),
            translationStore,
            applicationProvider,
            applicationTester);
    }

    private sealed class RecordingSettingsStore(AppSettings settings)
        : IAppSettingsStore
    {
        public int SaveCount { get; private set; }

        public AppSettings Load() => settings;

        public bool Save(AppSettings value)
        {
            SaveCount++;
            return true;
        }
    }

    private sealed class StubConnectionTester(ConnectionTestResult result)
        : IOpenAiConnectionTester
    {
        public int Calls { get; private set; }

        public Task<ConnectionTestResult> TestAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingConnectionTester(IOpenAiConfigurationProvider provider)
        : IOpenAiConnectionTester
    {
        public OpenAiTranslationConfiguration? Configuration { get; private set; }

        public Task<ConnectionTestResult> TestAsync(CancellationToken cancellationToken)
        {
            Configuration = provider.GetConfiguration();
            return Task.FromResult(Configuration is null
                ? new ConnectionTestResult(false, "API key is not configured.")
                : new ConnectionTestResult(true, "Connection successful."));
        }
    }

    private sealed class TranslationCredentialStores(
        ISecureApiKeyStore application,
        ISecureApiKeyStore translation)
        : IAiCredentialStoreProvider
    {
        public ISecureApiKeyStore GetStore(AiCredentialScope scope) => scope switch
        {
            AiCredentialScope.Application => application,
            AiCredentialScope.Translation => translation,
            AiCredentialScope.QuickChat => new InMemorySecureApiKeyStore(),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
        };
    }
}
