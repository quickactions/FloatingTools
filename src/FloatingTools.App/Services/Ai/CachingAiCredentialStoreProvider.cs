using System.Collections.Concurrent;
using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.App.Services.Ai;

/// <summary>
/// Wraps a real <see cref="IAiCredentialStoreProvider"/> so every caller
/// asking for the same <see cref="AiCredentialScope"/> gets back the same
/// <see cref="CachingSecureApiKeyStore"/> instance, instead of a fresh
/// store per call. This is what lets the cache actually help:
/// <see cref="Ai.AiConfigurationResolver"/> resolves a new store on every
/// request, so without this memoization each request would build its own
/// throwaway cache of one.
/// </summary>
public sealed class CachingAiCredentialStoreProvider(IAiCredentialStoreProvider inner)
    : IAiCredentialStoreProvider
{
    private readonly IAiCredentialStoreProvider _inner = inner
        ?? throw new ArgumentNullException(nameof(inner));
    private readonly ConcurrentDictionary<AiCredentialScope, ISecureApiKeyStore> _stores = new();

    public ISecureApiKeyStore GetStore(AiCredentialScope scope) =>
        _stores.GetOrAdd(
            scope,
            static (scope, inner) => new CachingSecureApiKeyStore(inner.GetStore(scope)),
            _inner);
}
