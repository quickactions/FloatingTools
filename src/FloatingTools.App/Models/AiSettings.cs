namespace FloatingTools.App.Models;

public sealed class AiSettings
{
    public string DefaultProvider { get; set; } = "OpenAI";

    public string DefaultModel { get; set; } = OpenAiModelOptions.DefaultModel;

    public AiToolSettings Translation { get; set; } = new();

    public AiToolSettings QuickChat { get; set; } = new();
}
