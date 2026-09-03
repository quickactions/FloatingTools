namespace FloatingTools.App.Services.OpenAI;

public sealed class InMemorySecureApiKeyStore(string? apiKey = null)
    : ISecureApiKeyStore
{
    private string? _apiKey = apiKey;

    public bool HasKey => !string.IsNullOrWhiteSpace(_apiKey);

    public string? Load() => _apiKey;

    public void Save(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        _apiKey = apiKey.Trim();
    }

    public void Remove() => _apiKey = null;
}
