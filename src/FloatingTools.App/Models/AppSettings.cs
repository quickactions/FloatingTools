using System.Text.Json.Serialization;
using FloatingTools.App.Services;

namespace FloatingTools.App.Models;

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public WindowPlacement? WindowPlacement { get; set; }

    public ToolId LastUsedTool { get; set; } = ToolId.Translation;

    public PanelSizePreset ActiveToolPanelSize { get; set; } = PanelSizePreset.Standard;

    public double? StandardPanelZoomPercentage { get; set; }

    public double? LargePanelZoomPercentage { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? PanelZoomPercentage { get; set; }

    public string TranslationModel { get; set; } = OpenAiModelOptions.DefaultModel;

    public AiSettings Ai { get; set; } = new();

    public ApplicationLanguageMode ApplicationLanguage { get; set; } =
        ApplicationLanguageMode.System;

    public AppAppearanceMode Appearance { get; set; } = AppAppearanceMode.System;

    public CalendarSettings Calendar { get; set; } = new();

    public TranslationLanguageMode LanguageMode { get; set; } =
        TranslationLanguageMode.Automatic;

    public int HistoryLimit { get; set; } = HistoryLimitOptions.Default;
}
