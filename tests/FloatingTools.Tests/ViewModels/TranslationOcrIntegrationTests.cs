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
    public async Task Capture_RecordsItsOutcomeSoCallersCanTellCancellationApart(
        ScreenTextCaptureStatus status)
    {
        var context = await CreateContextAsync(
            new RecordingTranslationService(),
            new StubScreenTextCaptureService(new ScreenTextCaptureResult(status)));

        Assert.Null(context.ViewModel.LastCaptureStatus);

        await context.ViewModel.CaptureTextCommand.ExecuteAsync(null);

        // The global Ctrl+Alt+T handler reads this to decide whether to pull the
        // user into Translation; a cancelled capture must stay distinguishable.
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
