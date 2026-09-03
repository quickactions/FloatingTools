namespace FloatingTools.App.Models;

public static class OpenAiModelOptions
{
    public const string SolModel = "gpt-5.6-sol";
    public const string TerraModel = "gpt-5.6-terra";
    public const string LunaModel = "gpt-5.6-luna";
    public const string DefaultModel = LunaModel;

    // Retained so settings written by earlier versions keep their effective model.
    // It is deliberately excluded from Supported and is not a selectable model.
    public const string LegacyNanoModel = "gpt-5.4-nano";

    // Compatibility alias for existing callers that need a non-default supported model.
    public const string FastModel = TerraModel;

    public static IReadOnlyList<string> Supported { get; } =
        [SolModel, TerraModel, LunaModel];

    public static string Normalize(string? model) =>
        Supported.Contains(model, StringComparer.Ordinal)
        || string.Equals(model, LegacyNanoModel, StringComparison.Ordinal)
            ? model!
            : DefaultModel;
}
