using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class InMemoryTranslationHistoryStoreTests
{
    [Fact]
    public async Task AddOrUpdateAsync_PersistsAndUpdatesAnEntry()
    {
        var store = new InMemoryTranslationHistoryStore();
        var now = DateTimeOffset.UtcNow;
        var entry = new TranslationEntry
        {
            Id = Guid.NewGuid(),
            SourceText = "Hello",
            Result = new TranslationResult("שלום"),
            CreatedAt = now,
            IsFavorite = false,
            UsageCount = 1,
            LastUsedAt = now
        };

        await store.AddOrUpdateAsync(entry);
        entry.IsFavorite = true;
        await store.AddOrUpdateAsync(entry);
        var loaded = await store.LoadAsync();

        var saved = Assert.Single(loaded);
        Assert.Same(entry, saved);
        Assert.True(saved.IsFavorite);
    }

    [Fact]
    public async Task LoadAsync_ReturnsEntriesInChronologicalOrder()
    {
        var store = new InMemoryTranslationHistoryStore();
        var later = CreateEntry("Later", DateTimeOffset.UtcNow);
        var earlier = CreateEntry("Earlier", later.CreatedAt.AddMinutes(-1));

        await store.AddOrUpdateAsync(later);
        await store.AddOrUpdateAsync(earlier);
        var loaded = await store.LoadAsync();

        Assert.Collection(
            loaded,
            first => Assert.Equal("Earlier", first.SourceText),
            second => Assert.Equal("Later", second.SourceText));
    }

    private static TranslationEntry CreateEntry(
        string source,
        DateTimeOffset createdAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            SourceText = source,
            Result = new TranslationResult("Result"),
            CreatedAt = createdAt,
            IsFavorite = false,
            UsageCount = 1,
            LastUsedAt = createdAt
        };
}
