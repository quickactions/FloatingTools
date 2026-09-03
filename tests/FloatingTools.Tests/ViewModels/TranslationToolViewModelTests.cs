using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.Services.OpenAI;
using FloatingTools.App.ViewModels;
using System.Windows;

namespace FloatingTools.Tests.ViewModels;

public sealed class TranslationToolViewModelTests
{
    [Fact]
    public void InputTextChange_UpdatesComposerDirectionAndAlignment()
    {
        var viewModel = CreateViewModel();
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) =>
            changedProperties.Add(args.PropertyName);

        viewModel.InputText = "שלום";

        Assert.Equal(FlowDirection.RightToLeft, viewModel.InputFlowDirection);
        Assert.Equal(TextAlignment.Left, viewModel.InputTextAlignment);
        Assert.Contains(nameof(viewModel.InputFlowDirection), changedProperties);
        Assert.Contains(nameof(viewModel.InputTextAlignment), changedProperties);

        changedProperties.Clear();
        viewModel.InputText = "Hello";

        Assert.Equal(FlowDirection.LeftToRight, viewModel.InputFlowDirection);
        Assert.Equal(TextAlignment.Left, viewModel.InputTextAlignment);
        Assert.Contains(nameof(viewModel.InputFlowDirection), changedProperties);
        Assert.Contains(nameof(viewModel.InputTextAlignment), changedProperties);
    }

    [Fact]
    public async Task HebrewInput_IsSentFromHebrewToEnglish()
    {
        var service = new RecordingTranslationService("Task");
        var viewModel = CreateViewModel(service);
        viewModel.InputText = "משימה";

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal("he", service.SourceLanguage);
        Assert.Equal("en", service.TargetLanguage);
    }

    [Fact]
    public async Task EnglishInput_IsSentFromEnglishToHebrew()
    {
        var service = new RecordingTranslationService("משימה");
        var viewModel = CreateViewModel(service);
        viewModel.InputText = "Task 123!";

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal("en", service.SourceLanguage);
        Assert.Equal("he", service.TargetLanguage);
    }

    [Fact]
    public void EmptyInput_CannotBeSent()
    {
        var service = new RecordingTranslationService("unused");
        var viewModel = CreateViewModel(service);

        viewModel.InputText = "   ";

        Assert.False(viewModel.SendCommand.CanExecute(null));
        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public async Task MoreThanThirtyWords_IsNotSentAndKeepsComposerText()
    {
        var service = new RecordingTranslationService("unused");
        var viewModel = CreateViewModel(service);
        var text = string.Join(' ', Enumerable.Repeat("word", 31));
        viewModel.InputText = text;

        await viewModel.RequestTranslationAsync();

        Assert.Equal(0, service.CallCount);
        Assert.Equal(text, viewModel.InputText);
        Assert.Empty(viewModel.Items);
        Assert.Equal(
            TranslationInputLimits.MaximumWordCountMessage,
            viewModel.InputValidationMessage);
        Assert.False(viewModel.SendCommand.CanExecute(null));
    }

    [Fact]
    public void ReducingInputToThirtyWords_ClearsValidationAndEnablesSend()
    {
        var viewModel = CreateViewModel(new RecordingTranslationService("Result"));
        viewModel.InputText = string.Join(' ', Enumerable.Repeat("word", 31));
        Assert.NotNull(viewModel.InputValidationMessage);

        viewModel.InputText = string.Join(' ', Enumerable.Repeat("word", 30));

        Assert.Null(viewModel.InputValidationMessage);
        Assert.True(viewModel.SendCommand.CanExecute(null));
    }

    [Fact]
    public async Task NewSend_CancelsPreviousRequestAndShowsOnlyLatestResult()
    {
        var service = new SupersedingTranslationService();
        var viewModel = CreateViewModel(service);
        viewModel.InputText = "First";

        var firstExecution = viewModel.SendCommand.ExecuteAsync(null);
        await service.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(viewModel.IsTranslating);
        viewModel.InputText = "Second";
        Assert.True(viewModel.SendCommand.CanExecute(null));

        var secondExecution = viewModel.SendCommand.ExecuteAsync(null);
        await service.SecondStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await service.FirstCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));

        service.CompleteSecond(new TranslationResult("Latest", "en", "Test"));
        await Task.WhenAll(firstExecution, secondExecution);

        Assert.False(viewModel.IsTranslating);
        var item = Assert.Single(viewModel.Items);
        Assert.Equal("Second", item.SourceText);
        Assert.Equal("Latest", item.MainTranslation);
    }

    [Fact]
    public async Task SuccessfulTranslation_AddsEntryAndClearsInput()
    {
        var viewModel = CreateViewModel(
            new RecordingTranslationService("שלום"));
        viewModel.InputText = "Hello";

        await viewModel.SendCommand.ExecuteAsync(null);

        var item = Assert.Single(viewModel.Items);
        Assert.Equal("Hello", item.SourceText);
        Assert.Equal("שלום", item.MainTranslation);
        Assert.Equal(1, item.Entry.UsageCount);
        Assert.Empty(viewModel.InputText);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Theory]
    [InlineData("Hello", "שלום", "hello")]
    [InlineData("מאגר", "Repository", "repository")]
    public async Task SuccessfulSingleWord_RecordsFrequentWord(
        string source,
        string translation,
        string expectedKey)
    {
        var (viewModel, frequentWords, _) = await CreateViewModelWithFrequentWordsAsync(
            new RecordingTranslationService(translation));
        viewModel.InputText = source;

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(expectedKey, Assert.Single(frequentWords.Items).NormalizedSourceKey);
    }

    [Theory]
    [InlineData("hello world")]
    [InlineData("אני רוצה")]
    [InlineData("hello\nworld")]
    public async Task SuccessfulPhraseOrMultiline_DoesNotRecordFrequentWord(string source)
    {
        var (viewModel, frequentWords, _) = await CreateViewModelWithFrequentWordsAsync(
            new RecordingTranslationService("translation"));
        viewModel.InputText = source;

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Empty(frequentWords.Items);
    }

    [Fact]
    public async Task FailedOrAmbiguousTranslation_DoesNotRecordFrequentWord()
    {
        var (failedViewModel, failedWords, _) =
            await CreateViewModelWithFrequentWordsAsync(new UnconfiguredTranslationService());
        failedViewModel.InputText = "hello";
        await failedViewModel.SendCommand.ExecuteAsync(null);

        var ambiguousResult = new TranslationResult(
            string.Empty,
            "en",
            "Test",
            correctionStatus: TranslationCorrectionStatus.Ambiguous,
            targetLanguage: "he");
        var (ambiguousViewModel, ambiguousWords, _) =
            await CreateViewModelWithFrequentWordsAsync(
                new StaticResultTranslationService(ambiguousResult));
        ambiguousViewModel.InputText = "aello";
        await ambiguousViewModel.SendCommand.ExecuteAsync(null);

        Assert.Empty(failedWords.Items);
        Assert.Empty(ambiguousWords.Items);
    }

    [Fact]
    public async Task RepeatedSubmission_IncludingProviderCacheBehavior_IncrementsAgain()
    {
        var (viewModel, frequentWords, _) = await CreateViewModelWithFrequentWordsAsync(
            new RecordingTranslationService("שלום"));

        viewModel.InputText = "hello";
        await viewModel.SendCommand.ExecuteAsync(null);
        viewModel.InputText = "HELLO";
        await viewModel.SendCommand.ExecuteAsync(null);

        var item = Assert.Single(frequentWords.Items);
        Assert.Equal(2, item.UsageCount);
    }

    [Fact]
    public async Task AlternativeAndSaveActions_DoNotIncrementFrequency()
    {
        var service = new RecordingTranslationService("יכול");
        var (viewModel, frequentWords, savedWords) =
            await CreateViewModelWithFrequentWordsAsync(service);
        viewModel.InputText = "can";
        await viewModel.SendCommand.ExecuteAsync(null);
        var entry = Assert.Single(viewModel.Items);

        await entry.RequestAlternativeCommand.ExecuteAsync(null);
        await entry.ToggleFavoriteCommand.ExecuteAsync(null);

        Assert.Equal(1, Assert.Single(frequentWords.Items).UsageCount);
        Assert.Single(savedWords.Items);
    }

    [Fact]
    public async Task FailedTranslation_KeepsInputAndDoesNotAddEntry()
    {
        var viewModel = CreateViewModel(new UnconfiguredTranslationService());
        viewModel.InputText = "Hello";

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.Items);
        Assert.Equal("Hello", viewModel.InputText);
        Assert.Equal(
            "OpenAI API key is missing. Set OPENAI_API_KEY and restart FloatingTools.",
            viewModel.ErrorMessage);
    }

    [Fact]
    public async Task ClearInput_ClearsOnlyComposerAndCurrentError()
    {
        var service = new RecordingTranslationService("שלום");
        var viewModel = CreateViewModel(service);
        viewModel.InputText = "Hello";
        await viewModel.SendCommand.ExecuteAsync(null);

        viewModel.InputText = "Next";
        service.Exception = new InvalidOperationException();
        await viewModel.SendCommand.ExecuteAsync(null);
        Assert.NotNull(viewModel.ErrorMessage);

        viewModel.ClearInputCommand.Execute(null);

        Assert.Empty(viewModel.InputText);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Single(viewModel.Items);
    }

    [Fact]
    public void ClearHistory_ClearsOnlyItemsAndPreservesComposerState()
    {
        var viewModel = CreateViewModel();
        var entry = CreateEntry("Hello", "שלום");
        viewModel.Items.Add(new TranslationEntryViewModel(
            entry,
            new RecordingClipboardService(),
            new InMemoryTranslationHistoryStore()));
        viewModel.InputText = "Keep this text";
        viewModel.ErrorMessage = "Keep this error";
        viewModel.ToggleAppMenuCommand.Execute(null);

        viewModel.ClearHistoryCommand.Execute(null);

        Assert.Empty(viewModel.Items);
        Assert.Equal("Keep this text", viewModel.InputText);
        Assert.Equal("Keep this error", viewModel.ErrorMessage);
        Assert.True(viewModel.IsAppMenuOpen);
    }

    [Fact]
    public void ClearHistory_IsDisabledWhenFeedIsEmpty()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.ClearHistoryCommand.CanExecute(null));

        var entry = CreateEntry("Hello", "שלום");
        viewModel.Items.Add(new TranslationEntryViewModel(
            entry,
            new RecordingClipboardService(),
            new InMemoryTranslationHistoryStore()));

        Assert.True(viewModel.ClearHistoryCommand.CanExecute(null));

        viewModel.ClearHistoryCommand.Execute(null);

        Assert.False(viewModel.ClearHistoryCommand.CanExecute(null));
    }

    [Fact]
    public void Constructor_RequiresExplicitOpenAiConfigurationProvider()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new TranslationToolViewModel(
                new UnconfiguredTranslationService(),
                new InMemoryTranslationHistoryStore(),
                new RecordingClipboardService(),
                null!));

        Assert.Equal("configurationProvider", exception.ParamName);
    }

    [Fact]
    public void TranslationEntry_ResolvesSourceAndTranslationDirectionsIndependently()
    {
        var entry = CreateEntry("שלום", "Hello");
        var viewModel = new TranslationEntryViewModel(
            entry,
            new RecordingClipboardService(),
            new InMemoryTranslationHistoryStore());

        Assert.Equal(FlowDirection.RightToLeft, viewModel.SourceFlowDirection);
        Assert.Equal(TextAlignment.Left, viewModel.SourceTextAlignment);
        Assert.Equal(
            FlowDirection.LeftToRight,
            viewModel.ResultFlowDirection);
        Assert.Equal(TextAlignment.Left, viewModel.ResultTextAlignment);
    }

    [Fact]
    public void TranslationEntry_ShowsCorrectionSeparatelyAndKeepsOriginalSource()
    {
        var now = DateTimeOffset.UtcNow;
        var entry = new TranslationEntry
        {
            Id = Guid.NewGuid(),
            SourceText = "I realy like this",
            Result = new TranslationResult(
                "אני באמת אוהב את זה",
                detectedLanguage: "en",
                provider: "Test",
                correctedSourceText: "I really like this",
                correctionStatus: TranslationCorrectionStatus.Confident,
                targetLanguage: "he"),
            CreatedAt = now,
            IsFavorite = false,
            UsageCount = 1,
            LastUsedAt = now
        };
        var viewModel = new TranslationEntryViewModel(
            entry,
            new RecordingClipboardService(),
            new InMemoryTranslationHistoryStore());

        Assert.Equal("I realy like this", viewModel.SourceText);
        Assert.True(viewModel.HasCorrection);
        Assert.Equal(
            "Corrected: I really like this",
            viewModel.CorrectedSourceDisplayText);
        Assert.Equal("אני באמת אוהב את זה", viewModel.MainTranslation);
        Assert.Equal("en", entry.Result.DetectedLanguage);
    }

    [Fact]
    public void TranslationEntry_NoCorrection_HidesCorrectionLine()
    {
        var entry = CreateEntry("Hello", "שלום");
        var viewModel = new TranslationEntryViewModel(
            entry,
            new RecordingClipboardService(),
            new InMemoryTranslationHistoryStore());

        Assert.False(viewModel.HasCorrection);
        Assert.Equal(string.Empty, viewModel.CorrectedSourceDisplayText);
        Assert.False(viewModel.IsAmbiguous);
        Assert.True(viewModel.HasTranslation);
    }

    [Fact]
    public void TranslationEntry_AmbiguousInput_ShowsMessageWithoutTranslation()
    {
        var now = DateTimeOffset.UtcNow;
        var entry = new TranslationEntry
        {
            Id = Guid.NewGuid(),
            SourceText = "aello",
            Result = new TranslationResult(
                string.Empty,
                detectedLanguage: "en",
                provider: "Test",
                correctionStatus: TranslationCorrectionStatus.Ambiguous,
                targetLanguage: "he"),
            CreatedAt = now,
            IsFavorite = false,
            UsageCount = 1,
            LastUsedAt = now
        };
        var viewModel = new TranslationEntryViewModel(
            entry,
            new RecordingClipboardService(),
            new InMemoryTranslationHistoryStore());

        Assert.Equal("aello", viewModel.SourceText);
        Assert.True(viewModel.IsAmbiguous);
        Assert.False(viewModel.HasCorrection);
        Assert.False(viewModel.HasTranslation);
        Assert.Equal(
            "Could not identify the intended word.",
            viewModel.AmbiguousMessage);
        Assert.False(viewModel.CopyCommand.CanExecute(null));
    }

    [Fact]
    public async Task Send_PreservesOriginalSourceTextExactly()
    {
        var service = new RecordingTranslationService("שלום");
        var viewModel = CreateViewModel(service);
        viewModel.InputText = "  helo  ";

        await viewModel.SendCommand.ExecuteAsync(null);

        var item = Assert.Single(viewModel.Items);
        Assert.Equal("  helo  ", service.Text);
        Assert.Equal("  helo  ", item.SourceText);
    }

    [Fact]
    public void AppMenu_TogglesOpenAndClosed()
    {
        var viewModel = CreateViewModel();

        viewModel.ToggleAppMenuCommand.Execute(null);
        Assert.True(viewModel.IsAppMenuOpen);

        viewModel.ToggleAppMenuCommand.Execute(null);
        Assert.False(viewModel.IsAppMenuOpen);
    }

    [Fact]
    public void SavedWordsNavigation_BackToFeedPreservesFeedAndComposerState()
    {
        var viewModel = CreateViewModel();
        viewModel.Items.Add(new TranslationEntryViewModel(
            CreateEntry("Repository", "מאגר"),
            new RecordingClipboardService(),
            new InMemoryTranslationHistoryStore()));
        viewModel.InputText = "unfinished input";
        viewModel.ToggleAppMenuCommand.Execute(null);

        viewModel.OpenSavedWordsCommand.Execute(null);

        Assert.True(viewModel.IsSavedWordsPage);
        Assert.False(viewModel.IsFeedPage);
        Assert.False(viewModel.IsAppMenuOpen);
        viewModel.BackToFeedCommand.Execute(null);

        Assert.False(viewModel.IsSavedWordsPage);
        Assert.True(viewModel.IsFeedPage);
        Assert.False(viewModel.IsAppMenuOpen);
        Assert.Single(viewModel.Items);
        Assert.Equal("unfinished input", viewModel.InputText);
    }

    [Fact]
    public void BackToFeed_ClosesMenuEvenIfSubpageStateWasReopened()
    {
        var viewModel = CreateViewModel();
        viewModel.OpenSavedWordsCommand.Execute(null);
        viewModel.ToggleAppMenuCommand.Execute(null);
        Assert.True(viewModel.IsAppMenuOpen);

        viewModel.BackToFeedCommand.Execute(null);

        Assert.True(viewModel.IsFeedPage);
        Assert.False(viewModel.IsAppMenuOpen);
    }

    [Fact]
    public void FrequentWordsNavigation_ClosesMenuAndPreservesFeedAndComposer()
    {
        var viewModel = CreateViewModel();
        viewModel.Items.Add(new TranslationEntryViewModel(
            CreateEntry("Repository", "מאגר"),
            new RecordingClipboardService(),
            new InMemoryTranslationHistoryStore()));
        viewModel.InputText = "unfinished input";
        viewModel.ToggleAppMenuCommand.Execute(null);

        viewModel.OpenFrequentWordsCommand.Execute(null);

        Assert.True(viewModel.IsFrequentWordsPage);
        Assert.False(viewModel.IsAppMenuOpen);
        viewModel.ToggleAppMenuCommand.Execute(null);
        viewModel.BackToFeedCommand.Execute(null);
        Assert.True(viewModel.IsFeedPage);
        Assert.False(viewModel.IsAppMenuOpen);
        Assert.Single(viewModel.Items);
        Assert.Equal("unfinished input", viewModel.InputText);
    }

    [Fact]
    public async Task OpeningEntryActions_ClosesPreviouslyExpandedEntry()
    {
        var service = new RecordingTranslationService("ראשון");
        var viewModel = CreateViewModel(service);
        viewModel.InputText = "First";
        await viewModel.SendCommand.ExecuteAsync(null);
        service.Result = "שני";
        viewModel.InputText = "Second";
        await viewModel.SendCommand.ExecuteAsync(null);
        var firstViewModel = viewModel.Items[0];
        var secondViewModel = viewModel.Items[1];

        firstViewModel.ToggleActionsCommand.Execute(null);
        Assert.True(firstViewModel.IsExpanded);
        Assert.Same(firstViewModel, viewModel.ActiveExpandedEntry);

        secondViewModel.ToggleActionsCommand.Execute(null);
        Assert.False(firstViewModel.IsExpanded);
        Assert.True(secondViewModel.IsExpanded);
        Assert.Same(secondViewModel, viewModel.ActiveExpandedEntry);

        secondViewModel.ToggleActionsCommand.Execute(null);
        Assert.False(secondViewModel.IsExpanded);
        Assert.Null(viewModel.ActiveExpandedEntry);
    }

    [Fact]
    public async Task ExpandingAnEntry_RequestsVisibilityOnlyAfterExpansion()
    {
        var service = new RecordingTranslationService("ראשון");
        var viewModel = CreateViewModel(service);
        viewModel.InputText = "First";
        await viewModel.SendCommand.ExecuteAsync(null);
        var entry = viewModel.Items[0];
        Assert.Equal(0, entry.RevealRequestVersion);

        entry.ToggleActionsCommand.Execute(null);

        Assert.True(entry.IsExpanded);
        Assert.Equal(1, entry.RevealRequestVersion);
    }

    [Fact]
    public async Task CollapsingAnEntry_DoesNotRequestVisibility()
    {
        var service = new RecordingTranslationService("ראשון");
        var viewModel = CreateViewModel(service);
        viewModel.InputText = "First";
        await viewModel.SendCommand.ExecuteAsync(null);
        var entry = viewModel.Items[0];
        entry.ToggleActionsCommand.Execute(null);
        Assert.Equal(1, entry.RevealRequestVersion);

        entry.ToggleActionsCommand.Execute(null);

        Assert.False(entry.IsExpanded);
        Assert.Equal(1, entry.RevealRequestVersion);
    }

    [Fact]
    public async Task ExpandingASecondEntry_RequestsVisibilityForTheNewlyExpandedEntryOnly()
    {
        var service = new RecordingTranslationService("ראשון");
        var viewModel = CreateViewModel(service);
        viewModel.InputText = "First";
        await viewModel.SendCommand.ExecuteAsync(null);
        service.Result = "שני";
        viewModel.InputText = "Second";
        await viewModel.SendCommand.ExecuteAsync(null);
        var firstViewModel = viewModel.Items[0];
        var secondViewModel = viewModel.Items[1];
        firstViewModel.ToggleActionsCommand.Execute(null);
        Assert.Equal(1, firstViewModel.RevealRequestVersion);

        secondViewModel.ToggleActionsCommand.Execute(null);

        Assert.False(firstViewModel.IsExpanded);
        Assert.Equal(1, firstViewModel.RevealRequestVersion);
        Assert.True(secondViewModel.IsExpanded);
        Assert.Equal(1, secondViewModel.RevealRequestVersion);
    }

    [Fact]
    public async Task Save_ChangesOnlySelectedEntry()
    {
        var store = new InMemoryTranslationHistoryStore();
        var clipboard = new RecordingClipboardService();
        var savedWords = await CreateSavedWordsServiceAsync();
        var first = CreateEntry("One", "אחד");
        var second = CreateEntry("Two", "שתיים");
        var firstViewModel = new TranslationEntryViewModel(
            first,
            clipboard,
            store,
            savedWordsService: savedWords);
        var secondViewModel = new TranslationEntryViewModel(
            second,
            clipboard,
            store,
            savedWordsService: savedWords);

        await firstViewModel.ToggleFavoriteCommand.ExecuteAsync(null);

        Assert.True(firstViewModel.IsFavorite);
        Assert.True(first.IsFavorite);
        Assert.False(secondViewModel.IsFavorite);
        Assert.False(second.IsFavorite);
        Assert.Single(savedWords.Items);
    }

    [Fact]
    public async Task ExistingSavedPair_IsReflectedAndCannotBeSavedAgain()
    {
        var savedWords = await CreateSavedWordsServiceAsync();
        await savedWords.AddAsync("Repository", "מאגר", "en", "he");
        var viewModel = new TranslationEntryViewModel(
            CreateEntry("Repository", "מאגר"),
            new RecordingClipboardService(),
            new InMemoryTranslationHistoryStore(),
            savedWordsService: savedWords);

        Assert.True(viewModel.IsFavorite);
        Assert.Equal("Saved", viewModel.FavoriteActionLabel);
        Assert.False(viewModel.ToggleFavoriteCommand.CanExecute(null));
        await viewModel.ToggleFavoriteCommand.ExecuteAsync(null);
        Assert.Single(savedWords.Items);
    }

    [Fact]
    public async Task Save_UsesOriginalSourceAndPrimaryOnly()
    {
        var savedWords = await CreateSavedWordsServiceAsync();
        var now = DateTimeOffset.UtcNow;
        var entry = new TranslationEntry
        {
            Id = Guid.NewGuid(),
            SourceText = "I realy like this",
            Result = new TranslationResult(
                "אני באמת אוהב את זה",
                detectedLanguage: "en",
                alternativeTranslations: ["אני ממש אוהב את זה"],
                correctedSourceText: "I really like this",
                correctionStatus: TranslationCorrectionStatus.Confident,
                targetLanguage: "he"),
            CreatedAt = now,
            LastUsedAt = now,
            UsageCount = 1
        };
        var viewModel = new TranslationEntryViewModel(
            entry,
            new RecordingClipboardService(),
            new InMemoryTranslationHistoryStore(),
            savedWordsService: savedWords);

        await viewModel.ToggleFavoriteCommand.ExecuteAsync(null);

        var saved = Assert.Single(savedWords.Items);
        Assert.Equal("I realy like this", saved.SourceText);
        Assert.Equal("אני באמת אוהב את זה", saved.PrimaryTranslation);
        Assert.DoesNotContain("I really like this", saved.SourceText);
        Assert.DoesNotContain("אני ממש אוהב את זה", saved.PrimaryTranslation);
    }

    [Fact]
    public async Task RemovingSavedWord_UpdatesExistingFeedEntrySavedState()
    {
        var savedWords = await CreateSavedWordsServiceAsync();
        var entry = CreateEntry("Repository", "מאגר");
        var viewModel = new TranslationEntryViewModel(
            entry,
            new RecordingClipboardService(),
            new InMemoryTranslationHistoryStore(),
            savedWordsService: savedWords);
        await viewModel.ToggleFavoriteCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsFavorite);

        await savedWords.RemoveAsync(Assert.Single(savedWords.Items).Id);

        Assert.False(viewModel.IsFavorite);
        Assert.False(entry.IsFavorite);
        Assert.True(viewModel.ToggleFavoriteCommand.CanExecute(null));
    }

    [Fact]
    public async Task SavedWordsLimit_ShowsMessageAndDoesNotSaveEntry()
    {
        var savedWords = await CreateSavedWordsServiceAsync();
        for (var index = 0; index < ISavedWordsService.MaximumItemCount; index++)
        {
            await savedWords.AddAsync($"s{index}", $"t{index}", "en", "he");
        }

        string? message = null;
        var viewModel = new TranslationEntryViewModel(
            CreateEntry("new", "חדש"),
            new RecordingClipboardService(),
            new InMemoryTranslationHistoryStore(),
            value => message = value,
            savedWordsService: savedWords);

        await viewModel.ToggleFavoriteCommand.ExecuteAsync(null);

        Assert.Equal("Saved words limit reached (200).", message);
        Assert.False(viewModel.IsFavorite);
        Assert.Equal(ISavedWordsService.MaximumItemCount, savedWords.Items.Count);
    }

    [Fact]
    public void Copy_CopiesOnlyMainTranslation()
    {
        var clipboard = new RecordingClipboardService();
        var entry = CreateEntry("Hello", "שלום");
        var viewModel = new TranslationEntryViewModel(
            entry,
            clipboard,
            new InMemoryTranslationHistoryStore());

        viewModel.CopyCommand.Execute(null);

        Assert.Equal("שלום", clipboard.Text);
        Assert.Equal("Copied", viewModel.CopyFeedback);
    }

    [Fact]
    public async Task Alternative_LoadsIntoExistingEntryWithoutCreatingHistoryEntry()
    {
        var store = new InMemoryTranslationHistoryStore();
        var service = new AlternativeTranslationService((_, _, _, _, _, _) =>
            Task.FromResult<string?>("פחית"));
        var viewModel = new TranslationEntryViewModel(
            CreateEntry("can", "יכול"),
            new RecordingClipboardService(),
            store,
            translationService: service);

        await viewModel.RequestAlternativeCommand.ExecuteAsync(null);

        Assert.Equal("פחית", Assert.Single(viewModel.Alternatives).Text);
        Assert.Null(viewModel.AlternativeMessage);
        Assert.Equal("יכול", viewModel.MainTranslation);
        Assert.Empty(await store.LoadAsync());
    }

    [Fact]
    public async Task LoadingAnAlternative_RequestsVisibilityAfterItFinishesLoading()
    {
        var service = new AlternativeTranslationService((_, _, _, _, _, _) =>
            Task.FromResult<string?>("פחית"));
        var viewModel = new TranslationEntryViewModel(
            CreateEntry("can", "יכול"),
            new RecordingClipboardService(),
            new InMemoryTranslationHistoryStore(),
            translationService: service);
        Assert.Equal(0, viewModel.RevealRequestVersion);

        await viewModel.RequestAlternativeCommand.ExecuteAsync(null);

        Assert.Equal(1, viewModel.RevealRequestVersion);
    }

    [Fact]
    public async Task AlternativeFailure_LeavesOriginalEntryIntactAndAllowsRetry()
    {
        var service = new AlternativeTranslationService((_, _, _, _, _, _) =>
            Task.FromException<string?>(new TranslationServiceException(
                TranslationFailureKind.ProviderUnavailable,
                "Unavailable")));
        var viewModel = new TranslationEntryViewModel(
            CreateEntry("I got it", "הבנתי"),
            new RecordingClipboardService(),
            new InMemoryTranslationHistoryStore(),
            translationService: service);

        await viewModel.RequestAlternativeCommand.ExecuteAsync(null);

        Assert.Equal("I got it", viewModel.SourceText);
        Assert.Equal("הבנתי", viewModel.MainTranslation);
        Assert.Empty(viewModel.Alternatives);
        Assert.Equal(
            "Could not load an alternative. Try again.",
            viewModel.AlternativeMessage);
        Assert.True(viewModel.RequestAlternativeCommand.CanExecute(null));
    }

    [Fact]
    public async Task NoUsefulAlternative_ShowsMessageAndDoesNotCreateEmptyLine()
    {
        var service = new AlternativeTranslationService((_, _, _, _, _, _) =>
            Task.FromResult<string?>(null));
        var viewModel = new TranslationEntryViewModel(
            CreateEntry("Hello", "שלום"),
            new RecordingClipboardService(),
            new InMemoryTranslationHistoryStore(),
            translationService: service);

        await viewModel.RequestAlternativeCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.Alternatives);
        Assert.Equal("No more alternatives.", viewModel.AlternativeMessage);
        Assert.True(viewModel.HasAlternativeMessage);
        Assert.True(viewModel.IsAlternativeExhausted);
        Assert.False(viewModel.RequestAlternativeCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("אפשרות", FlowDirection.RightToLeft, TextAlignment.Left)]
    [InlineData("Option", FlowDirection.LeftToRight, TextAlignment.Left)]
    public async Task Alternative_UsesItsOwnTextDirection(
        string alternative,
        FlowDirection expectedFlowDirection,
        TextAlignment expectedAlignment)
    {
        var service = new AlternativeTranslationService((_, _, _, _, _, _) =>
            Task.FromResult<string?>(alternative));
        var viewModel = new TranslationEntryViewModel(
            CreateEntry("source", "primary"),
            new RecordingClipboardService(),
            new InMemoryTranslationHistoryStore(),
            translationService: service);

        await viewModel.RequestAlternativeCommand.ExecuteAsync(null);

        var item = Assert.Single(viewModel.Alternatives);
        Assert.Equal(expectedFlowDirection, item.FlowDirection);
        Assert.Equal(expectedAlignment, item.TextAlignment);
    }

    [Fact]
    public async Task RepeatedAlternativeClickWhileLoading_SendsOnlyOneRequest()
    {
        var completion = new TaskCompletionSource<string?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new AlternativeTranslationService((_, _, _, _, _, _) =>
        {
            started.TrySetResult();
            return completion.Task;
        });
        var viewModel = new TranslationEntryViewModel(
            CreateEntry("I got it", "הבנתי"),
            new RecordingClipboardService(),
            new InMemoryTranslationHistoryStore(),
            translationService: service);

        var firstRequest = viewModel.RequestAlternativeCommand.ExecuteAsync(null);
        await started.Task;
        var repeatedRequest = viewModel.RequestAlternativeCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsAlternativeLoading);
        Assert.False(viewModel.RequestAlternativeCommand.CanExecute(null));
        Assert.Equal(1, service.AlternativeCallCount);
        completion.SetResult("קלטתי");
        await Task.WhenAll(firstRequest, repeatedRequest);
        Assert.Equal(1, service.AlternativeCallCount);
    }

    [Fact]
    public async Task Alternative_AllowsThreeSequentialResultsAndBlocksFourthRequest()
    {
        var results = new Queue<string?>(["first", "second", "third", "fourth"]);
        var service = new AlternativeTranslationService((_, _, _, _, _, _) =>
            Task.FromResult(results.Dequeue()));
        var viewModel = new TranslationEntryViewModel(
            CreateEntry("source", "primary"),
            new RecordingClipboardService(),
            new InMemoryTranslationHistoryStore(),
            translationService: service);

        await viewModel.RequestAlternativeCommand.ExecuteAsync(null);
        await viewModel.RequestAlternativeCommand.ExecuteAsync(null);
        await viewModel.RequestAlternativeCommand.ExecuteAsync(null);

        Assert.Equal(
            ["first", "second", "third"],
            viewModel.Alternatives.Select(item => item.Text));
        Assert.True(viewModel.IsAlternativeExhausted);
        Assert.False(viewModel.RequestAlternativeCommand.CanExecute(null));
        await viewModel.RequestAlternativeCommand.ExecuteAsync(null);
        Assert.Equal(3, service.AlternativeCallCount);
    }

    [Fact]
    public async Task Alternative_SubsequentRequestReceivesAllExistingAlternatives()
    {
        var results = new Queue<string?>(["first", "second"]);
        var service = new AlternativeTranslationService((_, _, _, _, _, _) =>
            Task.FromResult(results.Dequeue()));
        var viewModel = new TranslationEntryViewModel(
            CreateEntry("source", "primary"),
            new RecordingClipboardService(),
            new InMemoryTranslationHistoryStore(),
            translationService: service);

        await viewModel.RequestAlternativeCommand.ExecuteAsync(null);
        await viewModel.RequestAlternativeCommand.ExecuteAsync(null);

        Assert.Empty(service.ExistingAlternativesRequests[0]);
        Assert.Equal(["first"], service.ExistingAlternativesRequests[1]);
    }

    [Fact]
    public async Task Alternative_FailureDoesNotExhaustEntryAndRetryCanSucceed()
    {
        var attempt = 0;
        var service = new AlternativeTranslationService((_, _, _, _, _, _) =>
            ++attempt == 1
                ? Task.FromException<string?>(new HttpRequestException())
                : Task.FromResult<string?>("retry result"));
        var viewModel = new TranslationEntryViewModel(
            CreateEntry("source", "primary"),
            new RecordingClipboardService(),
            new InMemoryTranslationHistoryStore(),
            translationService: service);

        await viewModel.RequestAlternativeCommand.ExecuteAsync(null);
        Assert.False(viewModel.IsAlternativeExhausted);
        Assert.True(viewModel.RequestAlternativeCommand.CanExecute(null));

        await viewModel.RequestAlternativeCommand.ExecuteAsync(null);

        Assert.Equal("retry result", Assert.Single(viewModel.Alternatives).Text);
        Assert.Equal(2, service.AlternativeCallCount);
    }

    [Fact]
    public async Task Alternative_CollapseAndReopenPreservesExistingResults()
    {
        var service = new AlternativeTranslationService((_, _, _, _, _, _) =>
            Task.FromResult<string?>("result"));
        var viewModel = new TranslationEntryViewModel(
            CreateEntry("source", "primary"),
            new RecordingClipboardService(),
            new InMemoryTranslationHistoryStore(),
            translationService: service);

        await viewModel.RequestAlternativeCommand.ExecuteAsync(null);
        viewModel.ToggleActionsCommand.Execute(null);
        viewModel.ToggleActionsCommand.Execute(null);

        Assert.Equal("result", Assert.Single(viewModel.Alternatives).Text);
        Assert.Equal(1, service.AlternativeCallCount);
    }

    [Fact]
    public async Task SuccessfulTranslations_RemainInChronologicalInsertionOrder()
    {
        var service = new RecordingTranslationService("ראשון");
        var viewModel = CreateViewModel(service);
        viewModel.InputText = "First";
        await viewModel.SendCommand.ExecuteAsync(null);

        service.Result = "שני";
        viewModel.InputText = "Second";
        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Collection(
            viewModel.Items,
            first => Assert.Equal("First", first.SourceText),
            second => Assert.Equal("Second", second.SourceText));
    }

    [Fact]
    public async Task SettingsNavigation_ClosesMenuAndPreservesFeedAndComposer()
    {
        var history = new InMemoryTranslationHistoryStore();
        await history.AddOrUpdateAsync(CreateEntry("Hello", "שלום"));
        var viewModel = CreateViewModel(historyStore: history);
        await viewModel.LoadHistoryAsync();
        viewModel.InputText = "draft";
        viewModel.ToggleAppMenuCommand.Execute(null);

        viewModel.OpenSettingsCommand.Execute(null);

        Assert.True(viewModel.IsSettingsPage);
        Assert.False(viewModel.IsAppMenuOpen);
        viewModel.BackToFeedCommand.Execute(null);
        Assert.True(viewModel.IsFeedPage);
        Assert.False(viewModel.IsAppMenuOpen);
        Assert.Equal("draft", viewModel.InputText);
        Assert.Single(viewModel.Items);
    }

    [Theory]
    [InlineData(TranslationAssistantPage.Feed)]
    [InlineData(TranslationAssistantPage.SavedWords)]
    [InlineData(TranslationAssistantPage.FrequentWords)]
    [InlineData(TranslationAssistantPage.Settings)]
    public void SharedMenu_OpensAndClosesOnEveryInternalPage(
        TranslationAssistantPage page)
    {
        var viewModel = CreateViewModel();
        viewModel.CurrentPage = page;

        viewModel.ToggleAppMenuCommand.Execute(null);
        Assert.True(viewModel.IsAppMenuOpen);

        viewModel.ToggleAppMenuCommand.Execute(null);
        Assert.False(viewModel.IsAppMenuOpen);
        Assert.Equal(page, viewModel.CurrentPage);
    }

    [Fact]
    public void Menu_NavigatesDirectlyBetweenPagesAndPreservesPageState()
    {
        var viewModel = CreateViewModel();
        viewModel.InputText = "composer draft";
        viewModel.SavedWords.SearchQuery = "repository";
        viewModel.FrequentWords.ToggleSortCommand.Execute(null);
        var frequentSort = viewModel.FrequentWords.IsAscending;

        viewModel.OpenSavedWordsCommand.Execute(null);
        viewModel.ToggleAppMenuCommand.Execute(null);
        viewModel.OpenFrequentWordsCommand.Execute(null);

        Assert.True(viewModel.IsFrequentWordsPage);
        Assert.False(viewModel.IsAppMenuOpen);
        Assert.Equal("repository", viewModel.SavedWords.SearchQuery);
        Assert.Equal(frequentSort, viewModel.FrequentWords.IsAscending);
        Assert.Equal("composer draft", viewModel.InputText);

        viewModel.ToggleAppMenuCommand.Execute(null);
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.SelectedTranslationAiModel = OpenAiModelOptions.FastModel;
        viewModel.ToggleAppMenuCommand.Execute(null);
        viewModel.OpenSavedWordsCommand.Execute(null);

        Assert.True(viewModel.IsSavedWordsPage);
        Assert.False(viewModel.IsAppMenuOpen);
        Assert.Equal(OpenAiModelOptions.FastModel,
            viewModel.Settings.SelectedTranslationAiModel);
    }

    [Fact]
    public void SelectingCurrentPage_OnlyClosesMenuWithoutResettingState()
    {
        var viewModel = CreateViewModel();
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.UseAppCredentials = false;
        viewModel.Settings.BeginApiKeyEditCommand.Execute(null);
        viewModel.ToggleAppMenuCommand.Execute(null);

        viewModel.OpenSettingsCommand.Execute(null);

        Assert.True(viewModel.IsSettingsPage);
        Assert.False(viewModel.IsAppMenuOpen);
        Assert.True(viewModel.Settings.IsApiKeyEditing);
    }

    [Theory]
    [InlineData(TranslationAssistantPage.SavedWords)]
    [InlineData(TranslationAssistantPage.FrequentWords)]
    [InlineData(TranslationAssistantPage.Settings)]
    public void Back_FromInternalPageReturnsToFeedWithMenuClosed(
        TranslationAssistantPage page)
    {
        var viewModel = CreateViewModel();
        viewModel.CurrentPage = page;
        viewModel.ToggleAppMenuCommand.Execute(null);

        viewModel.BackToFeedCommand.Execute(null);

        Assert.True(viewModel.IsFeedPage);
        Assert.False(viewModel.IsAppMenuOpen);
    }

    [Theory]
    [InlineData(TranslationLanguageMode.HebrewToEnglish, "English", "he", "en")]
    [InlineData(TranslationLanguageMode.EnglishToHebrew, "עברית", "en", "he")]
    public async Task FixedLanguageMode_AffectsFutureTranslationRequests(
        TranslationLanguageMode mode,
        string input,
        string expectedSource,
        string expectedTarget)
    {
        var service = new RecordingTranslationService("result");
        var settings = new AppSettings { LanguageMode = mode };
        var viewModel = await CreateConfiguredViewModelAsync(service, settings);
        viewModel.InputText = input;

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(expectedSource, service.SourceLanguage);
        Assert.Equal(expectedTarget, service.TargetLanguage);
    }

    [Fact]
    public async Task LoweringHistoryLimit_TrimsOldestImmediatelyAndPreservesNewest()
    {
        var history = new InMemoryTranslationHistoryStore();
        for (var index = 0; index < 60; index++)
        {
            var timestamp = DateTimeOffset.UtcNow.AddMinutes(index);
            var entry = new TranslationEntry
            {
                Id = Guid.NewGuid(),
                SourceText = $"source-{index}",
                Result = new TranslationResult($"result-{index}"),
                CreatedAt = timestamp,
                LastUsedAt = timestamp,
                UsageCount = 1
            };
            await history.AddOrUpdateAsync(entry);
        }

        var settings = new AppSettings { HistoryLimit = 100 };
        var viewModel = await CreateConfiguredViewModelAsync(
            new RecordingTranslationService("result"), settings, history);
        await viewModel.LoadHistoryAsync();

        viewModel.Settings.SelectedHistoryLimit = 50;

        Assert.Equal(50, viewModel.Items.Count);
        Assert.Equal("source-10", viewModel.Items[0].SourceText);
        Assert.Equal("source-59", viewModel.Items[^1].SourceText);
        Assert.Equal(50, (await history.LoadAsync()).Count);
    }

    [Fact]
    public async Task ClearHistory_RemovesOnlyHistoryAndPreservesSettings()
    {
        var history = new InMemoryTranslationHistoryStore();
        await history.AddOrUpdateAsync(CreateEntry("Hello", "שלום"));
        var settings = new AppSettings
        {
            TranslationModel = OpenAiModelOptions.FastModel,
            HistoryLimit = 50
        };
        var viewModel = await CreateConfiguredViewModelAsync(
            new RecordingTranslationService("result"), settings, history);
        await viewModel.LoadHistoryAsync();

        viewModel.ClearHistoryCommand.Execute(null);

        Assert.Empty(viewModel.Items);
        Assert.Empty(await history.LoadAsync());
        Assert.Equal(OpenAiModelOptions.FastModel, settings.TranslationModel);
        Assert.Equal(50, settings.HistoryLimit);
    }

    private static TranslationToolViewModel CreateViewModel(
        ITranslationService? translationService = null,
        ITranslationHistoryStore? historyStore = null,
        IClipboardService? clipboardService = null) =>
        new(
            translationService ?? new UnconfiguredTranslationService(),
            historyStore ?? new InMemoryTranslationHistoryStore(),
            clipboardService ?? new RecordingClipboardService(),
            new TestOpenAiConfigurationProvider());

    private static async Task<SavedWordsService> CreateSavedWordsServiceAsync()
    {
        var service = new SavedWordsService(new InMemorySavedWordsStore());
        await service.InitializeAsync();
        return service;
    }

    private static async Task<(
        TranslationToolViewModel ViewModel,
        FrequentWordsService FrequentWords,
        SavedWordsService SavedWords)> CreateViewModelWithFrequentWordsAsync(
        ITranslationService translationService)
    {
        var frequentWords = new FrequentWordsService(new InMemoryFrequentWordsStore());
        await frequentWords.InitializeAsync();
        var savedWords = await CreateSavedWordsServiceAsync();
        var viewModel = new TranslationToolViewModel(
            translationService,
            new InMemoryTranslationHistoryStore(),
            new RecordingClipboardService(),
            savedWords,
            new NullSavedWordsExportService(),
            frequentWords,
            new TestOpenAiConfigurationProvider());
        return (viewModel, frequentWords, savedWords);
    }

    private static async Task<TranslationToolViewModel> CreateConfiguredViewModelAsync(
        ITranslationService translationService,
        AppSettings settings,
        ITranslationHistoryStore? historyStore = null)
    {
        var frequentWords = new FrequentWordsService(new InMemoryFrequentWordsStore());
        await frequentWords.InitializeAsync();
        var savedWords = await CreateSavedWordsServiceAsync();
        var keyStore = new InMemorySecureApiKeyStore();
        var configurationProvider = new TestOpenAiConfigurationProvider();
        return new TranslationToolViewModel(
            translationService,
            historyStore ?? new InMemoryTranslationHistoryStore(),
            new RecordingClipboardService(),
            savedWords,
            new NullSavedWordsExportService(),
            frequentWords,
            configurationProvider,
            settings,
            new InMemoryAppSettingsStore(settings),
            keyStore,
            new NullOpenAiConnectionTester());
    }

    private static TranslationEntry CreateEntry(string source, string result)
    {
        var now = DateTimeOffset.UtcNow;
        return new TranslationEntry
        {
            Id = Guid.NewGuid(),
            SourceText = source,
            Result = new TranslationResult(result),
            CreatedAt = now,
            IsFavorite = false,
            UsageCount = 1,
            LastUsedAt = now
        };
    }

    private sealed class RecordingClipboardService : IClipboardService
    {
        public string? Text { get; private set; }

        public void SetText(string text)
        {
            Text = text;
        }
    }

    private sealed class AlternativeTranslationService(
        Func<string, string, string, string, IReadOnlyList<string>, CancellationToken, Task<string?>> handler)
        : ITranslationService
    {
        public int AlternativeCallCount { get; private set; }

        public List<IReadOnlyList<string>> ExistingAlternativesRequests { get; } = [];

        public Task<TranslationResult> TranslateAsync(
            string text,
            string? sourceLanguage,
            string targetLanguage,
            CancellationToken cancellationToken) =>
            Task.FromException<TranslationResult>(new NotSupportedException());

        public Task<string?> TranslateAlternativeAsync(
            string sourceText,
            string sourceLanguage,
            string targetLanguage,
            string primaryTranslation,
            IReadOnlyList<string> existingAlternatives,
            CancellationToken cancellationToken)
        {
            AlternativeCallCount++;
            ExistingAlternativesRequests.Add(existingAlternatives.ToArray());
            return handler(
                sourceText,
                sourceLanguage,
                targetLanguage,
                primaryTranslation,
                existingAlternatives,
                cancellationToken);
        }
    }

    private sealed class RecordingTranslationService(string result)
        : ITranslationService
    {
        public int CallCount { get; private set; }

        public string? SourceLanguage { get; private set; }

        public string? TargetLanguage { get; private set; }

        public string? Text { get; private set; }

        public string Result { get; set; } = result;

        public Exception? Exception { get; set; }

        public Task<TranslationResult> TranslateAsync(
            string text,
            string? sourceLanguage,
            string targetLanguage,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Text = text;
            SourceLanguage = sourceLanguage;
            TargetLanguage = targetLanguage;

            if (Exception is not null)
            {
                return Task.FromException<TranslationResult>(Exception);
            }

            return Task.FromResult(
                new TranslationResult(Result, sourceLanguage, "Test"));
        }

        public Task<string?> TranslateAlternativeAsync(
            string sourceText,
            string sourceLanguage,
            string targetLanguage,
            string primaryTranslation,
            IReadOnlyList<string> existingAlternatives,
            CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }

    private sealed class StaticResultTranslationService(TranslationResult result)
        : ITranslationService
    {
        public Task<TranslationResult> TranslateAsync(
            string text,
            string? sourceLanguage,
            string targetLanguage,
            CancellationToken cancellationToken) => Task.FromResult(result);

        public Task<string?> TranslateAlternativeAsync(
            string sourceText,
            string sourceLanguage,
            string targetLanguage,
            string primaryTranslation,
            IReadOnlyList<string> existingAlternatives,
            CancellationToken cancellationToken) => Task.FromResult<string?>(null);
    }

    private sealed class SupersedingTranslationService : ITranslationService
    {
        private readonly TaskCompletionSource<TranslationResult> _secondCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource FirstStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SecondStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource FirstCancelled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _callCount;

        public async Task<TranslationResult> TranslateAsync(
            string text,
            string? sourceLanguage,
            string targetLanguage,
            CancellationToken cancellationToken)
        {
            var callNumber = Interlocked.Increment(ref _callCount);
            if (callNumber == 1)
            {
                FirstStarted.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    FirstCancelled.TrySetResult();
                    throw;
                }

                throw new InvalidOperationException("Unreachable.");
            }

            SecondStarted.TrySetResult();
            return await _secondCompletion.Task.WaitAsync(cancellationToken);
        }

        public void CompleteSecond(TranslationResult result)
        {
            _secondCompletion.TrySetResult(result);
        }

        public Task<string?> TranslateAlternativeAsync(
            string sourceText,
            string sourceLanguage,
            string targetLanguage,
            string primaryTranslation,
            IReadOnlyList<string> existingAlternatives,
            CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }
}
