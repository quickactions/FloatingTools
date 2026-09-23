using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.Services.OpenAI;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.ViewModels;

public sealed class ContextualWordTranslationViewModelTests
{
    [Fact]
    public async Task Lookup_AppendsNormalPersistedResultAndPreservesEntryAndComposer()
    {
        var service = new ControlledTranslationService();
        var history = new InMemoryTranslationHistoryStore();
        var original = CreateEntry(
            "I'm going to apply for a job.",
            "אני הולכת להגיש מועמדות לעבודה.");
        await history.AddOrUpdateAsync(original);
        var viewModel = CreateViewModel(service, history);
        await viewModel.LoadHistoryAsync();
        viewModel.InputText = "composer draft";

        await viewModel.TranslateEnglishWordInContextCommand.ExecuteAsync(
            new ContextualWordTranslationRequest("apply", original.SourceText));

        Assert.Equal("apply", service.ContextualWord);
        Assert.Equal(original.SourceText, service.Context);
        Assert.Equal("composer draft", viewModel.InputText);
        Assert.Equal(2, viewModel.Items.Count);
        Assert.Equal(original.SourceText, viewModel.Items[0].SourceText);
        Assert.Equal(original.Result.MainTranslation, viewModel.Items[0].MainTranslation);
        Assert.Equal("apply", viewModel.Items[1].SourceText);
        Assert.Equal("להגיש מועמדות", viewModel.Items[1].MainTranslation);
        var persisted = await history.LoadAsync();
        Assert.Equal(2, persisted.Count);
        Assert.Equal("apply", persisted.OrderBy(entry => entry.CreatedAt).Last().SourceText);
    }

    [Fact]
    public async Task LookupUsesDedicatedEnglishToHebrewOperationRegardlessOfAppMode()
    {
        var service = new ControlledTranslationService();
        var settings = new AppSettings { LanguageMode = TranslationLanguageMode.HebrewToEnglish };
        var viewModel = await CreateConfiguredViewModelAsync(service, settings);

        await viewModel.TranslateEnglishWordInContextCommand.ExecuteAsync(
            new ContextualWordTranslationRequest(
                "apply",
                "I want to apply for a job."));

        Assert.Equal(1, service.ContextualCallCount);
        Assert.Equal(0, service.OrdinaryCallCount);
        var result = Assert.Single(viewModel.Items);
        Assert.Equal("en", result.Entry.Result.DetectedLanguage);
        Assert.Equal("he", result.Entry.Result.TargetLanguage);
    }

    [Fact]
    public async Task BusyLookupRejectsRapidRepeatAndNormalSendSupersedesWithoutStaleAppend()
    {
        var service = new ControlledTranslationService { BlockContextual = true };
        var viewModel = CreateViewModel(service);
        var request = new ContextualWordTranslationRequest(
            "apply",
            "I want to apply for a job.");

        var lookup = viewModel.TranslateEnglishWordInContextCommand.ExecuteAsync(request);
        await service.ContextualStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(viewModel.IsTranslating);
        Assert.False(viewModel.TranslateEnglishWordInContextCommand.CanExecute(request));

        await viewModel.TranslateEnglishWordInContextCommand.ExecuteAsync(request);
        Assert.Equal(1, service.ContextualCallCount);

        viewModel.InputText = "New sentence";
        var send = viewModel.SendCommand.ExecuteAsync(null);
        await service.ContextualCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.WhenAll(lookup, send);

        Assert.False(viewModel.IsTranslating);
        Assert.Equal(1, service.OrdinaryCallCount);
        var item = Assert.Single(viewModel.Items);
        Assert.Equal("New sentence", item.SourceText);
        Assert.Equal("תרגום רגיל", item.MainTranslation);
    }

    [Fact]
    public async Task ActiveSentenceTranslationCausesWordLookupToBeIgnored()
    {
        var service = new ControlledTranslationService { BlockOrdinary = true };
        var viewModel = CreateViewModel(service);
        viewModel.InputText = "Sentence";

        var send = viewModel.SendCommand.ExecuteAsync(null);
        await service.OrdinaryStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var request = new ContextualWordTranslationRequest("word", "Sentence with word");

        Assert.False(viewModel.TranslateEnglishWordInContextCommand.CanExecute(request));
        await viewModel.TranslateEnglishWordInContextCommand.ExecuteAsync(request);
        Assert.Equal(0, service.ContextualCallCount);

        service.CompleteOrdinary();
        await send;
    }

    [Fact]
    public async Task LookupAppliesConfiguredHistoryLimit()
    {
        var service = new ControlledTranslationService();
        var history = new InMemoryTranslationHistoryStore();
        for (var index = 0; index < 50; index++)
        {
            await history.AddOrUpdateAsync(CreateEntry($"source-{index}", $"result-{index}"));
        }

        var settings = new AppSettings { HistoryLimit = 50 };
        var viewModel = await CreateConfiguredViewModelAsync(service, settings, history);
        await viewModel.LoadHistoryAsync();

        await viewModel.TranslateEnglishWordInContextCommand.ExecuteAsync(
            new ContextualWordTranslationRequest("apply", "apply for a job"));

        Assert.Equal(50, viewModel.Items.Count);
        Assert.DoesNotContain(viewModel.Items, item => item.SourceText == "source-0");
        Assert.Equal("apply", viewModel.Items[^1].SourceText);
        Assert.Equal(50, (await history.LoadAsync()).Count);
    }

    [Fact]
    public async Task LookupFailureUsesExistingErrorPresentationAndDoesNotChangeComposer()
    {
        var service = new ControlledTranslationService
        {
            ContextualException = new TranslationServiceException(
                TranslationFailureKind.NetworkUnavailable,
                "No network connection. Check your connection and try again.")
        };
        var viewModel = CreateViewModel(service);
        viewModel.InputText = "draft";

        await viewModel.TranslateEnglishWordInContextCommand.ExecuteAsync(
            new ContextualWordTranslationRequest("apply", "apply for a job"));

        Assert.Equal("draft", viewModel.InputText);
        Assert.Empty(viewModel.Items);
        Assert.Equal(
            "No network connection. Check your connection and try again.",
            viewModel.ErrorMessage);
    }

    private static TranslationToolViewModel CreateViewModel(
        ITranslationService service,
        ITranslationHistoryStore? history = null) =>
        new(
            service,
            history ?? new InMemoryTranslationHistoryStore(),
            new NullClipboardService(),
            new TestOpenAiConfigurationProvider());

    private static async Task<TranslationToolViewModel> CreateConfiguredViewModelAsync(
        ITranslationService service,
        AppSettings settings,
        ITranslationHistoryStore? history = null)
    {
        var savedWords = new SavedWordsService(new InMemorySavedWordsStore());
        await savedWords.InitializeAsync();
        var frequentWords = new FrequentWordsService(new InMemoryFrequentWordsStore());
        await frequentWords.InitializeAsync();
        return new TranslationToolViewModel(
            service,
            history ?? new InMemoryTranslationHistoryStore(),
            new NullClipboardService(),
            savedWords,
            new NullSavedWordsExportService(),
            frequentWords,
            new TestOpenAiConfigurationProvider(),
            settings,
            new InMemoryAppSettingsStore(settings));
    }

    private static TranslationEntry CreateEntry(string source, string translation)
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        return new TranslationEntry
        {
            Id = Guid.NewGuid(),
            SourceText = source,
            Result = new TranslationResult(translation, "en", "Test", targetLanguage: "he"),
            CreatedAt = now,
            IsFavorite = false,
            UsageCount = 1,
            LastUsedAt = now
        };
    }

    private sealed class NullClipboardService : IClipboardService
    {
        public void SetText(string text)
        {
        }
    }

    private sealed class ControlledTranslationService : ITranslationService
    {
        private readonly TaskCompletionSource<TranslationResult> _ordinaryCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool BlockContextual { get; init; }
        public bool BlockOrdinary { get; init; }
        public Exception? ContextualException { get; init; }
        public int ContextualCallCount { get; private set; }
        public int OrdinaryCallCount { get; private set; }
        public string? ContextualWord { get; private set; }
        public string? Context { get; private set; }
        public TaskCompletionSource ContextualStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ContextualCancelled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource OrdinaryStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<TranslationResult> TranslateEnglishWordInContextAsync(
            string word,
            string context,
            CancellationToken cancellationToken)
        {
            ContextualCallCount++;
            ContextualWord = word;
            Context = context;
            ContextualStarted.TrySetResult();
            if (ContextualException is not null) throw ContextualException;
            if (BlockContextual)
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    ContextualCancelled.TrySetResult();
                    throw;
                }
            }

            return new TranslationResult(
                "להגיש מועמדות",
                "en",
                "Test",
                targetLanguage: "he");
        }

        public async Task<TranslationResult> TranslateAsync(
            string text,
            string? sourceLanguage,
            string targetLanguage,
            CancellationToken cancellationToken)
        {
            OrdinaryCallCount++;
            OrdinaryStarted.TrySetResult();
            if (BlockOrdinary)
            {
                return await _ordinaryCompletion.Task.WaitAsync(cancellationToken);
            }

            return new TranslationResult(
                "תרגום רגיל",
                sourceLanguage,
                "Test",
                targetLanguage: targetLanguage);
        }

        public void CompleteOrdinary() => _ordinaryCompletion.TrySetResult(
            new TranslationResult("תרגום רגיל", "en", "Test", targetLanguage: "he"));

        public Task<string?> TranslateAlternativeAsync(
            string sourceText,
            string sourceLanguage,
            string targetLanguage,
            string primaryTranslation,
            IReadOnlyList<string> existingAlternatives,
            CancellationToken cancellationToken) => Task.FromResult<string?>(null);
    }
}
