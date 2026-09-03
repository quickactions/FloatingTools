using System.IO;
using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class JsonSavedWordsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "FloatingTools.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAndLoad_RoundTripsUnicodeData()
    {
        var path = Path.Combine(_directory, "saved-words.json");
        var store = new JsonSavedWordsStore(path);
        var item = CreateItem("Repository", "מאגר");

        await store.SaveAsync([item]);
        var loaded = await store.LoadAsync();

        var restored = Assert.Single(loaded);
        Assert.Equal(item.Id, restored.Id);
        Assert.Equal("Repository", restored.SourceText);
        Assert.Equal("מאגר", restored.PrimaryTranslation);
        Assert.Equal(item.SavedAt, restored.SavedAt);
    }

    [Fact]
    public async Task Load_MissingFileReturnsEmpty()
    {
        var store = new JsonSavedWordsStore(
            Path.Combine(_directory, "missing.json"));

        Assert.Empty(await store.LoadAsync());
    }

    [Fact]
    public async Task Load_EmptyFileReturnsEmpty()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "empty.json");
        await File.WriteAllTextAsync(path, string.Empty);

        Assert.Empty(await new JsonSavedWordsStore(path).LoadAsync());
    }

    [Fact]
    public async Task Load_MalformedFileReturnsEmptyWithoutThrowing()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "malformed.json");
        await File.WriteAllTextAsync(path, "{ definitely not valid json");

        Assert.Empty(await new JsonSavedWordsStore(path).LoadAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static SavedWord CreateItem(string source, string translation) =>
        new()
        {
            Id = Guid.NewGuid(),
            SourceText = source,
            PrimaryTranslation = translation,
            SourceLanguage = "en",
            TargetLanguage = "he",
            SavedAt = DateTimeOffset.UtcNow
        };
}
