namespace FloatingTools.App.Services;

/// <summary>
/// Shared user-facing wording for AI configuration problems, so Translation and
/// Quick Chat report a missing key identically instead of drifting apart.
/// </summary>
public static class AiConfigurationMessages
{
    public const string MissingApiKey =
        "OpenAI API key is missing. Add it in Settings.";
}
