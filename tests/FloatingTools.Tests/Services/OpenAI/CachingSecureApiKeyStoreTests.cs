using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.Tests.Services.OpenAI;

public sealed class CachingSecureApiKeyStoreTests
{
    [Fact]
    public void Load_OnlyCallsTheInnerStoreOnce()
    {
        var inner = new CountingApiKeyStore("secret-key");
        var cache = new CachingSecureApiKeyStore(inner);

        var first = cache.Load();
        var second = cache.Load();
        var third = cache.Load();

        Assert.Equal("secret-key", first);
        Assert.Equal("secret-key", second);
        Assert.Equal("secret-key", third);
        Assert.Equal(1, inner.LoadCallCount);
    }

    [Fact]
    public void Load_CachesANullResultTooWithoutRepeatedlyCallingTheInnerStore()
    {
        var inner = new CountingApiKeyStore(null);
        var cache = new CachingSecureApiKeyStore(inner);

        Assert.Null(cache.Load());
        Assert.Null(cache.Load());
        Assert.Equal(1, inner.LoadCallCount);
    }

    [Fact]
    public void Save_UpdatesTheInnerStoreAndTheCacheSoTheNextLoadSeesTheNewValueWithoutRereading()
    {
        var inner = new CountingApiKeyStore("old-key");
        var cache = new CachingSecureApiKeyStore(inner);
        Assert.Equal("old-key", cache.Load());

        cache.Save("new-key");

        Assert.Equal("new-key", cache.Load());
        Assert.Equal("new-key", inner.SavedValue);
        // The inner Load() must not be called again after Save() — the
        // cache already knows the new value.
        Assert.Equal(1, inner.LoadCallCount);
    }

    [Fact]
    public void Save_TrimsTheCachedValueToMatchWhatARealLoadWouldReturn()
    {
        var inner = new CountingApiKeyStore(null);
        var cache = new CachingSecureApiKeyStore(inner);

        cache.Save("  padded-key  ");

        Assert.Equal("padded-key", cache.Load());
    }

    [Fact]
    public void Remove_ClearsTheInnerStoreAndTheCachedValue()
    {
        var inner = new CountingApiKeyStore("existing-key");
        var cache = new CachingSecureApiKeyStore(inner);
        Assert.Equal("existing-key", cache.Load());

        cache.Remove();

        Assert.Null(cache.Load());
        Assert.True(inner.RemoveCalled);
        Assert.Equal(1, inner.LoadCallCount);
    }

    [Fact]
    public void HasKey_AlwaysDelegatesToTheInnerStoreRatherThanBeingCached()
    {
        var inner = new CountingApiKeyStore("key") { HasKeyOverride = true };
        var cache = new CachingSecureApiKeyStore(inner);

        Assert.True(cache.HasKey);
        inner.HasKeyOverride = false;
        Assert.False(cache.HasKey);
    }

    [Fact]
    public async Task ConcurrentLoads_OnlyCallTheInnerStoreOnceAndNeverThrow()
    {
        var inner = new CountingApiKeyStore("shared-key", loadDelay: TimeSpan.FromMilliseconds(20));
        var cache = new CachingSecureApiKeyStore(inner);

        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => cache.Load()))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        Assert.All(results, result => Assert.Equal("shared-key", result));
        Assert.Equal(1, inner.LoadCallCount);
    }

    private sealed class CountingApiKeyStore(string? value, TimeSpan? loadDelay = null)
        : ISecureApiKeyStore
    {
        public int LoadCallCount { get; private set; }

        public bool RemoveCalled { get; private set; }

        public string? SavedValue { get; private set; }

        public bool HasKeyOverride { get; set; } = value is not null;

        public bool HasKey => HasKeyOverride;

        public string? Load()
        {
            LoadCallCount++;
            if (loadDelay is { } delay)
            {
                Thread.Sleep(delay);
            }

            return value;
        }

        public void Save(string apiKey)
        {
            SavedValue = apiKey;
            value = apiKey;
        }

        public void Remove()
        {
            RemoveCalled = true;
            value = null;
        }
    }
}
