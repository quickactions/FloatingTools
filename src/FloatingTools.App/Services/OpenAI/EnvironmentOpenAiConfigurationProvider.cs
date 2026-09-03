namespace FloatingTools.App.Services.OpenAI;

public sealed class EnvironmentOpenAiConfigurationProvider
    : IOpenAiConfigurationProvider
{
    public const string ApiKeyVariableName = "OPENAI_API_KEY";
    public const string ModelVariableName = "FLOATINGTOOLS_OPENAI_MODEL";
    public const string DefaultModel = "gpt-5.6-luna";

    public OpenAiTranslationConfiguration? GetConfiguration()
    {
        var apiKey = Environment.GetEnvironmentVariable(ApiKeyVariableName);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var model = Environment.GetEnvironmentVariable(ModelVariableName);
        return new OpenAiTranslationConfiguration(
            apiKey.Trim(),
            string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim());
    }
}
