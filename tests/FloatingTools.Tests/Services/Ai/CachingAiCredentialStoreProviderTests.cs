using FloatingTools.App.Services.Ai;
using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.Tests.Services.Ai;

public sealed class CachingAiCredentialStoreProviderTests
{
    [Fact]
    public void GetStore_ReturnsTheSameInstanceForTheSameScope()
    {
        var provider = new CachingAiCredentialStoreProvider(new FakeProvider());

        var first = provider.GetStore(AiCredentialScope.Translation);
        var second = provider.GetStore(AiCredentialScope.Translation);

        Assert.Same(first, second);
    }

    [Fact]
    public void GetStore_ReturnsDistinctInstancesForDistinctScopes()
    {
        var provider = new CachingAiCredentialStoreProvider(new FakeProvider());

        var application = provider.GetStore(AiCredentialScope.Application);
        var translation = provider.GetStore(AiCredentialScope.Translation);
        var quickChat = provider.GetStore(AiCredentialScope.QuickChat);

        Assert.NotSame(application, translation);
        Assert.NotSame(application, quickChat);
        Assert.NotSame(translation, quickChat);
    }

    [Fact]
    public void GetStore_WrapsEachInnerStoreExactlyOnce()
    {
        var inner = new FakeProvider();
        var provider = new CachingAiCredentialStoreProvider(inner);

        provider.GetStore(AiCredentialScope.Translation);
        provider.GetStore(AiCredentialScope.Translation);
        provider.GetStore(AiCredentialScope.Translation);

        Assert.Equal(1, inner.GetStoreCallCount);
    }

    [Fact]
    public void RepeatedResolutionAcrossTheSameScope_OnlyLoadsFromTheInnerStoreOnce()
    {
        var inner = new FakeProvider();
        var provider = new CachingAiCredentialStoreProvider(inner);

        // Mirrors AiConfigurationResolver.Resolve, which calls GetStore(scope)
        // fresh on every request.
        provider.GetStore(AiCredentialScope.Translation).Load();
        provider.GetStore(AiCredentialScope.Translation).Load();
        provider.GetStore(AiCredentialScope.Translation).Load();

        Assert.Equal(1, inner.Store(AiCredentialScope.Translation).LoadCallCount);
    }

    [Fact]
    public void SaveOnAResolvedStore_IsVisibleToTheNextResolutionOfTheSameScope()
    {
        var provider = new CachingAiCredentialStoreProvider(new FakeProvider());

        provider.GetStore(AiCredentialScope.QuickChat).Save("updated-key");

        Assert.Equal("updated-key", provider.GetStore(AiCredentialScope.QuickChat).Load());
    }

    private sealed class FakeProvider : IAiCredentialStoreProvider
    {
        private readonly Dictionary<AiCredentialScope, RecordingStore> _stores = new();

        public int GetStoreCallCount { get; private set; }

        public RecordingStore Store(AiCredentialScope scope) => _stores[scope];

        public ISecureApiKeyStore GetStore(AiCredentialScope scope)
        {
            GetStoreCallCount++;
            if (!_stores.TryGetValue(scope, out var store))
            {
                store = new RecordingStore();
                _stores[scope] = store;
            }

            return store;
        }
    }

    public sealed class RecordingStore : ISecureApiKeyStore
    {
        private string? _value;

        public int LoadCallCount { get; private set; }

        public bool HasKey => _value is not null;

        public string? Load()
        {
            LoadCallCount++;
            return _value;
        }

        public void Save(string apiKey) => _value = apiKey;

        public void Remove() => _value = null;
    }
}
