using System.IO;
using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.App.Services.Ai;

public sealed class WindowsDpapiAiCredentialStoreProvider(string credentialDirectory)
    : IAiCredentialStoreProvider
{
    public const string ApplicationCredentialFileName = "openai-key.dat";
    public const string TranslationCredentialFileName = "translation-openai-key.dat";
    public const string QuickChatCredentialFileName = "quick-chat-openai-key.dat";

    public ISecureApiKeyStore GetStore(AiCredentialScope scope) =>
        new WindowsDpapiApiKeyStore(GetPath(scope));

    public string GetPath(AiCredentialScope scope) =>
        Path.Combine(credentialDirectory, GetFileName(scope));

    private static string GetFileName(AiCredentialScope scope) => scope switch
    {
        AiCredentialScope.Application => ApplicationCredentialFileName,
        AiCredentialScope.Translation => TranslationCredentialFileName,
        AiCredentialScope.QuickChat => QuickChatCredentialFileName,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
    };
}
