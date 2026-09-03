using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;
using System.Windows;

namespace FloatingTools.Tests.ViewModels;

public sealed class FrequentWordsViewModelTests
{
    [Fact]
    public async Task ItemDirectionMapsHebrewAndEnglishToTheirPhysicalSides()
    {
        var (_, viewModel, _, _) = await CreateAsync(
        [
            CreateLegacy(
                "hello",
                "שלום",
                "en",
                "he",
                1,
                DateTimeOffset.UtcNow)
        ]);

        var item = Assert.Single(viewModel.Items);
        Assert.Equal(FlowDirection.LeftToRight, item.SourceFlowDirection);
        Assert.Equal(TextAlignment.Left, item.SourceTextAlignment);
        Assert.Equal(FlowDirection.RightToLeft, item.TranslationFlowDirection);
        Assert.Equal(TextAlignment.Left, item.TranslationTextAlignment);
    }

    [Fact]
    public async Task DefaultOrder_IsDescendingWithRecentTieBreaker()
    {
        var now = DateTimeOffset.UtcNow;
        var (_, viewModel, _, _) = await CreateAsync(
        [
            CreateItem("older", 3, now.AddMinutes(-2)),
            CreateItem("recent", 3, now),
            CreateItem("lower", 1, now.AddMinutes(1))
        ]);

        Assert.Equal(
            ["recent", "older", "lower"],
            viewModel.Items.Select(item => item.SourceText));
    }

    [Fact]
    public async Task ReverseOrder_ReversesSameTopTwentyPopulation()
    {
        var items = Enumerable.Range(1, 21)
            .Select(index => CreateItem($"word{index}", index, DateTimeOffset.UtcNow))
            .ToArray();
        var (_, viewModel, _, _) = await CreateAsync(items);
        Assert.DoesNotContain(viewModel.Items, item => item.SourceText == "word1");

        viewModel.ToggleSortCommand.Execute(null);

        Assert.Equal(20, viewModel.Items.Count);
        Assert.Equal("word2", viewModel.Items[0].SourceText);
        Assert.Equal("word21", viewModel.Items[^1].SourceText);
        Assert.DoesNotContain(viewModel.Items, item => item.SourceText == "word1");
    }

    [Fact]
    public async Task HiddenItem_CanEnterTopTwentyAfterMoreTranslations()
    {
        var items = Enumerable.Range(1, 21)
            .Select(index => CreateItem($"word{index}", index, DateTimeOffset.UtcNow))
            .ToArray();
        var (service, viewModel, _, _) = await CreateAsync(items);
        Assert.DoesNotContain(viewModel.Items, item => item.SourceText == "word1");
        var hidden = Assert.Single(service.Items, item => item.SourceText == "word1");

        for (var count = 0; count < 21; count++)
        {
            await service.RecordSuccessfulTranslationAsync(
                hidden.SourceText,
                hidden.PrimaryTranslation,
                "en",
                "he");
        }

        Assert.Contains(viewModel.Items, item => item.SourceText == "word1");
        Assert.Equal(21, service.Items.Count);
    }

    [Fact]
    public async Task CombinedOppositeDirectionCount_DrivesRankingAndDisplay()
    {
        var now = DateTimeOffset.UtcNow;
        var (_, viewModel, _, _) = await CreateAsync(
        [
            CreateLegacy("ball", "כדור", "en", "he", 4, now.AddMinutes(-2)),
            CreateLegacy("כדור", "ball", "he", "en", 3, now),
            CreateLegacy("bank", "בנק", "en", "he", 6, now)
        ]);

        Assert.Equal(2, viewModel.Items.Count);
        Assert.Equal("ball", viewModel.Items[0].SourceText);
        Assert.Equal("כדור", viewModel.Items[0].PrimaryTranslation);
        Assert.Equal("bank", viewModel.Items[1].SourceText);
    }

    [Fact]
    public async Task Export_ContainsMergedCanonicalPairOnlyOnce()
    {
        var exporter = new RecordingExportService();
        var (_, viewModel, _, _) = await CreateAsync(
        [
            CreateLegacy("ball", "כדור", "en", "he", 2, DateTimeOffset.UtcNow),
            CreateLegacy("כדור", "ball", "he", "en", 3, DateTimeOffset.UtcNow)
        ], exporter);

        await viewModel.ExportCsvCommand.ExecuteAsync(null);

        var exported = Assert.Single(exporter.Items!);
        Assert.Equal("ball", exported.SourceText);
        Assert.Equal("כדור", exported.PrimaryTranslation);
    }

    [Fact]
    public async Task CopyAndSave_ReuseExistingServicesWithoutChangingFrequency()
    {
        var (service, viewModel, savedWords, clipboard) = await CreateAsync(
            [CreateItem("repository", 4, DateTimeOffset.UtcNow)]);
        var item = Assert.Single(viewModel.Items);

        item.CopyCommand.Execute(null);
        await item.SaveCommand.ExecuteAsync(null);

        Assert.Equal("תרגום-repository", clipboard.Text);
        Assert.Single(savedWords.Items);
        Assert.True(item.IsSaved);
        Assert.Equal("Saved", item.SaveActionLabel);
        Assert.Equal(4, Assert.Single(service.Items).UsageCount);
    }

    [Theory]
    [InlineData(SavedWordsExportFormat.Csv)]
    [InlineData(SavedWordsExportFormat.Text)]
    [InlineData(SavedWordsExportFormat.Pdf)]
    public async Task Export_UsesVisibleOrderAndFrequentWordsOptions(
        SavedWordsExportFormat format)
    {
        var exporter = new RecordingExportService();
        var (_, viewModel, _, _) = await CreateAsync(
        [
            CreateItem("first", 2, DateTimeOffset.UtcNow),
            CreateItem("second", 1, DateTimeOffset.UtcNow)
        ], exporter);
        viewModel.ToggleSortCommand.Execute(null);

        await ExecuteExportAsync(viewModel, format);

        Assert.Equal(format, exporter.Format);
        Assert.Equal("Frequent Words", exporter.Title);
        Assert.Equal("frequent-words", exporter.FileNameStem);
        Assert.False(exporter.IncludeSavedAt);
        Assert.Equal(
            ["second", "first"],
            exporter.Items!.Select(item => item.SourceText));
    }

    private static async Task<(
        FrequentWordsService Service,
        FrequentWordsViewModel ViewModel,
        SavedWordsService SavedWords,
        RecordingClipboardService Clipboard)> CreateAsync(
        IReadOnlyList<FrequentWord> seed,
        RecordingExportService? exporter = null)
    {
        var store = new InMemoryFrequentWordsStore();
        await store.SaveAsync(seed);
        var service = new FrequentWordsService(store);
        await service.InitializeAsync();
        var savedWords = new SavedWordsService(new InMemorySavedWordsStore());
        await savedWords.InitializeAsync();
        var clipboard = new RecordingClipboardService();
        var viewModel = new FrequentWordsViewModel(
            service,
            savedWords,
            clipboard,
            exporter ?? new RecordingExportService());
        return (service, viewModel, savedWords, clipboard);
    }

    private static FrequentWord CreateItem(
        string source,
        int count,
        DateTimeOffset lastUsedAt) => new()
    {
        Id = Guid.NewGuid(),
        NormalizedSourceKey = source.ToLowerInvariant(),
        SourceText = source,
        PrimaryTranslation = $"תרגום-{source}",
        SourceLanguage = "en",
        TargetLanguage = "he",
        UsageCount = count,
        LastUsedAt = lastUsedAt
    };

    private static FrequentWord CreateLegacy(
        string source,
        string translation,
        string sourceLanguage,
        string targetLanguage,
        int count,
        DateTimeOffset lastUsedAt) => new()
    {
        Id = Guid.NewGuid(),
        NormalizedSourceKey = source.ToLowerInvariant(),
        SourceText = source,
        PrimaryTranslation = translation,
        SourceLanguage = sourceLanguage,
        TargetLanguage = targetLanguage,
        UsageCount = count,
        LastUsedAt = lastUsedAt
    };

    private static Task ExecuteExportAsync(
        FrequentWordsViewModel viewModel,
        SavedWordsExportFormat format) => format switch
    {
        SavedWordsExportFormat.Csv => viewModel.ExportCsvCommand.ExecuteAsync(null),
        SavedWordsExportFormat.Text => viewModel.ExportTextCommand.ExecuteAsync(null),
        SavedWordsExportFormat.Pdf => viewModel.ExportPdfCommand.ExecuteAsync(null),
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    private sealed class RecordingClipboardService : IClipboardService
    {
        public string? Text { get; private set; }

        public void SetText(string text) => Text = text;
    }

    private sealed class RecordingExportService : ISavedWordsExportService
    {
        public IReadOnlyList<SavedWord>? Items { get; private set; }

        public SavedWordsExportFormat? Format { get; private set; }

        public string? Title { get; private set; }

        public string? FileNameStem { get; private set; }

        public bool IncludeSavedAt { get; private set; }

        public Task<bool> ExportAsync(
            IReadOnlyList<SavedWord> items,
            SavedWordsExportFormat format,
            string title = "Saved Words",
            string fileNameStem = "saved-words",
            bool includeSavedAt = true,
            CancellationToken cancellationToken = default)
        {
            Items = items.ToArray();
            Format = format;
            Title = title;
            FileNameStem = fileNameStem;
            IncludeSavedAt = includeSavedAt;
            return Task.FromResult(true);
        }
    }
}
