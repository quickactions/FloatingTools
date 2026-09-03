using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.App.Services.Ai;

public sealed record ResolvedAiConfiguration(
    string? Provider,
    string Model,
    AiCredentialScope CredentialScope,
    ISecureApiKeyStore CredentialStore);
