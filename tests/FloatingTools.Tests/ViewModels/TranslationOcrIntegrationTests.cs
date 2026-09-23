using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.ViewModels;

public sealed class TranslationOcrIntegrationTests
{
    [Fact]
    public async Task SuccessfulCapture_ReplacesComposerWithoutSubmittingTranslation()
    {
        var translation = new RecordingTranslationService();
        var capture = new StubScreenTextCaptureService(
            ScreenTextCaptureResult.Success("FIRST LINE SECOND LINE"));
        var context = await CreateContextAsync(translation, capture);
        context.ViewModel.InputText = "existing draft";

        await context.ViewModel.CaptureTextCommand.ExecuteAsync(null);

        Assert.Equal("FIRST LINE SECOND LINE", context.ViewModel.InputText);
        Assert.True(context.ViewModel.LastCaptureProducedText);
        Assert.Equal(0, translation.CallCount);
        Assert.Empty(context.ViewModel.Items);
        Assert.Empty(await context.History.LoadAsync());
        Assert.Empty(context.FrequentWords.Items);
        Assert.Empty(context.SavedWords.Items);
    }

    [Fact]
    public async Task ManuallyTypedMultilineText_IsNotOcrNormalized()
    {
        var context = await CreateContextAsync(
            new RecordingTranslationService(),
            new StubScreenTextCaptureService(ScreenTextCaptureResult.Cancelled()));

        context.ViewModel.InputText = "first line\nsecond   line";

        Assert.Equal("first line\nsecond   line", context.ViewModel.InputText);
    }

    [Theory]
    [InlineData(ScreenTextCaptureStatus.Cancelled)]
    [InlineData(ScreenTextCaptureStatus.NoText)]
    [InlineData(ScreenTextCaptureStatus.Failed)]
    public async Task Capture_RecordsItsOutcomeForExistingStatusConsumers(
        ScreenTextCaptureStatus status)
    {
        var context = await CreateContextAsync(
            new RecordingTranslationService(),
            new StubScreenTextCaptureService(new ScreenTextCaptureResult(status)));

        Assert.Null(context.ViewModel.LastCaptureStatus);

        await context.ViewModel.CaptureTextCommand.ExecuteAsync(null);

        Assert.Equal(status, context.ViewModel.LastCaptureStatus);
    }

    [Fact]
    public async Task SuccessfulCapture_RecordsSuccessOutcome()
    {
        var context = await CreateContextAsync(
            new RecordingTranslationService(),
            new StubScreenTextCaptureService(ScreenTextCaptureResult.Success("HELLO")));

        await context.ViewModel.CaptureTextCommand.ExecuteAsync(null);

        Assert.Equal(ScreenTextCaptureStatus.Success, context.ViewModel.LastCaptureStatus);
        Assert.True(context.ViewModel.LastCaptureProducedText);
    }

    [Theory]
    [InlineData(ScreenTextCaptureStatus.Success, "")]
    [InlineData(ScreenTextCaptureStatus.Success, "   \t")]
    [InlineData(ScreenTextCaptureStatus.NoText, null)]
    [InlineData(ScreenTextCaptureStatus.Failed, null)]
    [InlineData(ScreenTextCaptureStatus.Cancelled, null)]
    public async Task CaptureWithoutAppliedText_DoesNotReportProducedText(
        ScreenTextCaptureStatus status,
        string? text)
    {
        var context = await CreateContextAsync(
            new RecordingTranslationService(),
            new StubScreenTextCaptureService(new ScreenTextCaptureResult(status, text)));

        await context.ViewModel.CaptureTextCommand.ExecuteAsync(null);

        Assert.False(context.ViewModel.LastCaptureProducedText);
    }

    [Fact]
    public async Task CaptureException_DoesNotReportProducedText()
    {
        var context = await CreateContextAsync(
            new RecordingTranslationService(),
            new ThrowingScreenTextCaptureService());

        await context.ViewModel.CaptureTextCommand.ExecuteAsync(null);

        Assert.False(context.ViewModel.LastCaptureProducedText);
        Assert.Equal(ScreenTextCaptureStatus.Failed, context.ViewModel.LastCaptureStatus);
        Assert.Null(context.ViewModel.CaptureMessage);
    }

    [Fact]
    public async Task LaterCapture_ResetsProducedTextBeforeAwaitingItsResult()
    {
        var pending = new TaskCompletionSource<ScreenTextCaptureResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var capture = new SequenceScreenTextCaptureService(
            Task.FromResult(ScreenTextCaptureResult.Success("FIRST")),
            pending.Task);
        var context = await CreateContextAsync(new RecordingTranslationService(), capture);

        await context.ViewModel.CaptureTextCommand.ExecuteAsync(null);
        Assert.True(context.ViewModel.LastCaptureProducedText);

        var laterCapture = context.ViewModel.CaptureTextCommand.ExecuteAsync(null);
        Assert.False(context.ViewModel.LastCaptureProducedText);
        pending.SetResult(ScreenTextCaptureResult.Cancelled());
        await laterCapture;

        Assert.False(context.ViewModel.LastCaptureProducedText);
    }

    [Theory]
    [InlineData(ScreenTextCaptureStatus.Success, "CAPTURED", null)]
    [InlineData(ScreenTextCaptureStatus.NoText, null, "No text detected.")]
    [InlineData(ScreenTextCaptureStatus.Failed, null, null)]
    [InlineData(ScreenTextCaptureStatus.Cancelled, null, null)]
    public async Task Capture_ShowsReadingMessageOnlyWhileOperationIsPending(
        ScreenTextCaptureStatus status,
        string? text,
        string? expectedFinalMessage)
    {
        var pending = new TaskCompletionSource<ScreenTextCaptureResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var context = await CreateContextAsync(
            new RecordingTranslationService(),
            new SequenceScreenTextCaptureService(pending.Task));

        var capture = context.ViewModel.CaptureTextCommand.ExecuteAsync(null);

        Assert.Equal("Reading text…", context.ViewModel.CaptureMessage);

        pending.SetResult(new ScreenTextCaptureResult(status, text));
        await capture;

        Assert.Equal(expectedFinalMessage, context.ViewModel.CaptureMessage);
    }

    [Theory]
    [InlineData(ScreenTextCaptureStatus.NoText)]
    [InlineData(ScreenTextCaptureStatus.Failed)]
    public async Task NewCapture_ReplacesStaleCaptureOrErrorMessageWhilePending(
        ScreenTextCaptureStatus firstStatus)
    {
        var pending = new TaskCompletionSource<ScreenTextCaptureResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var captureService = new SequenceScreenTextCaptureService(
            Task.FromResult(new ScreenTextCaptureResult(firstStatus)),
            pending.Task);
        var context = await CreateContextAsync(
            new RecordingTranslationService(),
            captureService);

        await context.ViewModel.CaptureTextCommand.ExecuteAsync(null);
        var secondCapture = context.ViewModel.CaptureTextCommand.ExecuteAsync(null);

        Assert.Equal("Reading text…", context.ViewModel.CaptureMessage);
        Assert.Null(context.ViewModel.ErrorMessage);

        pending.SetResult(ScreenTextCaptureResult.Cancelled());
        await secondCapture;
        Assert.Null(context.ViewModel.CaptureMessage);
    }

    [Theory]
    [InlineData(ScreenTextCaptureStatus.Cancelled, null, null)]
    [InlineData(ScreenTextCaptureStatus.NoText, null, "No text detected.")]
    [InlineData(ScreenTextCaptureStatus.Failed, "Could not read text from the selected area.", null)]
    public async Task UnsuccessfulCapture_PreservesExistingComposer(
        ScreenTextCaptureStatus status,
        string? expectedError,
        string? expectedMessage)
    {
        var context = await CreateContextAsync(
            new RecordingTranslationService(),
            new StubScreenTextCaptureService(new ScreenTextCaptureResult(status)));
        context.ViewModel.InputText = "keep this";

        await context.ViewModel.CaptureTextCommand.ExecuteAsync(null);

        Assert.Equal("keep this", context.ViewModel.InputText);
        Assert.Equal(expectedError, context.ViewModel.ErrorMessage);
        Assert.Equal(expectedMessage, context.ViewModel.CaptureMessage);
    }

    [Fact]
    public async Task RejectedOcrResult_LeavesComposerAndAllFeatureStateUntouched()
    {
        var translation = new RecordingTranslationService();
        var context = await CreateContextAsync(
            translation,
            new StubScreenTextCaptureService(ScreenTextCaptureResult.NoText()));
        context.ViewModel.InputText = "existing composer text";

        await context.ViewModel.CaptureTextCommand.ExecuteAsync(null);

        Assert.Equal("existing composer text", context.ViewModel.InputText);
        Assert.Equal("No text detected.", context.ViewModel.CaptureMessage);
        Assert.Equal(0, translation.CallCount);
        Assert.Empty(context.ViewModel.Items);
        Assert.Empty(await context.History.LoadAsync());
        Assert.Empty(context.FrequentWords.Items);
        Assert.Empty(context.SavedWords.Items);
    }

    [Fact]
    public async Task CaptureOverThirtyWords_InsertsAllTextAndUsesExistingValidation()
    {
        var text = string.Join(' ', Enumerable.Range(1, 45).Select(index => $"word{index}"));
        var translation = new RecordingTranslationService();
        var context = await CreateContextAsync(
            translation,
            new StubScreenTextCaptureService(ScreenTextCaptureResult.Success(text)));

        await context.ViewModel.CaptureTextCommand.ExecuteAsync(null);

        Assert.Equal(text, context.ViewModel.InputText);
        Assert.Equal(TranslationInputLimits.MaximumWordCountMessage,
            context.ViewModel.InputValidationMessage);
        Assert.False(context.ViewModel.SendCommand.CanExecute(null));
        Assert.Equal(0, translation.CallCount);
    }

    [Fact]
    public async Task TranslationStillWorksAfterCapturedTextIsManuallySent()
    {
        var translation = new RecordingTranslationService();
        var context = await CreateContextAsync(
            translation,
            new StubScreenTextCaptureService(
                ScreenTextCaptureResult.Success("Hello from OCR")));
        await context.ViewModel.CaptureTextCommand.ExecuteAsync(null);

        await context.ViewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(1, translation.CallCount);
        Assert.Equal("Hello from OCR", translation.LastText);
        Assert.Single(context.ViewModel.Items);
    }

    [Fact]
    public async Task ShortcutCapture_ReturnsOnlyItsOwnAppliedTextAndRejectsOverlap()
    {
        var pending = new TaskCompletionSource<ScreenTextCaptureResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var translation = new RecordingTranslationService();
        var context = await CreateContextAsync(
            translation, new SequenceScreenTextCaptureService(pending.Task));

        var current = context.ViewModel.CaptureTextForShortcutAsync();
        context.ViewModel.InputText = "edited while OCR runs";
        var overlap = await context.ViewModel.CaptureTextForShortcutAsync();
        Assert.False(overlap.Started);

        pending.SetResult(ScreenTextCaptureResult.Success("New OCR text"));
        var outcome = await current;

        Assert.True(outcome.Started);
        Assert.True(outcome.SelectionCompleted);
        Assert.Equal(ScreenTextCaptureStatus.Success, outcome.Status);
        Assert.Equal("New OCR text", outcome.AppliedText);
        Assert.Equal("New OCR text", context.ViewModel.InputText);
        Assert.Equal(0, translation.CallCount);
    }

    [Theory]
    [InlineData(ScreenTextCaptureStatus.Cancelled)]
    [InlineData(ScreenTextCaptureStatus.NoText)]
    [InlineData(ScreenTextCaptureStatus.Failed)]
    public async Task ShortcutCapture_UnsuccessfulAttemptNeverReturnsStaleText(
        ScreenTextCaptureStatus status)
    {
        var context = await CreateContextAsync(
            new RecordingTranslationService(),
            new SequenceScreenTextCaptureService(
                Task.FromResult(ScreenTextCaptureResult.Success("First OCR")),
                Task.FromResult(new ScreenTextCaptureResult(status))));
        var first = await context.ViewModel.CaptureTextForShortcutAsync();
        var second = await context.ViewModel.CaptureTextForShortcutAsync();

        Assert.Equal("First OCR", first.AppliedText);
        Assert.Null(second.AppliedText);
        Assert.False(context.ViewModel.LastCaptureProducedText);
        Assert.Equal("First OCR", context.ViewModel.InputText);
    }

    [Fact]
    public async Task ShortcutCapture_FocusFailureDoesNotReturnTextForSending()
    {
        var context = await CreateContextAsync(
            new RecordingTranslationService(),
            new StubScreenTextCaptureService(ScreenTextCaptureResult.Success("OCR text")));
        context.ViewModel.ComposerFocusRequested += (_, _) =>
            throw new InvalidOperationException("Focus failed");

        var outcome = await context.ViewModel.CaptureTextForShortcutAsync();

        Assert.True(outcome.Started);
        Assert.Null(outcome.AppliedText);
        Assert.Equal(ScreenTextCaptureStatus.Failed, outcome.Status);
        Assert.False(context.ViewModel.LastCaptureProducedText);
    }

    private static async Task<TestContext> CreateContextAsync(
        RecordingTranslationService translationService,
        IScreenTextCaptureService captureService)
    {
        var history = new InMemoryTranslationHistoryStore();
        var savedWords = new SavedWordsService(new InMemorySavedWordsStore());
        await savedWords.InitializeAsync();
        var frequentWords = new FrequentWordsService(new InMemoryFrequentWordsStore());
        await frequentWords.InitializeAsync();
        var viewModel = new TranslationToolViewModel(
            translationService,
            history,
            new RecordingClipboardService(),
            savedWords,
            new NullSavedWordsExportService(),
            frequentWords,
            new TestOpenAiConfigurationProvider(),
            screenTextCaptureService: captureService);
        return new TestContext(viewModel, history, savedWords, frequentWords);
    }

    private sealed record TestContext(
        TranslationToolViewModel ViewModel,
        InMemoryTranslationHistoryStore History,
        SavedWordsService SavedWords,
        FrequentWordsService FrequentWords);

    private sealed class StubScreenTextCaptureService(ScreenTextCaptureResult result)
        : IScreenTextCaptureService
    {
        public Task<ScreenTextCaptureResult> CaptureTextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class ThrowingScreenTextCaptureService : IScreenTextCaptureService
    {
        public Task<ScreenTextCaptureResult> CaptureTextAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Capture failed.");
    }

    private sealed class SequenceScreenTextCaptureService(
        params Task<ScreenTextCaptureResult>[] results) : IScreenTextCaptureService
    {
        private int _nextResult;

        public Task<ScreenTextCaptureResult> CaptureTextAsync(
            CancellationToken cancellationToken = default) => results[_nextResult++];
    }

    private sealed class RecordingTranslationService : ITranslationService
    {
        public int CallCount { get; private set; }

        public string? LastText { get; private set; }

        public Task<TranslationResult> TranslateAsync(
            string text,
            string? sourceLanguage,
            string targetLanguage,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastText = text;
            return Task.FromResult(new TranslationResult(
                "תרגום",
                sourceLanguage,
                "Test",
                targetLanguage: targetLanguage));
        }

        public Task<string?> TranslateAlternativeAsync(
            string sourceText,
            string sourceLanguage,
            string targetLanguage,
            string primaryTranslation,
            IReadOnlyList<string> existingAlternatives,
            CancellationToken cancellationToken) => Task.FromResult<string?>(null);
    }

    private sealed class RecordingClipboardService : IClipboardService
    {
        public void SetText(string text)
        {
        }
    }
}
