using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.App.ViewModels;

public sealed partial class QuickChatSettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly IAppSettingsStore _settingsStore;
    private readonly ISecureApiKeyStore _credentialStore;
    private readonly IOpenAiConfigurationProvider _configurationProvider;
    private readonly IOpenAiConnectionTester _connectionTester;
    private string? _selectedModel;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _connectionStatusMessage;

    [ObservableProperty]
    private bool _isTestingConnection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsApiKeyDisplayMode))]
    private bool _isApiKeyEditing;

    public QuickChatSettingsViewModel(
        AppSettings settings,
        IAppSettingsStore settingsStore,
        ISecureApiKeyStore credentialStore,
        IOpenAiConfigurationProvider configurationProvider,
        IOpenAiConnectionTester connectionTester)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _configurationProvider = configurationProvider
            ?? throw new ArgumentNullException(nameof(configurationProvider));
        _connectionTester = connectionTester
            ?? throw new ArgumentNullException(nameof(connectionTester));
        _selectedModel = settings.Ai.QuickChat.Model;
        ModelOptions = CreateModelOptions(_selectedModel);
    }

    public IReadOnlyList<AiModelChoice> ModelOptions { get; }

    public bool UseAppCredentials
    {
        get => _settings.Ai.QuickChat.UseAppCredentials;
        set
        {
            if (value == _settings.Ai.QuickChat.UseAppCredentials)
            {
                return;
            }

            var previousUseAppCredentials = _settings.Ai.QuickChat.UseAppCredentials;
            var previousProvider = _settings.Ai.QuickChat.Provider;
            _settings.Ai.QuickChat.UseAppCredentials = value;
            if (!value && string.IsNullOrWhiteSpace(_settings.Ai.QuickChat.Provider))
            {
                _settings.Ai.QuickChat.Provider = OpenAiTranslationService.ProviderName;
            }

            if (!_settingsStore.Save(_settings))
            {
                _settings.Ai.QuickChat.UseAppCredentials = previousUseAppCredentials;
                _settings.Ai.QuickChat.Provider = previousProvider;
                ErrorMessage = "Could not save settings.";
                return;
            }

            ErrorMessage = null;
            ConnectionStatusMessage = null;
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
        : _settings.Ai.QuickChat.Provider ?? OpenAiTranslationService.ProviderName;

    public string? SelectedModel
    {
        get => _selectedModel;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? null
                : OpenAiModelOptions.Normalize(value);
            if (string.Equals(_selectedModel, normalized, StringComparison.Ordinal))
            {
                return;
            }

            var previous = _settings.Ai.QuickChat.Model;
            var previousInitialized = _settings.Ai.QuickChat.IsModelSelectionInitialized;
            _settings.Ai.QuickChat.Model = normalized;
            _settings.Ai.QuickChat.IsModelSelectionInitialized = true;
            if (!_settingsStore.Save(_settings))
            {
                _settings.Ai.QuickChat.Model = previous;
                _settings.Ai.QuickChat.IsModelSelectionInitialized = previousInitialized;
                ErrorMessage = "Could not save settings.";
                return;
            }

            SetProperty(ref _selectedModel, normalized);
            ConnectionStatusMessage = null;
            ErrorMessage = null;
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
                if (_credentialStore.HasKey)
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
                return _credentialStore.HasKey;
            }
            catch
            {
                return false;
            }
        }
    }

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

    public bool IsApiKeyDisplayMode => !IsApiKeyEditing;

    public bool SaveApiKey(string? apiKey)
    {
        if (UseAppCredentials)
        {
            ErrorMessage = "Switch to custom credentials to save a Quick Chat API key.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            ErrorMessage = "Enter an API key.";
            return false;
        }

        try
        {
            _credentialStore.Save(apiKey);
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

    [RelayCommand]
    private void BeginApiKeyEdit()
    {
        if (!UseAppCredentials)
        {
            ErrorMessage = null;
            ConnectionStatusMessage = null;
            IsApiKeyEditing = true;
        }
    }

    [RelayCommand]
    private void CancelApiKeyEdit()
    {
        ErrorMessage = null;
        IsApiKeyEditing = false;
    }

    [RelayCommand(CanExecute = nameof(HasStoredApiKey))]
    private void RemoveApiKey()
    {
        try
        {
            _credentialStore.Remove();
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
            ConnectionStatusMessage = (await _connectionTester.TestAsync(cancellationToken)).Message;
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

    private void RefreshApiKeyState()
    {
        OnPropertyChanged(nameof(ApiKeyStatus));
        OnPropertyChanged(nameof(HasStoredApiKey));
        OnPropertyChanged(nameof(HasApiKeyConfiguration));
        RemoveApiKeyCommand.NotifyCanExecuteChanged();
    }

    private static IReadOnlyList<AiModelChoice> CreateModelOptions(string? selectedModel)
    {
        var options = new List<AiModelChoice> { new(null, "App default") };
        options.AddRange(OpenAiModelOptions.Supported.Select(model => new AiModelChoice(
            model,
            model switch
            {
                OpenAiModelOptions.SolModel => "GPT-5.6 Sol",
                OpenAiModelOptions.TerraModel => "GPT-5.6 Terra",
                OpenAiModelOptions.LunaModel => "GPT-5.6 Luna",
                _ => model
            })));
        if (string.Equals(selectedModel, OpenAiModelOptions.LegacyNanoModel, StringComparison.Ordinal))
        {
            options.Add(new AiModelChoice(
                OpenAiModelOptions.LegacyNanoModel,
                "GPT-5.4 Nano (legacy)",
                IsSelectable: false));
        }

        return options;
    }
}
