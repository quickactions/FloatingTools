namespace FloatingTools.App.Services.OpenAI;

public interface ISecureApiKeyStore
{
    bool HasKey { get; }

    string? Load();

    void Save(string apiKey);

    void Remove();
}
