namespace FloatingTools.App.Services;

public sealed class TranslationProviderNotConfiguredException : InvalidOperationException
{
    public TranslationProviderNotConfiguredException()
        : base(AiConfigurationMessages.MissingApiKey)
    {
    }
}
