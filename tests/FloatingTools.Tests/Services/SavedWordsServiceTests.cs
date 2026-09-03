using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class SavedWordsServiceTests
{
    [Fact]
    public async Task Add_SavesSourcePrimaryLanguagesAndNewestFirst()
    {
        var service = await CreateServiceAsync();

        var firstResult = await service.AddAsync(
            "Repository", "מאגר", "en", "he");
        await service.AddAsync("Feature", "תכונה", "en", "he");

        Assert.Equal(SavedWordAddResult.Added, firstResult);
        Assert.Collection(
            service.Items,
            newest =>
            {
                Assert.Equal("Feature", newest.SourceText);
                Assert.Equal("תכונה", newest.PrimaryTranslation);
            },
            oldest =>
            {
                Assert.Equal("Repository", oldest.SourceText);
                Assert.Equal("מאגר", oldest.PrimaryTranslation);
                Assert.Equal("en", oldest.SourceLanguage);
                Assert.Equal("he", oldest.TargetLanguage);
                Assert.NotEqual(default, oldest.SavedAt);
            });
    }

    [Fact]
    public async Task Add_ExactPairCannotBeSavedTwice()
    {
        var service = await CreateServiceAsync();

        await service.AddAsync("Repository", "מאגר", "en", "he");
        var duplicate = await service.AddAsync(
            "Repository", "מאגר", "en", "he");

        Assert.Equal(SavedWordAddResult.AlreadyExists, duplicate);
        Assert.Single(service.Items);
    }

    [Fact]
    public async Task Add_SameSourceWithDifferentPrimaryIsAllowed()
    {
        var service = await CreateServiceAsync();

        await service.AddAsync("light", "אור", "en", "he");
        var second = await service.AddAsync("light", "קל", "en", "he");

        Assert.Equal(SavedWordAddResult.Added, second);
        Assert.Equal(2, service.Items.Count);
    }

    [Fact]
    public async Task Add_Item201IsRejectedWithoutDeletingOlderItems()
    {
        var service = await CreateServiceAsync();
        for (var index = 0; index < ISavedWordsService.MaximumItemCount; index++)
        {
            Assert.Equal(
                SavedWordAddResult.Added,
                await service.AddAsync(
                    $"source {index}",
                    $"translation {index}",
                    "en",
                    "he"));
        }

        var rejected = await service.AddAsync(
            "source 201", "translation 201", "en", "he");

        Assert.Equal(SavedWordAddResult.LimitReached, rejected);
        Assert.Equal(ISavedWordsService.MaximumItemCount, service.Items.Count);
        Assert.Contains(service.Items, item => item.SourceText == "source 0");
    }

    [Fact]
    public async Task Remove_AllowsAnotherItemAfterLimit()
    {
        var service = await CreateServiceAsync();
        for (var index = 0; index < ISavedWordsService.MaximumItemCount; index++)
        {
            await service.AddAsync($"s{index}", $"t{index}", "en", "he");
        }

        Assert.True(await service.RemoveAsync(service.Items[0].Id));
        var result = await service.AddAsync("new", "חדש", "en", "he");

        Assert.Equal(SavedWordAddResult.Added, result);
        Assert.Equal(ISavedWordsService.MaximumItemCount, service.Items.Count);
    }

    private static async Task<SavedWordsService> CreateServiceAsync()
    {
        var service = new SavedWordsService(new InMemorySavedWordsStore());
        await service.InitializeAsync();
        return service;
    }
}
