using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingTools.App.Diagnostics;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.Services.OpenAI;
using FloatingTools.App.SharedUi.Direction;

namespace FloatingTools.App.ViewModels;

public partial class TranslationToolViewModel : ObservableObject
{
    private readonly ITranslationService _translationService;
    private readonly ITranslationHistoryStore _historyStore;
    private readonly IClipboardService _clipboardService;
    private readonly ISavedWordsService _savedWordsService;
    private readonly IFrequentWordsService _frequentWordsService;
    private readonly IScreenTextCaptureService _screenTextCaptureService;
    private readonly AppSettings _appSettings;
    private bool _historyLoaded;
    private CancellationTokenSource? _translationCancellation;
    private long _latestTranslationRequestId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InputFlowDirection))]
    [NotifyPropertyChangedFor(nameof(InputTextAlignment))]
    [NotifyCanExecuteChangedFor(nameof(ClearInputCommand))]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private bool _isTranslating;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearInputCommand))]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _inputValidationMessage;

    [ObservableProperty]
    private string? _captureMessage;

    [ObservableProperty]
    private bool _isCapturingText;

    [ObservableProperty]
    private bool _isAppMenuOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFeedPage))]
    [NotifyPropertyChangedFor(nameof(IsSavedWordsPage))]
    [NotifyPropertyChangedFor(nameof(IsFrequentWordsPage))]
    [NotifyPropertyChangedFor(nameof(IsSettingsPage))]
    private TranslationAssistantPage _currentPage;

    [ObservableProperty]
    private TranslationEntryViewModel? _activeExpandedEntry;

    public TranslationToolViewModel(
        ITranslationService translationService,
        ITranslationHistoryStore historyStore,
        IClipboardService clipboardService,
        IOpenAiConfigurationProvider configurationProvider)
        : this(
            translationService,
            historyStore,
            clipboardService,
            CreateInMemorySavedWordsService(),
            new NullSavedWordsExportService(),
            CreateInMemoryFrequentWordsService(),
            configurationProvider)
    {
    }

    public TranslationToolViewModel(
        ITranslationService translationService,
        ITranslationHistoryStore historyStore,
        IClipboardService clipboardService,
        ISavedWordsService savedWordsService,
        ISavedWordsExportService savedWordsExportService,
        IOpenAiConfigurationProvider configurationProvider)
        : this(
            translationService,
            historyStore,
            clipboardService,
            savedWordsService,
            savedWordsExportService,
            CreateInMemoryFrequentWordsService(),
            configurationProvider)
    {
    }

    public TranslationToolViewModel(
        ITranslationService translationService,
        ITranslationHistoryStore historyStore,
        IClipboardService clipboardService,
        ISavedWordsService savedWordsService,
        ISavedWordsExportService savedWordsExportService,
        IFrequentWordsService frequentWordsService,
        IOpenAiConfigurationProvider configurationProvider,
        AppSettings? appSettings = null,
        IAppSettingsStore? settingsStore = null,
        ISecureApiKeyStore? secureApiKeyStore = null,
        IOpenAiConnectionTester? connectionTester = null,
        IScreenTextCaptureService? screenTextCaptureService = null,
        ISecureApiKeyStore? translationCredentialStore = null,
        IOpenAiConfigurationProvider? applicationConfigurationProvider = null,
        IOpenAiConnectionTester? applicationConnectionTester = null)
    {
        _translationService = translationService
            ?? throw new ArgumentNullException(nameof(translationService));
        _historyStore = historyStore
            ?? throw new ArgumentNullException(nameof(historyStore));
        _clipboardService = clipboardService
            ?? throw new ArgumentNullException(nameof(clipboardService));
        _savedWordsService = savedWordsService
            ?? throw new ArgumentNullException(nameof(savedWordsService));
        _frequentWordsService = frequentWordsService
            ?? throw new ArgumentNullException(nameof(frequentWordsService));
        _screenTextCaptureService = screenTextCaptureService
            ?? new UnavailableScreenTextCaptureService();
        _appSettings = appSettings ?? new AppSettings();
        settingsStore ??= new InMemoryAppSettingsStore(_appSettings);
        secureApiKeyStore ??= new InMemorySecureApiKeyStore();
        ArgumentNullException.ThrowIfNull(configurationProvider);
        connectionTester ??= new NullOpenAiConnectionTester();
        SavedWords = new SavedWordsViewModel(
            _savedWordsService,
            _clipboardService,
            savedWordsExportService
                ?? throw new ArgumentNullException(nameof(savedWordsExportService)));
        FrequentWords = new FrequentWordsViewModel(
            _frequentWordsService,
            _savedWordsService,
            _clipboardService,
            savedWordsExportService);

        SendCommand = new AsyncRelayCommand(
            RequestTranslationAsync,
            CanSend,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        CaptureTextCommand = new AsyncRelayCommand(CaptureTextAsync);
        ClearHistoryCommand = new RelayCommand(ClearHistory, CanClearHistory);
        Settings = new SettingsViewModel(
            _appSettings,
            settingsStore,
            secureApiKeyStore,
            configurationProvider,
            connectionTester,
            ApplyHistoryLimit,
            translationCredentialStore,
            applicationConfigurationProvider,
            applicationConnectionTester);
        Items.CollectionChanged += (_, _) =>
            ClearHistoryCommand.NotifyCanExecuteChanged();
    }

    public ObservableCollection<TranslationEntryViewModel> Items { get; } = [];

    public SavedWordsViewModel SavedWords { get; }

    public FrequentWordsViewModel FrequentWords { get; }

    public SettingsViewModel Settings { get; }

    public bool IsFeedPage => CurrentPage == TranslationAssistantPage.Feed;

    public bool IsSavedWordsPage =>
        CurrentPage == TranslationAssistantPage.SavedWords;

    public bool IsFrequentWordsPage =>
        CurrentPage == TranslationAssistantPage.FrequentWords;

    public bool IsSettingsPage => CurrentPage == TranslationAssistantPage.Settings;

    public IAsyncRelayCommand SendCommand { get; }

    public IAsyncRelayCommand CaptureTextCommand { get; }

    public IRelayCommand ClearHistoryCommand { get; }

    public event EventHandler? ComposerFocusRequested;

    /// <summary>
    /// Outcome of the most recent Extract Text from Screen run. Callers that
    /// react to a capture (the global Ctrl+Alt+T handler) use this to avoid
    /// pulling the user into Translation when they cancelled the capture.
    /// </summary>
    public ScreenTextCaptureStatus? LastCaptureStatus { get; private set; }

    private async Task CaptureTextAsync()
    {
        if (IsCapturingText)
        {
            return;
        }

        IsCapturingText = true;
        CaptureMessage = null;
        ErrorMessage = null;
        LastCaptureStatus = null;
        try
        {
            var result = await _screenTextCaptureService.CaptureTextAsync();
            LastCaptureStatus = result.Status;
            switch (result.Status)
            {
                case ScreenTextCaptureStatus.Success
                    when !string.IsNullOrWhiteSpace(result.Text):
                    InputText = result.Text;
                    ComposerFocusRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case ScreenTextCaptureStatus.NoText:
                    CaptureMessage = "No text detected.";
                    break;
                case ScreenTextCaptureStatus.Failed:
                    ErrorMessage = "Could not read text from the selected area.";
                    break;
            }
        }
        catch
        {
            LastCaptureStatus = ScreenTextCaptureStatus.Failed;
            ErrorMessage = "Could not read text from the selected area.";
        }
        finally
        {
            IsCapturingText = false;
        }
    }

    public FlowDirection InputFlowDirection =>
        TextDirectionResolver.Resolve(InputText).ToFlowDirection();

    public TextAlignment InputTextAlignment =>
        TextDirectionResolver.Resolve(InputText).ToPhysicalTextAlignment();

    public async Task LoadHistoryAsync(CancellationToken cancellationToken = default)
    {
        if (_historyLoaded)
        {
            return;
        }

        var entries = await _historyStore.LoadAsync(cancellationToken);
        foreach (var entry in entries
                     .OrderByDescending(entry => entry.CreatedAt)
                     .Take(_appSettings.HistoryLimit)
                     .OrderBy(entry => entry.CreatedAt))
        {
            Items.Add(CreateEntryViewModel(entry));
        }

        _historyStore.TrimToLimit(_appSettings.HistoryLimit);

        _historyLoaded = true;
    }

    public async Task RequestTranslationAsync()
    {
        if (!CanSend())
        {
            return;
        }

        using var timing = DebugAiRequestTiming.Start("translation", "ui_pipeline");
        var sourceText = InputText;
        var direction = TranslationDirectionResolver.Resolve(
            sourceText,
            _appSettings.LanguageMode);
        var requestId = Interlocked.Increment(ref _latestTranslationRequestId);
        var requestCancellation = new CancellationTokenSource();
        var previousCancellation = Interlocked.Exchange(
            ref _translationCancellation,
            requestCancellation);
        previousCancellation?.Cancel();

        IsTranslating = true;
        ErrorMessage = null;
        timing.Mark("local_state_prepared");

        try
        {
            timing.Mark("service_request_started");
            var result = await _translationService.TranslateAsync(
                sourceText,
                direction.SourceLanguage,
                direction.TargetLanguage,
                requestCancellation.Token);
            timing.Mark("service_result_available");
            if (requestId != Volatile.Read(ref _latestTranslationRequestId))
            {
                timing.Complete("superseded");
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var entry = new TranslationEntry
            {
                Id = Guid.NewGuid(),
                SourceText = sourceText,
                Result = result,
                CreatedAt = now,
                IsFavorite = false,
                UsageCount = 1,
                LastUsedAt = now
            };

            await _historyStore.AddOrUpdateAsync(
                entry,
                requestCancellation.Token);
            timing.Mark("history_persisted");
            if (requestId != Volatile.Read(ref _latestTranslationRequestId))
            {
                timing.Complete("superseded");
                return;
            }

            Items.Add(CreateEntryViewModel(entry));
            ApplyHistoryLimit(_appSettings.HistoryLimit);
            InputText = string.Empty;
            timing.Mark("ui_updated");

            if (result.CorrectionStatus != TranslationCorrectionStatus.Ambiguous
                && !string.IsNullOrWhiteSpace(result.MainTranslation))
            {
                try
                {
                    await _frequentWordsService.RecordSuccessfulTranslationAsync(
                        sourceText,
                        result.MainTranslation,
                        result.DetectedLanguage ?? direction.SourceLanguage,
                        result.TargetLanguage ?? direction.TargetLanguage,
                        now,
                        CancellationToken.None);
                    timing.Mark("frequency_data_persisted");
                }
                catch (Exception)
                {
                    // Frequency tracking is secondary and must never hide a
                    // successful translation from the user.
                }
            }

            timing.Complete("success");
        }
        catch (OperationCanceledException)
            when (requestCancellation.IsCancellationRequested)
        {
            // A newer request superseded this one. Only its result may update the UI.
            timing.Complete("cancelled");
        }
        catch (Exception)
            when (requestId != Volatile.Read(ref _latestTranslationRequestId))
        {
            // Ignore failures from a request that has already been superseded.
            timing.Complete("superseded");
        }
        catch (TranslationProviderNotConfiguredException exception)
            when (requestId == Volatile.Read(ref _latestTranslationRequestId))
        {
            ErrorMessage = exception.Message;
            timing.Complete("not_configured");
        }
        catch (TranslationServiceException exception)
            when (requestId == Volatile.Read(ref _latestTranslationRequestId))
        {
            ErrorMessage = exception.Message;
            timing.Complete("service_failure");
        }
        catch (Exception)
            when (requestId == Volatile.Read(ref _latestTranslationRequestId))
        {
            ErrorMessage = "Translation failed. Please try again.";
            timing.Complete("failure");
        }
        finally
        {
            if (requestId == Volatile.Read(ref _latestTranslationRequestId))
            {
                Interlocked.CompareExchange(
                    ref _translationCancellation,
                    null,
                    requestCancellation);
                IsTranslating = false;
            }

            requestCancellation.Dispose();
        }
    }

    [RelayCommand(CanExecute = nameof(CanClearInput))]
    private void ClearInput()
    {
        InputText = string.Empty;
        ErrorMessage = null;
    }

    [RelayCommand]
    private void ToggleAppMenu()
    {
        IsAppMenuOpen = !IsAppMenuOpen;
    }

    [RelayCommand]
    private void OpenFeed() => NavigateToPage(TranslationAssistantPage.Feed);

    [RelayCommand]
    private void OpenSavedWords()
        => NavigateToPage(TranslationAssistantPage.SavedWords);

    [RelayCommand]
    private void OpenFrequentWords()
        => NavigateToPage(TranslationAssistantPage.FrequentWords);

    [RelayCommand]
    private void OpenSettings()
        => NavigateToPage(TranslationAssistantPage.Settings);

    [RelayCommand]
    private void BackToFeed()
        => NavigateToPage(TranslationAssistantPage.Feed);

    private void NavigateToPage(TranslationAssistantPage destination)
    {
        CloseAppMenu();
        if (CurrentPage == destination)
        {
            return;
        }

        if (CurrentPage == TranslationAssistantPage.Settings)
        {
            Settings.CancelApiKeyEditCommand.Execute(null);
        }

        CurrentPage = destination;
    }

    private void CloseAppMenu() => IsAppMenuOpen = false;

    private bool CanSend() =>
        !string.IsNullOrWhiteSpace(InputText)
        && TranslationInputLimits.IsWithinWordLimit(InputText);

    private bool CanClearInput() =>
        !string.IsNullOrEmpty(InputText) || !string.IsNullOrEmpty(ErrorMessage);

    private void ClearHistory()
    {
        ActiveExpandedEntry = null;
        Items.Clear();
        try
        {
            _historyStore.Clear();
        }
        catch
        {
            ErrorMessage = "Could not clear history.";
        }
    }

    private bool CanClearHistory() => Items.Count > 0;

    private void ApplyHistoryLimit(int maximumEntries)
    {
        maximumEntries = HistoryLimitOptions.Normalize(maximumEntries);
        while (Items.Count > maximumEntries)
        {
            Items.RemoveAt(0);
        }

        try
        {
            _historyStore.TrimToLimit(maximumEntries);
        }
        catch
        {
            ErrorMessage = "Could not update history.";
        }
    }

    private TranslationEntryViewModel CreateEntryViewModel(TranslationEntry entry) =>
        new(
            entry,
            _clipboardService,
            _historyStore,
            errorMessage => ErrorMessage = errorMessage,
            ToggleEntryActions,
            _translationService,
            _savedWordsService);

    private void ToggleEntryActions(TranslationEntryViewModel entry)
    {
        if (entry.IsExpanded)
        {
            entry.SetExpanded(false);
            if (ReferenceEquals(ActiveExpandedEntry, entry))
            {
                ActiveExpandedEntry = null;
            }

            return;
        }

        ActiveExpandedEntry?.SetExpanded(false);
        entry.SetExpanded(true);
        ActiveExpandedEntry = entry;
    }

#if DEBUG
    internal void AddDebugPreviewEntry(TranslationEntry entry)
    {
        Items.Add(new TranslationEntryViewModel(
            entry,
            _clipboardService,
            new InMemoryTranslationHistoryStore(),
            errorMessage => ErrorMessage = errorMessage,
            ToggleEntryActions,
            _translationService,
            _savedWordsService));
    }
#endif

    partial void OnInputTextChanged(string value)
    {
        CaptureMessage = null;
        InputValidationMessage = TranslationInputLimits.IsWithinWordLimit(value)
            ? null
            : TranslationInputLimits.MaximumWordCountMessage;
        SendCommand.NotifyCanExecuteChanged();
    }

    private static ISavedWordsService CreateInMemorySavedWordsService()
    {
        var service = new SavedWordsService(new InMemorySavedWordsStore());
        service.InitializeAsync().GetAwaiter().GetResult();
        return service;
    }

    private static IFrequentWordsService CreateInMemoryFrequentWordsService()
    {
        var service = new FrequentWordsService(new InMemoryFrequentWordsStore());
        service.InitializeAsync().GetAwaiter().GetResult();
        return service;
    }

}
