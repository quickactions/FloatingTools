using System.Text.Json;
using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class JsonTranslationHistoryStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "FloatingTools-History-" + Guid.NewGuid());
    private string StoragePath => Path.Combine(_directory, "translation-history.json");

    [Fact]
    public async Task RecreatedStore_RestoresAllEntryAndResultFieldsInChronologicalOrder()
    {
        var store = new JsonTranslationHistoryStore(StoragePath);
        var earlier = Entry(0);
        var later = Entry(2);
        await store.AddOrUpdateAsync(later);
        await store.AddOrUpdateAsync(earlier);
        var restored = await new JsonTranslationHistoryStore(StoragePath).LoadAsync();
        Assert.Equal(new[] { earlier.Id, later.Id }, restored.Select(entry => entry.Id));
        var copy = restored[0];
        Assert.Equal(earlier.SourceText, copy.SourceText);
        Assert.Equal(earlier.CreatedAt, copy.CreatedAt);
        Assert.Equal(earlier.LastUsedAt, copy.LastUsedAt);
        Assert.Equal(earlier.IsFavorite, copy.IsFavorite);
        Assert.Equal(earlier.UsageCount, copy.UsageCount);
        Assert.Equal(earlier.Result.MainTranslation, copy.Result.MainTranslation);
        Assert.Equal(earlier.Result.CorrectedSourceText, copy.Result.CorrectedSourceText);
        Assert.Equal(earlier.Result.CorrectionStatus, copy.Result.CorrectionStatus);
        Assert.Equal(earlier.Result.Provider, copy.Result.Provider);
        Assert.Equal(earlier.Result.DetectedLanguage, copy.Result.DetectedLanguage);
        Assert.Equal(earlier.Result.TargetLanguage, copy.Result.TargetLanguage);
        Assert.Equal(earlier.Result.AlternativeTranslations, copy.Result.AlternativeTranslations);
        Assert.Equal(earlier.Result.Examples, copy.Result.Examples);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(StoragePath));
        Assert.Equal(new[] { "createdAt", "id", "isFavorite", "lastUsedAt", "result", "sourceText", "usageCount" },
            json.RootElement[0].EnumerateObject().Select(property => property.Name).Order());
        Assert.False(File.Exists(StoragePath + ".tmp"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("not JSON")]
    [InlineData("[{}]")]
    [InlineData("[null]")]
    public async Task MissingEmptyOrCorruptStorage_LoadsEmptyAndCanAcceptNewEntries(string? content)
    {
        if (content is not null)
        {
            Directory.CreateDirectory(_directory);
            await File.WriteAllTextAsync(StoragePath, content);
        }
        var store = new JsonTranslationHistoryStore(StoragePath);
        Assert.Empty(await store.LoadAsync());
        await store.AddOrUpdateAsync(Entry(0));
        Assert.Single(await new JsonTranslationHistoryStore(StoragePath).LoadAsync());
    }

    [Fact]
    public async Task AddOrUpdate_UpdatesByIdButDoesNotDeduplicateMatchingText()
    {
        var store = new JsonTranslationHistoryStore(StoragePath);
        var first = Entry(0);
        var duplicateText = Entry(0);
        await store.AddOrUpdateAsync(first);
        await store.AddOrUpdateAsync(duplicateText);
        first.UsageCount = 4;
        await store.AddOrUpdateAsync(first);
        var restored = await new JsonTranslationHistoryStore(StoragePath).LoadAsync();
        Assert.Equal(2, restored.Count);
        Assert.Equal(4, restored.Single(entry => entry.Id == first.Id).UsageCount);
    }

    [Fact]
    public async Task ClearAndRemove_PersistImmediatelyWithoutRemovingOtherEntries()
    {
        var store = new JsonTranslationHistoryStore(StoragePath);
        var entries = Enumerable.Range(0, 3).Select(Entry).ToArray();
        foreach (var entry in entries) await store.AddOrUpdateAsync(entry);
        store.Remove(entries[1].Id);
        store.Remove(Guid.NewGuid());
        Assert.Equal(new[] { entries[0].Id, entries[2].Id },
            (await new JsonTranslationHistoryStore(StoragePath).LoadAsync()).Select(entry => entry.Id));
        store.Clear();
        Assert.Empty(await store.LoadAsync());
        Assert.Empty(await new JsonTranslationHistoryStore(StoragePath).LoadAsync());
    }

    [Theory]
    [InlineData(49)]
    [InlineData(50)]
    [InlineData(51)]
    [InlineData(65)]
    public async Task Trim_KeepsNewestCapacityEntriesIncludingEqualTimestampTies(int count)
    {
        const int capacity = 50;
        var store = new JsonTranslationHistoryStore(StoragePath);
        var entries = Enumerable.Range(0, count).Select(Entry).ToArray();
        foreach (var entry in entries)
        {
            await store.AddOrUpdateAsync(entry);
            store.TrimToLimit(capacity);
        }
        var expected = entries.TakeLast(capacity).Select(entry => entry.Id);
        Assert.Equal(expected, (await store.LoadAsync()).Select(entry => entry.Id));
        Assert.Equal(expected, (await new JsonTranslationHistoryStore(StoragePath).LoadAsync()).Select(entry => entry.Id));
    }

    [Fact]
    public async Task FailedWrite_PreservesExistingDiskAndMemoryContents()
    {
        var store = new JsonTranslationHistoryStore(StoragePath);
        var entry = Entry(0);
        await store.AddOrUpdateAsync(entry);
        var before = await File.ReadAllBytesAsync(StoragePath);
        Directory.CreateDirectory(StoragePath + ".tmp");
        Assert.Throws<UnauthorizedAccessException>(() => store.Clear());
        Assert.Equal(entry.Id, Assert.Single(await store.LoadAsync()).Id);
        Assert.Equal(before, await File.ReadAllBytesAsync(StoragePath));
    }

    [Fact]
    public async Task AmbiguousResult_CanRoundTripWithoutInventingATranslation()
    {
        var entry = new TranslationEntry
        {
            Id = Guid.NewGuid(), SourceText = "ambiguous", CreatedAt = DateTimeOffset.UtcNow,
            LastUsedAt = DateTimeOffset.UtcNow,
            Result = new TranslationResult("", correctionStatus: TranslationCorrectionStatus.Ambiguous)
        };
        await new JsonTranslationHistoryStore(StoragePath).AddOrUpdateAsync(entry);
        var restored = Assert.Single(await new JsonTranslationHistoryStore(StoragePath).LoadAsync());
        Assert.Equal(TranslationCorrectionStatus.Ambiguous, restored.Result.CorrectionStatus);
        Assert.Empty(restored.Result.MainTranslation);
    }

    private static TranslationEntry Entry(int index) => new()
    {
        Id = Guid.NewGuid(), SourceText = "Hello שלום " + index,
        Result = new TranslationResult("שלום", "en", "Test", ["alternative"], ["example"],
            "Hello", TranslationCorrectionStatus.Confident, "he"),
        // Equal-time pairs explicitly exercise stable newest-first eviction.
        CreatedAt = DateTimeOffset.UnixEpoch.AddMinutes(index / 2),
        LastUsedAt = DateTimeOffset.UnixEpoch.AddMinutes(index), IsFavorite = true, UsageCount = 2
    };

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
