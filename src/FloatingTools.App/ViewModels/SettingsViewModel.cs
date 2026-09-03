using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly IAppSettingsStore _settingsStore;
    private readonly ISecureApiKeyStore _applicationCredentialStore;
    private readonly ISecureApiKeyStore _translationCredentialStore;
    private readonly IOpenAiConfigurationProvider _applicationConfigurationProvider;
    private readonly IOpenAiConfigurationProvider _configurationProvider;
    private readonly IOpenAiConnectionTester _applicationConnectionTester;
    private readonly IOpenAiConnectionTester _connectionTester;
    private readonly Action<int> _historyLimitChanged;
    private ApplicationLanguageMode _selectedApplicationLanguage;
    private AppAppearanceMode _selectedAppearance;
    private string _selectedApplicationDefaultModel;
    private string? _selectedTranslationAiModel;
    private TranslationLanguageMode _selectedLanguageMode;
    private int _selectedHistoryLimit;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _connectionStatusMessage;

    [ObservableProperty]
    private string? _applicationConnectionStatusMessage;

    [ObservableProperty]
    private bool _isTestingConnection;

    [ObservableProperty]
    private bool _isTestingApplicationConnection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsApiKeyDisplayMode))]
    private bool _isApiKeyEditing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsApplicationApiKeyDisplayMode))]
    private bool _isApplicationApiKeyEditing;

    [ObservableProperty]
    private string? _globalShortcutsStatusMessage;

    public SettingsViewModel(
        AppSettings settings,
        IAppSettingsStore settingsStore,
        ISecureApiKeyStore secureApiKeyStore,
        IOpenAiConfigurationProvider configurationProvider,
        IOpenAiConnectionTester connectionTester,
        Action<int> historyLimitChanged,
        ISecureApiKeyStore? translationCredentialStore = null,
        IOpenAiConfigurationProvider? applicationConfigurationProvider = null,
        IOpenAiConnectionTester? applicationConnectionTester = null)
    {
        _settings = settings;
        _settingsStore = settingsStore;
        _applicationCredentialStore = secureApiKeyStore;
        _translationCredentialStore = translationCredentialStore
            ?? new InMemorySecureApiKeyStore();
        _applicationConfigurationProvider = applicationConfigurationProvider
            ?? new ApplicationOpenAiConfigurationProvider(
                settings,
                secureApiKeyStore,
                () => null);
        _configurationProvider = configurationProvider;
        _applicationConnectionTester = applicationConnectionTester
            ?? new NullOpenAiConnectionTester();
        _connectionTester = connectionTester;
        _historyLimitChanged = historyLimitChanged;
        _selectedApplicationLanguage = Enum.IsDefined(settings.ApplicationLanguage)
            ? settings.ApplicationLanguage
            : ApplicationLanguageMode.System;
        _selectedAppearance = Enum.IsDefined(settings.Appearance)
            ? settings.Appearance
            : AppAppearanceMode.System;
        _selectedApplicationDefaultModel = OpenAiModelOptions.Normalize(
            settings.Ai.DefaultModel);
        _selectedTranslationAiModel = settings.Ai.Translation.Model;
        ApplicationDefaultModelOptions = CreateConcreteModelOptions(
            _selectedApplicationDefaultModel);
        TranslationModelOptions = CreateTranslationModelOptions(
            _selectedTranslationAiModel);
        _selectedLanguageMode = Enum.IsDefined(settings.LanguageMode)
            ? settings.LanguageMode
            : TranslationLanguageMode.Automatic;
        _selectedHistoryLimit = HistoryLimitOptions.Normalize(settings.HistoryLimit);
    }

    public IReadOnlyList<AiModelChoice> ApplicationDefaultModelOptions { get; }

    public IReadOnlyList<AiModelChoice> TranslationModelOptions { get; }

    public IReadOnlyList<ApplicationLanguageChoice> ApplicationLanguageChoices { get; } =
    [
        new(ApplicationLanguageMode.System, "System"),
        new(ApplicationLanguageMode.English, "English"),
        new(ApplicationLanguageMode.Hebrew, "Hebrew")
    ];

    public IReadOnlyList<AppAppearanceChoice> AppearanceChoices { get; } =
    [
        new(AppAppearanceMode.System, "System"),
        new(AppAppearanceMode.Dark, "Dark"),
        new(AppAppearanceMode.Light, "Light")
    ];

    public IReadOnlyList<int> SupportedHistoryLimits => HistoryLimitOptions.Supported;

    public IReadOnlyList<LanguageModeChoice> LanguageModes { get; } =
    [
        new(TranslationLanguageMode.Automatic, "Automatic Hebrew ↔ English"),
        new(TranslationLanguageMode.HebrewToEnglish, "Hebrew → English"),
        new(TranslationLanguageMode.EnglishToHebrew, "English → Hebrew")
    ];

    public event Action<ApplicationLanguageMode>? ApplicationLanguageChanged;

    public ApplicationLanguageMode SelectedApplicationLanguage
    {
        get => _selectedApplicationLanguage;
        set
        {
            var normalized = Enum.IsDefined(value)
                ? value
                : ApplicationLanguageMode.System;
            if (normalized == _selectedApplicationLanguage
                || !PersistSetting(
                    () => _settings.ApplicationLanguage,
                    previous => _settings.ApplicationLanguage = previous,
                    () => _settings.ApplicationLanguage = normalized))
            {
                return;
            }

            SetProperty(ref _selectedApplicationLanguage, normalized);
            ApplicationLanguageChanged?.Invoke(normalized);
        }
    }

    public event Action<AppAppearanceMode>? AppearanceChanged;

    public AppAppearanceMode SelectedAppearance
    {
        get => _selectedAppearance;
        set
        {
            var normalized = Enum.IsDefined(value)
                ? value
                : AppAppearanceMode.System;
            if (normalized == _selectedAppearance
                || !PersistSetting(
                    () => _settings.Appearance,
                    previous => _settings.Appearance = previous,
                    () => _settings.Appearance = normalized))
            {
                return;
            }

            SetProperty(ref _selectedAppearance, normalized);
            AppearanceChanged?.Invoke(normalized);
        }
    }

    public string ApplicationProviderDisplayName => _settings.Ai.DefaultProvider;

    public string ApplicationApiKeyStatus
    {
        get
        {
            try
            {
                if (_applicationCredentialStore.HasKey)
                {
                    return "••••••••••••••";
                }
            }
            catch
            {
            }

            return _applicationConfigurationProvider.GetConfiguration() is null
                ? "Not configured"
                : "Configured from environment";
        }
    }

    public bool HasStoredApplicationApiKey
    {
        get
        {
            try
            {
                return _applicationCredentialStore.HasKey;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool IsApplicationApiKeyDisplayMode => !IsApplicationApiKeyEditing;

    public bool HasApplicationApiKeyConfiguration
    {
        get
        {
            try
            {
                return _applicationConfigurationProvider.GetConfiguration() is not null;
            }
            catch
            {
                return false;
            }
        }
    }

    public string ApplicationApiKeySaveActionLabel => HasStoredApplicationApiKey
        ? "Replace"
        : "Add API Key";

    public string SelectedApplicationDefaultModel
    {
        get => _selectedApplicationDefaultModel;
        set
        {
            var normalized = OpenAiModelOptions.Normalize(value);
            if (string.Equals(
                    normalized,
                    _selectedApplicationDefaultModel,
                    StringComparison.Ordinal))
            {
                return;
            }

            var previous = _settings.Ai.DefaultModel;
            _settings.Ai.DefaultModel = normalized;
            if (!_settingsStore.Save(_settings))
            {
                _settings.Ai.DefaultModel = previous;
                ErrorMessage = "Could not save settings.";
                return;
            }

            SetProperty(ref _selectedApplicationDefaultModel, normalized);
            ApplicationConnectionStatusMessage = null;
            ErrorMessage = null;
            OnPropertyChanged(nameof(SelectedTranslationModel));
        }
    }

    public string ApiKeyStatus
    {
        get
        {
            if (UseAppCredentials)
            {
                return "Using app credentials";
            }

            try
            {
                if (_translationCredentialStore.HasKey)
                {
                    return "••••••••••••••";
                }
            }
            catch
            {
            }

            return _configurationProvider.GetConfiguration() is null
                ? "Not configured"
                : "Configured from environment";
        }
    }

    public bool HasStoredApiKey
    {
        get
        {
            if (UseAppCredentials)
            {
                return false;
            }

            try
            {
                return _translationCredentialStore.HasKey;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool IsApiKeyDisplayMode => !IsApiKeyEditing;

    public bool HasApiKeyConfiguration
    {
        get
        {
            try
            {
                return _configurationProvider.GetConfiguration() is not null;
            }
            catch
            {
                return false;
            }
        }
    }

    public string ApiKeySaveActionLabel => HasStoredApiKey
        ? "Replace"
        : "Add API Key";

    public bool UseAppCredentials
    {
        get => _settings.Ai.Translation.UseAppCredentials;
        set
        {
            if (value == _settings.Ai.Translation.UseAppCredentials)
            {
                return;
            }

            var previousUseAppCredentials = _settings.Ai.Translation.UseAppCredentials;
            var previousProvider = _settings.Ai.Translation.Provider;
            _settings.Ai.Translation.UseAppCredentials = value;
            if (!value && string.IsNullOrWhiteSpace(_settings.Ai.Translation.Provider))
            {
                _settings.Ai.Translation.Provider = OpenAiTranslationService.ProviderName;
            }

            if (!_settingsStore.Save(_settings))
            {
                _settings.Ai.Translation.UseAppCredentials = previousUseAppCredentials;
                _settings.Ai.Translation.Provider = previousProvider;
                ErrorMessage = "Could not save settings.";
                return;
            }

            ErrorMessage = null;
            if (value)
            {
                IsApiKeyEditing = false;
            }

            OnPropertyChanged(nameof(UseAppCredentials));
            OnPropertyChanged(nameof(IsUsingCustomCredentials));
            OnPropertyChanged(nameof(ProviderDisplayName));
            RefreshApiKeyState();
        }
    }

    public bool IsUsingCustomCredentials => !UseAppCredentials;

    public string ProviderDisplayName => UseAppCredentials
        ? _settings.Ai.DefaultProvider
        : _settings.Ai.Translation.Provider ?? OpenAiTranslationService.ProviderName;

    public string? SelectedTranslationAiModel
    {
        get => _selectedTranslationAiModel;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? null
                : OpenAiModelOptions.Normalize(value);
            if (SetProperty(ref _selectedTranslationAiModel, normalized))
            {
                PersistTranslationAiModel(normalized);
                OnPropertyChanged(nameof(SelectedTranslationModel));
            }
        }
    }

    public string SelectedTranslationModel
    {
        get => OpenAiModelOptions.Normalize(
            SelectedTranslationAiModel ?? _settings.Ai.DefaultModel);
        set => SelectedTranslationAiModel = value;
    }

    public TranslationLanguageMode SelectedLanguageMode
    {
        get => _selectedLanguageMode;
        set
        {
            var normalized = Enum.IsDefined(value)
                ? value
                : TranslationLanguageMode.Automatic;
            if (SetProperty(ref _selectedLanguageMode, normalized))
            {
                PersistSetting(
                    () => _settings.LanguageMode,
                    previous => _settings.LanguageMode = previous,
                    () => _settings.LanguageMode = normalized);
            }
        }
    }

    public int SelectedHistoryLimit
    {
        get => _selectedHistoryLimit;
        set
        {
            var normalized = HistoryLimitOptions.Normalize(value);
            if (SetProperty(ref _selectedHistoryLimit, normalized))
            {
                if (PersistSetting(
                        () => _settings.HistoryLimit,
                        previous => _settings.HistoryLimit = previous,
                        () => _settings.HistoryLimit = normalized))
                {
                    _historyLimitChanged(normalized);
                }
            }
        }
    }

    public bool SaveApiKey(string? apiKey)
    {
        if (UseAppCredentials)
        {
            ErrorMessage = "Switch to custom credentials to save a Translation API key.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            ErrorMessage = "Enter an API key.";
            return false;
        }

        try
        {
            _translationCredentialStore.Save(apiKey);
            ErrorMessage = null;
            ConnectionStatusMessage = null;
            IsApiKeyEditing = false;
            RefreshApiKeyState();
            return true;
        }
        catch
        {
            ErrorMessage = "Could not save API key securely.";
            return false;
        }
    }

    public bool SaveApplicationApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            ErrorMessage = "Enter an API key.";
            return false;
        }

        try
        {
            _applicationCredentialStore.Save(apiKey);
            ErrorMessage = null;
            ApplicationConnectionStatusMessage = null;
            IsApplicationApiKeyEditing = false;
            RefreshApplicationApiKeyState();
            RefreshApiKeyState();
            return true;
        }
        catch
        {
            ErrorMessage = "Could not save API key securely.";
            return false;
        }
    }

    [RelayCommand]
    private void BeginApplicationApiKeyEdit()
    {
        ErrorMessage = null;
        ApplicationConnectionStatusMessage = null;
        IsApiKeyEditing = false;
        IsApplicationApiKeyEditing = true;
    }

    [RelayCommand]
    private void CancelApplicationApiKeyEdit()
    {
        ErrorMessage = null;
        ApplicationConnectionStatusMessage = null;
        IsApplicationApiKeyEditing = false;
    }

    [RelayCommand(CanExecute = nameof(HasStoredApplicationApiKey))]
    private void RemoveApplicationApiKey()
    {
        try
        {
            _applicationCredentialStore.Remove();
            ErrorMessage = null;
            ApplicationConnectionStatusMessage = null;
            IsApplicationApiKeyEditing = false;
            RefreshApplicationApiKeyState();
            RefreshApiKeyState();
        }
        catch
        {
            ErrorMessage = "Could not remove the API key securely.";
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task TestApplicationConnectionAsync(
        CancellationToken cancellationToken)
    {
        IsTestingApplicationConnection = true;
        ErrorMessage = null;
        try
        {
            var result = await _applicationConnectionTester.TestAsync(
                cancellationToken);
            ApplicationConnectionStatusMessage = result.Message;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ApplicationConnectionStatusMessage = null;
        }
        catch
        {
            ApplicationConnectionStatusMessage =
                "Could not verify the OpenAI connection.";
        }
        finally
        {
            IsTestingApplicationConnection = false;
        }
    }

    [RelayCommand]
    private void BeginApiKeyEdit()
    {
        if (UseAppCredentials)
        {
            return;
        }

        ErrorMessage = null;
        ConnectionStatusMessage = null;
        IsApplicationApiKeyEditing = false;
        IsApiKeyEditing = true;
    }

    [RelayCommand]
    private void CancelApiKeyEdit()
    {
        ErrorMessage = null;
        ConnectionStatusMessage = null;
        IsApiKeyEditing = false;
    }

    [RelayCommand(CanExecute = nameof(HasStoredApiKey))]
    private void RemoveApiKey()
    {
        try
        {
            _translationCredentialStore.Remove();
            ErrorMessage = null;
            ConnectionStatusMessage = null;
            IsApiKeyEditing = false;
            RefreshApiKeyState();
        }
        catch
        {
            ErrorMessage = "Could not remove the API key securely.";
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        IsTestingConnection = true;
        ErrorMessage = null;
        try
        {
            var result = await _connectionTester.TestAsync(cancellationToken);
            ConnectionStatusMessage = result.Message;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ConnectionStatusMessage = null;
        }
        catch
        {
            ConnectionStatusMessage = "Could not verify the OpenAI connection.";
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    private bool PersistSetting<T>(
        Func<T> getPrevious,
        Action<T> restore,
        Action update)
    {
        var previous = getPrevious();
        update();
        if (_settingsStore.Save(_settings))
        {
            ErrorMessage = null;
            return true;
        }

        restore(previous);
        ErrorMessage = "Could not save settings.";
        return false;
    }

    private bool PersistTranslationAiModel(string? model)
    {
        var previousTranslationModel = _settings.TranslationModel;
        var previousAiTranslationModel = _settings.Ai.Translation.Model;
        var previousModelSelectionInitialized =
            _settings.Ai.Translation.IsModelSelectionInitialized;
        _settings.Ai.Translation.Model = model;
        _settings.Ai.Translation.IsModelSelectionInitialized = true;
        if (model is not null)
        {
            _settings.TranslationModel = model;
        }

        if (_settingsStore.Save(_settings))
        {
            ErrorMessage = null;
            return true;
        }

        _settings.TranslationModel = previousTranslationModel;
        _settings.Ai.Translation.Model = previousAiTranslationModel;
        _settings.Ai.Translation.IsModelSelectionInitialized =
            previousModelSelectionInitialized;
        ErrorMessage = "Could not save settings.";
        return false;
    }

    private void RefreshApiKeyState()
    {
        OnPropertyChanged(nameof(ApiKeyStatus));
        OnPropertyChanged(nameof(HasStoredApiKey));
        OnPropertyChanged(nameof(ApiKeySaveActionLabel));
        OnPropertyChanged(nameof(HasApiKeyConfiguration));
        RemoveApiKeyCommand.NotifyCanExecuteChanged();
    }

    private void RefreshApplicationApiKeyState()
    {
        OnPropertyChanged(nameof(ApplicationApiKeyStatus));
        OnPropertyChanged(nameof(HasStoredApplicationApiKey));
        OnPropertyChanged(nameof(ApplicationApiKeySaveActionLabel));
        OnPropertyChanged(nameof(HasApplicationApiKeyConfiguration));
        RemoveApplicationApiKeyCommand.NotifyCanExecuteChanged();
    }

    private static string GetModelDisplayName(string model) => model switch
    {
        OpenAiModelOptions.SolModel => "GPT-5.6 Sol",
        OpenAiModelOptions.TerraModel => "GPT-5.6 Terra",
        OpenAiModelOptions.LunaModel => "GPT-5.6 Luna",
        OpenAiModelOptions.LegacyNanoModel => "GPT-5.4 Nano (legacy)",
        _ => model
    };

    private static IReadOnlyList<AiModelChoice> CreateTranslationModelOptions(
        string? selectedModel)
    {
        var options = new List<AiModelChoice> { new(null, "App default") };
        options.AddRange(CreateConcreteModelOptions(selectedModel));

        return options;
    }

    private static IReadOnlyList<AiModelChoice> CreateConcreteModelOptions(
        string? selectedModel)
    {
        var options = OpenAiModelOptions.Supported.Select(model =>
            new AiModelChoice(model, GetModelDisplayName(model))).ToList();

        if (string.Equals(
                selectedModel,
                OpenAiModelOptions.LegacyNanoModel,
                StringComparison.Ordinal))
        {
            options.Add(new AiModelChoice(
                OpenAiModelOptions.LegacyNanoModel,
                GetModelDisplayName(OpenAiModelOptions.LegacyNanoModel),
                IsSelectable: false));
        }

        return options;
    }
}

public sealed record LanguageModeChoice(
    TranslationLanguageMode Value,
    string DisplayName);

public sealed record ApplicationLanguageChoice(
    ApplicationLanguageMode Value,
    string DisplayName);

public sealed record AppAppearanceChoice(
    AppAppearanceMode Value,
    string DisplayName);

public sealed record AiModelChoice(
    string? Model,
    string DisplayName,
    bool IsSelectable = true);
