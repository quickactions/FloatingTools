using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class FrequentWordsServiceTests
{
    [Fact]
    public async Task EnglishVariants_IncrementOneCanonicalPair()
    {
        var service = await CreateAsync();

        Assert.True(await service.RecordSuccessfulTranslationAsync(
            "Hello",
            "שלום",
            "en",
            "he"));
        Assert.True(await service.RecordSuccessfulTranslationAsync(
            " hello! ",
            "שלום!",
            "en",
            "he"));

        var item = Assert.Single(service.Items);
        Assert.Equal("hello", item.NormalizedSourceKey);
        Assert.Equal("hello", item.SourceText);
        Assert.Equal("שלום", item.PrimaryTranslation);
        Assert.Equal(2, item.UsageCount);
    }

    [Fact]
    public async Task HebrewWord_IncrementsAndPreservesText()
    {
        var service = await CreateAsync();

        await service.RecordSuccessfulTranslationAsync(
            "מאגר",
            "repository",
            "he",
            "en");

        var item = Assert.Single(service.Items);
        Assert.Equal("repository", item.NormalizedSourceKey);
        Assert.Equal("repository", item.SourceText);
        Assert.Equal("מאגר", item.PrimaryTranslation);
        Assert.Equal(1, item.UsageCount);
    }

    [Fact]
    public async Task OppositeDirections_MergeIntoEnglishHebrewCanonicalPair()
    {
        var service = await CreateAsync();

        await service.RecordSuccessfulTranslationAsync("Ball", "כדור", "en", "he");
        await service.RecordSuccessfulTranslationAsync("כדור", "ball!", "he", "en");

        var item = Assert.Single(service.Items);
        Assert.Equal("ball", item.SourceText);
        Assert.Equal("כדור", item.PrimaryTranslation);
        Assert.Equal("en", item.SourceLanguage);
        Assert.Equal("he", item.TargetLanguage);
        Assert.Equal(2, item.UsageCount);
    }

    [Fact]
    public async Task PairIdentity_UsesBothLanguagesAndKeepsDistinctMeanings()
    {
        var service = await CreateAsync();

        await service.RecordSuccessfulTranslationAsync("bank", "בנק", "en", "he");
        await service.RecordSuccessfulTranslationAsync("bank", "גדה", "en", "he");
        await service.RecordSuccessfulTranslationAsync("בנק", "bank", "he", "en");

        Assert.Equal(2, service.Items.Count);
        var bank = Assert.Single(service.Items, item => item.PrimaryTranslation == "בנק");
        var shore = Assert.Single(service.Items, item => item.PrimaryTranslation == "גדה");
        Assert.Equal(2, bank.UsageCount);
        Assert.Equal(1, shore.UsageCount);
    }

    [Fact]
    public async Task Initialize_MergesLegacyOppositeDirectionsAndPersistsMigration()
    {
        var store = new InMemoryFrequentWordsStore();
        var older = DateTimeOffset.UtcNow.AddHours(-1);
        var newer = DateTimeOffset.UtcNow;
        await store.SaveAsync(
        [
            CreateLegacy("ball", "כדור", "en", "he", 5, older),
            CreateLegacy("כדור", "ball", "he", "en", 3, newer)
        ]);

        var service = new FrequentWordsService(store);
        await service.InitializeAsync();

        var merged = Assert.Single(service.Items);
        Assert.Equal("ball", merged.SourceText);
        Assert.Equal("כדור", merged.PrimaryTranslation);
        Assert.Equal(8, merged.UsageCount);
        Assert.Equal(newer, merged.LastUsedAt);
        var persisted = Assert.Single(await store.LoadAsync());
        Assert.Equal(merged.CanonicalPairKey, persisted.CanonicalPairKey);
        Assert.Equal(8, persisted.UsageCount);
    }

    [Theory]
    [InlineData("hello world", "en")]
    [InlineData("אני רוצה", "he")]
    [InlineData("hello\nworld", "en")]
    public async Task IneligibleInput_DoesNotPersist(string input, string language)
    {
        var service = await CreateAsync();

        var recorded = await service.RecordSuccessfulTranslationAsync(
            input,
            "translation",
            language,
            language == "en" ? "he" : "en");

        Assert.False(recorded);
        Assert.Empty(service.Items);
    }

    [Fact]
    public async Task ServicePersistsAllItemsBeyondVisibleTopTwenty()
    {
        var store = new InMemoryFrequentWordsStore();
        var service = new FrequentWordsService(store);
        await service.InitializeAsync();
        for (var index = 1; index <= 21; index++)
        {
            await service.RecordSuccessfulTranslationAsync(
                $"word{index}",
                $"מילה{index}",
                "en",
                "he");
        }

        var reloaded = new FrequentWordsService(store);
        await reloaded.InitializeAsync();

        Assert.Equal(21, reloaded.Items.Count);
    }

    private static async Task<FrequentWordsService> CreateAsync()
    {
        var service = new FrequentWordsService(new InMemoryFrequentWordsStore());
        await service.InitializeAsync();
        return service;
    }

    private static FloatingTools.App.Models.FrequentWord CreateLegacy(
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
}
