using System.IO;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;
using System.Windows;

namespace FloatingTools.Tests.ViewModels;

public sealed class SavedWordsViewModelTests
{
    [Fact]
    public async Task ItemDirectionMapsHebrewAndEnglishToTheirPhysicalSides()
    {
        var (service, viewModel, _) = await CreateAsync();
        await service.AddAsync("שלום", "hello", "he", "en");

        var item = Assert.Single(viewModel.Items);
        Assert.Equal(FlowDirection.RightToLeft, item.SourceFlowDirection);
        Assert.Equal(TextAlignment.Left, item.SourceTextAlignment);
        Assert.Equal(FlowDirection.LeftToRight, item.TranslationFlowDirection);
        Assert.Equal(TextAlignment.Left, item.TranslationTextAlignment);
    }

    [Fact]
    public async Task Search_MatchesHebrewAndEnglishSides()
    {
        var (service, viewModel, _) = await CreateAsync();
        await service.AddAsync("Repository", "מאגר", "en", "he");

        viewModel.SearchQuery = "  מאגר  ";
        Assert.Single(viewModel.Items);

        viewModel.SearchQuery = "repository";
        Assert.Single(viewModel.Items);
    }

    [Fact]
    public async Task Search_EnglishIsCaseInsensitiveAndEmptyShowsAll()
    {
        var (service, viewModel, _) = await CreateAsync();
        await service.AddAsync("Repository", "מאגר", "en", "he");
        await service.AddAsync("Feature", "תכונה", "en", "he");

        viewModel.SearchQuery = "REPOSITORY";
        Assert.Single(viewModel.Items);
        Assert.Equal("Repository", viewModel.Items[0].SourceText);

        viewModel.SearchQuery = "  ";
        Assert.Equal(2, viewModel.Items.Count);
    }

    [Fact]
    public async Task Search_NoMatchExposesNoMatchState()
    {
        var (service, viewModel, _) = await CreateAsync();
        await service.AddAsync("Repository", "מאגר", "en", "he");

        viewModel.SearchQuery = "missing";

        Assert.Empty(viewModel.Items);
        Assert.True(viewModel.HasNoMatches);
        Assert.False(viewModel.IsEmpty);
    }

    [Fact]
    public async Task Copy_CopiesOnlyPrimaryTranslation()
    {
        var clipboard = new RecordingClipboardService();
        var (service, viewModel, _) = await CreateAsync(clipboard: clipboard);
        await service.AddAsync("Repository", "מאגר", "en", "he");

        Assert.Single(viewModel.Items).CopyCommand.Execute(null);

        Assert.Equal("מאגר", clipboard.Text);
    }

    [Fact]
    public async Task Remove_ChangesSavedWordsButNotTranslationHistory()
    {
        var history = new InMemoryTranslationHistoryStore();
        var historyEntry = CreateHistoryEntry();
        await history.AddOrUpdateAsync(historyEntry);
        var (service, viewModel, _) = await CreateAsync();
        await service.AddAsync("Repository", "מאגר", "en", "he");

        await Assert.Single(viewModel.Items).RemoveCommand.ExecuteAsync(null);

        Assert.Empty(service.Items);
        Assert.Single(await history.LoadAsync());
    }

    [Fact]
    public async Task Items_AreNewestSavedFirst()
    {
        var (service, viewModel, _) = await CreateAsync();
        await service.AddAsync("First", "ראשון", "en", "he");
        await service.AddAsync("Second", "שני", "en", "he");

        Assert.Collection(
            viewModel.Items,
            first => Assert.Equal("Second", first.SourceText),
            second => Assert.Equal("First", second.SourceText));
    }

    [Fact]
    public async Task ExportFailure_ShowsFriendlyMessageAndKeepsItems()
    {
        var exporter = new RecordingExportService
        {
            Exception = new IOException("failure")
        };
        var (service, viewModel, _) = await CreateAsync(exporter: exporter);
        await service.AddAsync("Repository", "מאגר", "en", "he");

        await viewModel.ExportCsvCommand.ExecuteAsync(null);

        Assert.Equal("Could not export saved words.", viewModel.ErrorMessage);
        Assert.Single(viewModel.Items);
    }

    [Theory]
    [InlineData(SavedWordsExportFormat.Csv)]
    [InlineData(SavedWordsExportFormat.Text)]
    [InlineData(SavedWordsExportFormat.Pdf)]
    public async Task Export_UsesAllSavedItemsAndRequestedFormat(
        SavedWordsExportFormat format)
    {
        var exporter = new RecordingExportService();
        var (service, viewModel, _) = await CreateAsync(exporter: exporter);
        await service.AddAsync("Repository", "מאגר", "en", "he");

        if (format == SavedWordsExportFormat.Csv)
        {
            await viewModel.ExportCsvCommand.ExecuteAsync(null);
        }
        else if (format == SavedWordsExportFormat.Text)
        {
            await viewModel.ExportTextCommand.ExecuteAsync(null);
        }
        else
        {
            await viewModel.ExportPdfCommand.ExecuteAsync(null);
        }

        Assert.Equal(format, exporter.Format);
        Assert.Single(exporter.Items!);
    }

    private static async Task<(
        SavedWordsService Service,
        SavedWordsViewModel ViewModel,
        RecordingClipboardService Clipboard)> CreateAsync(
        RecordingClipboardService? clipboard = null,
        RecordingExportService? exporter = null)
    {
        var service = new SavedWordsService(new InMemorySavedWordsStore());
        await service.InitializeAsync();
        clipboard ??= new RecordingClipboardService();
        var viewModel = new SavedWordsViewModel(
            service,
            clipboard,
            exporter ?? new RecordingExportService());
        return (service, viewModel, clipboard);
    }

    private static TranslationEntry CreateHistoryEntry()
    {
        var now = DateTimeOffset.UtcNow;
        return new TranslationEntry
        {
            Id = Guid.NewGuid(),
            SourceText = "Repository",
            Result = new TranslationResult("מאגר"),
            CreatedAt = now,
            LastUsedAt = now,
            UsageCount = 1
        };
    }

    private sealed class RecordingClipboardService : IClipboardService
    {
        public string? Text { get; private set; }

        public void SetText(string text) => Text = text;
    }

    private sealed class RecordingExportService : ISavedWordsExportService
    {
        public Exception? Exception { get; set; }

        public IReadOnlyList<SavedWord>? Items { get; private set; }

        public SavedWordsExportFormat? Format { get; private set; }

        public Task<bool> ExportAsync(
            IReadOnlyList<SavedWord> items,
            SavedWordsExportFormat format,
            string title = "Saved Words",
            string fileNameStem = "saved-words",
            bool includeSavedAt = true,
            CancellationToken cancellationToken = default)
        {
            if (Exception is not null)
            {
                return Task.FromException<bool>(Exception);
            }

            Items = items.ToArray();
            Format = format;
            return Task.FromResult(true);
        }
    }
}
