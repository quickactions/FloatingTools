using FloatingTools.App.Models;

namespace FloatingTools.App.Services.Ai;

public sealed class AiConfigurationResolver(
    AppSettings settings,
    IAiCredentialStoreProvider credentialStores)
{
    public ResolvedAiConfiguration Resolve(AiToolId tool)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(credentialStores);

        var toolSettings = GetToolSettings(tool);
        var credentialScope = toolSettings.UseAppCredentials
            ? AiCredentialScope.Application
            : GetToolCredentialScope(tool);
        var provider = toolSettings.UseAppCredentials
            ? settings.Ai.DefaultProvider
            : toolSettings.Provider;
        var model = string.IsNullOrWhiteSpace(toolSettings.Model)
            ? settings.Ai.DefaultModel
            : toolSettings.Model;

        return new ResolvedAiConfiguration(
            provider,
            model,
            credentialScope,
            credentialStores.GetStore(credentialScope));
    }

    private AiToolSettings GetToolSettings(AiToolId tool) => tool switch
    {
        AiToolId.Translation => settings.Ai.Translation,
        AiToolId.QuickChat => settings.Ai.QuickChat,
        _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, null)
    };

    private static AiCredentialScope GetToolCredentialScope(AiToolId tool) => tool switch
    {
        AiToolId.Translation => AiCredentialScope.Translation,
        AiToolId.QuickChat => AiCredentialScope.QuickChat,
        _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, null)
    };
}
