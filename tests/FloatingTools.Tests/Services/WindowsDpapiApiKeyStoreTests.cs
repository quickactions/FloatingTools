using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.Tests.Services;

public sealed class WindowsDpapiApiKeyStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "FloatingTools.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveLoadRemove_RoundTripsWithoutPlainTextStorage()
    {
        const string key = "sk-test-secret-value";
        var path = Path.Combine(_directory, "openai-key.dat");
        var store = new WindowsDpapiApiKeyStore(path);

        store.Save(key);

        Assert.True(store.HasKey);
        Assert.Equal(key, store.Load());
        Assert.DoesNotContain(key, Convert.ToBase64String(File.ReadAllBytes(path)));

        store.Remove();
        Assert.False(store.HasKey);
        Assert.Null(store.Load());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
