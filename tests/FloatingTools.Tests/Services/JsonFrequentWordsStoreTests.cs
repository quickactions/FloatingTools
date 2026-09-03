using System.IO;
using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class JsonFrequentWordsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "FloatingTools.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAndLoad_RoundTripsFrequencyAndUnicode()
    {
        var store = new JsonFrequentWordsStore(Path.Combine(_directory, "frequent.json"));
        var item = CreateItem();

        await store.SaveAsync([item]);
        var restored = Assert.Single(await store.LoadAsync());

        Assert.Equal(item.Id, restored.Id);
        Assert.Equal("מאגר", restored.SourceText);
        Assert.Equal(7, restored.UsageCount);
        Assert.Equal(item.LastUsedAt, restored.LastUsedAt);
    }

    [Fact]
    public async Task MissingFile_ReturnsEmpty()
    {
        var store = new JsonFrequentWordsStore(Path.Combine(_directory, "missing.json"));
        Assert.Empty(await store.LoadAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ not valid json")]
    public async Task EmptyOrMalformedFile_ReturnsEmpty(string content)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "frequent.json");
        await File.WriteAllTextAsync(path, content);

        Assert.Empty(await new JsonFrequentWordsStore(path).LoadAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static FrequentWord CreateItem() => new()
    {
        Id = Guid.NewGuid(),
        NormalizedSourceKey = "מאגר",
        SourceText = "מאגר",
        PrimaryTranslation = "repository",
        SourceLanguage = "he",
        TargetLanguage = "en",
        UsageCount = 7,
        LastUsedAt = DateTimeOffset.UtcNow
    };
}
