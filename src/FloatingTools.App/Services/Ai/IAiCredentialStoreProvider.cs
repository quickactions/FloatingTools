using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.App.Services.Ai;

public interface IAiCredentialStoreProvider
{
    ISecureApiKeyStore GetStore(AiCredentialScope scope);
}
