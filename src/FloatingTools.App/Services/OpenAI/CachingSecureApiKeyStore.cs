namespace FloatingTools.App.Services.OpenAI;

/// <summary>
/// Wraps a real <see cref="ISecureApiKeyStore"/> with an in-memory cache of
/// the decrypted key, so repeated AI requests within the same process don't
/// each pay for a disk read + DPAPI decrypt. The cache never persists
/// anywhere new — it lives only as long as this object — and is kept
/// coherent with <see cref="Save"/>/<see cref="Remove"/> so a settings
/// change is reflected on the very next <see cref="Load"/>.
/// </summary>
public sealed class CachingSecureApiKeyStore(ISecureApiKeyStore inner) : ISecureApiKeyStore
{
    private readonly ISecureApiKeyStore _inner = inner
        ?? throw new ArgumentNullException(nameof(inner));
    private readonly Lock _gate = new();
    private bool _cached;
    private string? _cachedValue;

    public bool HasKey => _inner.HasKey;

    public string? Load()
    {
        lock (_gate)
        {
            if (_cached)
            {
                return _cachedValue;
            }

            _cachedValue = _inner.Load();
            _cached = true;
            return _cachedValue;
        }
    }

    public void Save(string apiKey)
    {
        _inner.Save(apiKey);
        lock (_gate)
        {
            _cachedValue = apiKey.Trim();
            _cached = true;
        }
    }

    public void Remove()
    {
        _inner.Remove();
        lock (_gate)
        {
            _cachedValue = null;
            _cached = true;
        }
    }
}
