using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "FloatingTools.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Load_ReturnsDefaultsWhenFileIsMissing()
    {
        var service = CreateService();

        var result = service.Load();

        Assert.Equal(AppSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.Null(result.WindowPlacement);
        Assert.Equal(PanelSizePreset.Standard, result.ActiveToolPanelSize);
        Assert.Equal(OpenAiModelOptions.DefaultModel, result.TranslationModel);
        Assert.Equal(OpenAiModelOptions.DefaultModel, result.Ai.Translation.Model);
        Assert.Equal(ApplicationLanguageMode.System, result.ApplicationLanguage);
        Assert.Equal(TranslationLanguageMode.Automatic, result.LanguageMode);
        Assert.Equal(200, result.HistoryLimit);
    }

    [Fact]
    public void Load_ReturnsDefaultsWhenJsonIsMalformed()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(GetSettingsPath(), "{ this is not valid JSON");
        var service = CreateService();

        var result = service.Load();

        Assert.Equal(AppSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.Null(result.WindowPlacement);
    }

    [Fact]
    public void Load_OldStageOneSettings_DefaultsLastUsedToolWithoutLosingPlacement()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(
            GetSettingsPath(),
            """
            {
              "schemaVersion": 1,
              "windowPlacement": {
                "monitorId": "\\\\.\\DISPLAY1",
                "dockSide": "Left",
                "verticalOffset": 48
              }
            }
            """);
        var service = CreateService();

        var result = service.Load();

        Assert.Equal(ToolId.Translation, result.LastUsedTool);
        Assert.NotNull(result.WindowPlacement);
        Assert.Equal(DockSide.Left, result.WindowPlacement.DockSide);
        Assert.Equal(48, result.WindowPlacement.VerticalOffset);
    }

    [Fact]
    public void Load_InvalidLastUsedTool_FallsBackWithoutLosingPlacement()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(
            GetSettingsPath(),
            """
            {
              "schemaVersion": 1,
              "windowPlacement": {
                "monitorId": "\\\\.\\DISPLAY1",
                "dockSide": "Right",
                "verticalOffset": 125
              },
              "lastUsedTool": "UnknownTool"
            }
            """);
        var service = CreateService();

        var result = service.Load();

        Assert.Equal(ToolId.Translation, result.LastUsedTool);
        Assert.NotNull(result.WindowPlacement);
        Assert.Equal(DockSide.Right, result.WindowPlacement.DockSide);
        Assert.Equal(125, result.WindowPlacement.VerticalOffset);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsPlacement()
    {
        var service = CreateService();
        var settings = new AppSettings
        {
            WindowPlacement = new WindowPlacement(
                @"\\.\DISPLAY1",
                DockSide.Left,
                125.5)
        };

        var saved = service.Save(settings);
        var loaded = service.Load();

        Assert.True(saved);
        Assert.Equal(settings.WindowPlacement, loaded.WindowPlacement);
        Assert.False(File.Exists(GetSettingsPath() + ".tmp"));
    }

    [Fact]
    public void SaveLastUsedTool_PreservesWindowPlacement()
    {
        var service = CreateService();
        var placement = new WindowPlacement(
            @"\\.\DISPLAY2",
            DockSide.Right,
            225.5);
        var settings = new AppSettings
        {
            WindowPlacement = placement,
            LastUsedTool = ToolId.Translation
        };

        var saved = service.Save(settings);
        var loaded = service.Load();

        Assert.True(saved);
        Assert.Equal(ToolId.Translation, loaded.LastUsedTool);
        Assert.Equal(placement, loaded.WindowPlacement);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsActiveToolPanelSize()
    {
        var service = CreateService();
        var settings = new AppSettings
        {
            ActiveToolPanelSize = PanelSizePreset.Large
        };

        Assert.True(service.Save(settings));

        var loaded = service.Load();
        Assert.Equal(PanelSizePreset.Large, loaded.ActiveToolPanelSize);
    }

    [Fact]
    public void InvalidPanelSize_FallsBackWithoutLosingPlacement()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(
            GetSettingsPath(),
            """
            {
              "schemaVersion": 1,
              "windowPlacement": {
                "monitorId": "\\\\.\\DISPLAY1",
                "dockSide": "Left",
                "verticalOffset": 48
              },
              "activeToolPanelSize": "UnknownSize"
            }
            """);

        var loaded = CreateService().Load();

        Assert.Equal(PanelSizePreset.Standard, loaded.ActiveToolPanelSize);
        Assert.NotNull(loaded.WindowPlacement);
        Assert.Equal(DockSide.Left, loaded.WindowPlacement.DockSide);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsTranslationSettingsIndependently()
    {
        var settings = new AppSettings
        {
            TranslationModel = OpenAiModelOptions.FastModel,
            LanguageMode = TranslationLanguageMode.EnglishToHebrew,
            HistoryLimit = 50
        };

        Assert.True(CreateService().Save(settings));
        var loaded = CreateService().Load();

        Assert.Equal(OpenAiModelOptions.FastModel, loaded.TranslationModel);
        Assert.Equal(TranslationLanguageMode.EnglishToHebrew, loaded.LanguageMode);
        Assert.Equal(50, loaded.HistoryLimit);
        Assert.False(File.Exists(GetSettingsPath() + ".tmp"));
    }

    [Fact]
    public void Load_OldShapedSettingsWithoutAi_MigratesTranslationToPinnedAiModel()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(
            GetSettingsPath(),
            """
            {
              "schemaVersion": 1,
              "translationModel": "gpt-5.4-nano",
              "lastUsedTool": "Translation"
            }
            """);

        var loaded = CreateService().Load();

        Assert.Equal(1, AppSettings.CurrentSchemaVersion);
        Assert.Equal(AppSettings.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal(OpenAiModelOptions.LegacyNanoModel, loaded.TranslationModel);
        Assert.Equal("OpenAI", loaded.Ai.DefaultProvider);
        Assert.Equal(OpenAiModelOptions.DefaultModel, loaded.Ai.DefaultModel);
        Assert.True(loaded.Ai.Translation.UseAppCredentials);
        Assert.True(loaded.Ai.QuickChat.UseAppCredentials);
        Assert.Equal(OpenAiModelOptions.LegacyNanoModel, loaded.Ai.Translation.Model);
        Assert.Null(loaded.Ai.QuickChat.Model);
    }

    [Fact]
    public void LoadSaveReload_OldWritingModelIsIgnoredAndOtherSettingsArePreserved()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(
            GetSettingsPath(),
            """
            {
              "schemaVersion": 1,
              "windowPlacement": {
                "monitorId": "\\\\.\\DISPLAY2",
                "dockSide": "Right",
                "verticalOffset": 125
              },
              "lastUsedTool": "Notes",
              "activeToolPanelSize": "Large",
              "translationModel": "gpt-5.4-nano",
              "writingModel": "gpt-5.6-luna",
              "languageMode": "EnglishToHebrew",
              "historyLimit": 50
            }
            """);
        var service = CreateService();

        var migrated = service.Load();

        Assert.Equal(AppSettings.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(OpenAiModelOptions.LegacyNanoModel, migrated.TranslationModel);
        Assert.Equal(OpenAiModelOptions.LegacyNanoModel, migrated.Ai.Translation.Model);
        Assert.True(migrated.Ai.Translation.UseAppCredentials);
        Assert.Null(migrated.Ai.QuickChat.Model);
        Assert.Equal(ToolId.Notes, migrated.LastUsedTool);
        Assert.Equal(PanelSizePreset.Large, migrated.ActiveToolPanelSize);
        Assert.Equal(TranslationLanguageMode.EnglishToHebrew, migrated.LanguageMode);
        Assert.Equal(50, migrated.HistoryLimit);
        Assert.NotNull(migrated.WindowPlacement);
        Assert.Equal(DockSide.Right, migrated.WindowPlacement.DockSide);

        Assert.True(service.Save(migrated));
        var firstSavedJson = File.ReadAllText(GetSettingsPath());
        var reloaded = service.Load();
        Assert.True(service.Save(reloaded));
        var secondSavedJson = File.ReadAllText(GetSettingsPath());

        Assert.Equal(OpenAiModelOptions.LegacyNanoModel, reloaded.Ai.Translation.Model);
        Assert.DoesNotContain("\"writingModel\"", firstSavedJson);
        Assert.Equal(firstSavedJson, secondSavedJson);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsLegacyTranslationModelAlongsideIndependentAiToolModels()
    {
        var settings = new AppSettings
        {
            TranslationModel = OpenAiModelOptions.FastModel,
            Ai = new AiSettings
            {
                Translation = new AiToolSettings { Model = "translation-specific-model" },
                QuickChat = new AiToolSettings { Model = "quick-chat-specific-model" }
            }
        };

        Assert.True(CreateService().Save(settings));
        var loaded = CreateService().Load();

        Assert.Equal(OpenAiModelOptions.FastModel, loaded.TranslationModel);
        Assert.Equal("translation-specific-model", loaded.Ai.Translation.Model);
        Assert.Equal("quick-chat-specific-model", loaded.Ai.QuickChat.Model);
    }

    [Fact]
    public void UnknownSettingsValues_FallBackWithoutLosingOtherSettings()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(GetSettingsPath(),
            """
            {
              "schemaVersion": 1,
              "translationModel": "unknown-model",
              "writingModel": "also-unknown",
              "languageMode": "UnknownDirection",
              "historyLimit": 73,
              "lastUsedTool": "Translation"
            }
            """);

        var loaded = CreateService().Load();

        Assert.Equal(OpenAiModelOptions.DefaultModel, loaded.TranslationModel);
        Assert.Equal(TranslationLanguageMode.Automatic, loaded.LanguageMode);
        Assert.Equal(HistoryLimitOptions.Default, loaded.HistoryLimit);
        Assert.Equal(ToolId.Translation, loaded.LastUsedTool);
    }

    [Fact]
    public void EmptyFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(GetSettingsPath(), string.Empty);

        var loaded = CreateService().Load();

        Assert.Equal(OpenAiModelOptions.DefaultModel, loaded.TranslationModel);
        Assert.Equal(HistoryLimitOptions.Default, loaded.HistoryLimit);
    }

    [Fact]
    public void Load_OldSettingsWithoutCalendarBlockAddsSafeDefaultsWithoutSchemaBump()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(
            GetSettingsPath(),
            """
            {
              "schemaVersion": 1,
              "lastUsedTool": "Notes",
              "historyLimit": 50
            }
            """);

        var loaded = CreateService().Load();

        Assert.Equal(1, AppSettings.CurrentSchemaVersion);
        Assert.Equal(AppSettings.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal(ToolId.Notes, loaded.LastUsedTool);
        Assert.Equal(50, loaded.HistoryLimit);
        Assert.Equal(ApplicationLanguageMode.System, loaded.ApplicationLanguage);
        AssertCalendarDefaults(loaded.Calendar);
    }

    [Fact]
    public void Load_NullCalendarBlockAddsIndependentSafeDefaults()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(
            GetSettingsPath(),
            """
            { "schemaVersion": 1, "calendar": null }
            """);

        var first = CreateService().Load();
        var second = CreateService().Load();

        AssertCalendarDefaults(first.Calendar);
        AssertCalendarDefaults(second.Calendar);
        Assert.NotSame(first.Calendar, second.Calendar);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsExplicitCalendarSettingsWithoutChangingExistingSettings()
    {
        var settings = new AppSettings
        {
            LastUsedTool = ToolId.Notes,
            HistoryLimit = 50,
            Calendar = new CalendarSettings
            {
                Language = CalendarLanguageMode.Hebrew,
                DefaultView = CalendarView.Week,
                FirstDayOfWeek = FirstDayOfWeekMode.Sunday,
                ShowHolidays = false
            }
        };

        Assert.True(CreateService().Save(settings));
        var loaded = CreateService().Load();

        Assert.Equal(CalendarLanguageMode.Hebrew, loaded.Calendar.Language);
        Assert.Equal(CalendarView.Week, loaded.Calendar.DefaultView);
        Assert.Equal(FirstDayOfWeekMode.Sunday,
            loaded.Calendar.FirstDayOfWeek);
        Assert.False(loaded.Calendar.ShowHolidays);
        Assert.Equal(ToolId.Notes, loaded.LastUsedTool);
        Assert.Equal(50, loaded.HistoryLimit);
    }

    [Theory]
    [InlineData(ApplicationLanguageMode.System)]
    [InlineData(ApplicationLanguageMode.English)]
    [InlineData(ApplicationLanguageMode.Hebrew)]
    public void SaveAndLoad_RoundTripsApplicationLanguage(
        ApplicationLanguageMode language)
    {
        var settings = new AppSettings { ApplicationLanguage = language };

        Assert.True(CreateService().Save(settings));
        var loaded = CreateService().Load();

        Assert.Equal(language, loaded.ApplicationLanguage);
        Assert.Equal(1, AppSettings.CurrentSchemaVersion);
    }

    [Theory]
    [InlineData("\"Unknown\"")]
    [InlineData("999")]
    [InlineData("{ \"invalid\": true }")]
    public void Load_InvalidApplicationLanguageNormalizesWithoutDiscardingSettings(
        string invalidValue)
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(
            GetSettingsPath(),
            $$"""
            {
              "schemaVersion": 1,
              "lastUsedTool": "Notes",
              "applicationLanguage": {{invalidValue}}
            }
            """);

        var loaded = CreateService().Load();

        Assert.Equal(ApplicationLanguageMode.System, loaded.ApplicationLanguage);
        Assert.Equal(ToolId.Notes, loaded.LastUsedTool);
    }

    [Theory]
    [InlineData(AppAppearanceMode.System)]
    [InlineData(AppAppearanceMode.Dark)]
    [InlineData(AppAppearanceMode.Light)]
    public void SaveAndLoad_RoundTripsAppearance(AppAppearanceMode appearance)
    {
        var settings = new AppSettings { Appearance = appearance };

        Assert.True(CreateService().Save(settings));
        var loaded = CreateService().Load();

        Assert.Equal(appearance, loaded.Appearance);
    }

    [Fact]
    public void Load_ReturnsDefaultAppearanceOfSystemWhenFileIsMissing()
    {
        var result = CreateService().Load();

        Assert.Equal(AppAppearanceMode.System, result.Appearance);
    }

    [Theory]
    [InlineData("\"Unknown\"")]
    [InlineData("999")]
    [InlineData("{ \"invalid\": true }")]
    public void Load_InvalidAppearanceNormalizesWithoutDiscardingSettings(
        string invalidValue)
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(
            GetSettingsPath(),
            $$"""
            {
              "schemaVersion": 1,
              "lastUsedTool": "Notes",
              "appearance": {{invalidValue}}
            }
            """);

        var loaded = CreateService().Load();

        Assert.Equal(AppAppearanceMode.System, loaded.Appearance);
        Assert.Equal(ToolId.Notes, loaded.LastUsedTool);
    }

    [Fact]
    public void Load_InvalidCalendarEnumsNormalizeWithoutDiscardingOtherSettings()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(
            GetSettingsPath(),
            """
            {
              "schemaVersion": 1,
              "lastUsedTool": "Notes",
              "calendar": {
                "language": "UnknownLanguage",
                "defaultView": 999,
                "firstDayOfWeek": { "invalid": true },
                "showHolidays": false
              }
            }
            """);

        var loaded = CreateService().Load();

        Assert.Equal(ToolId.Notes, loaded.LastUsedTool);
        Assert.Equal(CalendarLanguageMode.UseAppLanguage, loaded.Calendar.Language);
        Assert.Equal(CalendarView.Month, loaded.Calendar.DefaultView);
        Assert.Equal(FirstDayOfWeekMode.System,
            loaded.Calendar.FirstDayOfWeek);
        Assert.False(loaded.Calendar.ShowHolidays);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private SettingsService CreateService() => new(GetSettingsPath());

    private string GetSettingsPath() => Path.Combine(_testDirectory, "settings.json");

    private static void AssertCalendarDefaults(CalendarSettings settings)
    {
        Assert.Equal(CalendarLanguageMode.UseAppLanguage, settings.Language);
        Assert.Equal(CalendarView.Month, settings.DefaultView);
        Assert.Equal(FirstDayOfWeekMode.System, settings.FirstDayOfWeek);
        Assert.True(settings.ShowHolidays);
    }
}
