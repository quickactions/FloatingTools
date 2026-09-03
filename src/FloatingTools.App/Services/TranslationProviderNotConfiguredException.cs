namespace FloatingTools.App.Services;

public sealed class TranslationProviderNotConfiguredException : InvalidOperationException
{
    public TranslationProviderNotConfiguredException()
        : base(
            "OpenAI API key is missing. Set OPENAI_API_KEY and restart FloatingTools.")
    {
    }
}
