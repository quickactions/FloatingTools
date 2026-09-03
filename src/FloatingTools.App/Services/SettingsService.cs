using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public sealed class SettingsService : IAppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters =
        {
            new ToolIdJsonConverter(),
            new PanelSizePresetJsonConverter(),
            new TranslationLanguageModeJsonConverter(),
            new NormalizedEnumJsonConverter<ApplicationLanguageMode>(
                ApplicationLanguageMode.System),
            new NormalizedEnumJsonConverter<AppAppearanceMode>(
                AppAppearanceMode.System),
            new NormalizedEnumJsonConverter<CalendarView>(CalendarView.Month),
            new NormalizedEnumJsonConverter<CalendarLanguageMode>(
                CalendarLanguageMode.UseAppLanguage),
            new NormalizedEnumJsonConverter<FirstDayOfWeekMode>(
                FirstDayOfWeekMode.System),
            new JsonStringEnumConverter()
        }
    };

    private readonly string _settingsPath;

    public SettingsService(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _settingsPath = settingsPath;
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return CreateDefaultSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);

            if (settings?.SchemaVersion != AppSettings.CurrentSchemaVersion)
            {
                return CreateDefaultSettings();
            }

            Normalize(settings);
            return settings;
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException)
        {
            return CreateDefaultSettings();
        }
    }

    private static AppSettings CreateDefaultSettings()
    {
        var settings = new AppSettings();
        Normalize(settings);
        return settings;
    }

    private static void Normalize(AppSettings settings)
    {
        settings.TranslationModel = OpenAiModelOptions.Normalize(
            settings.TranslationModel);
        settings.LanguageMode = Enum.IsDefined(settings.LanguageMode)
            ? settings.LanguageMode
            : TranslationLanguageMode.Automatic;
        settings.HistoryLimit = HistoryLimitOptions.Normalize(settings.HistoryLimit);
        settings.ApplicationLanguage = Enum.IsDefined(settings.ApplicationLanguage)
            ? settings.ApplicationLanguage
            : ApplicationLanguageMode.System;
        settings.Appearance = Enum.IsDefined(settings.Appearance)
            ? settings.Appearance
            : AppAppearanceMode.System;
        settings.Ai ??= new AiSettings();
        settings.Ai.DefaultProvider = string.IsNullOrWhiteSpace(settings.Ai.DefaultProvider)
            ? "OpenAI"
            : settings.Ai.DefaultProvider;
        settings.Ai.DefaultModel = string.IsNullOrWhiteSpace(settings.Ai.DefaultModel)
            ? OpenAiModelOptions.DefaultModel
            : settings.Ai.DefaultModel;
        settings.Ai.Translation ??= new AiToolSettings();
        settings.Ai.QuickChat ??= new AiToolSettings();
        settings.Calendar ??= new CalendarSettings();
        settings.Calendar.Language = Enum.IsDefined(settings.Calendar.Language)
            ? settings.Calendar.Language
            : CalendarLanguageMode.UseAppLanguage;
        settings.Calendar.DefaultView = Enum.IsDefined(settings.Calendar.DefaultView)
            ? settings.Calendar.DefaultView
            : CalendarView.Month;
        settings.Calendar.FirstDayOfWeek = Enum.IsDefined(
            settings.Calendar.FirstDayOfWeek)
                ? settings.Calendar.FirstDayOfWeek
                : FirstDayOfWeekMode.System;
        if (!settings.Ai.Translation.IsModelSelectionInitialized
            && settings.Ai.Translation.UseAppCredentials
            && string.IsNullOrWhiteSpace(settings.Ai.Translation.Model))
        {
            settings.Ai.Translation.Model = settings.TranslationModel;
            settings.Ai.Translation.IsModelSelectionInitialized = true;
        }
    }

    public bool Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(_settingsPath);
        var temporaryPath = _settingsPath + ".tmp";

        try
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            settings.SchemaVersion = AppSettings.CurrentSchemaVersion;
            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _settingsPath, overwrite: true);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            TryDeleteTemporaryFile(temporaryPath);
            return false;
        }
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException)
        {
            // A stale temporary file is harmless and can be replaced next time.
        }
    }

    private sealed class ToolIdJsonConverter : JsonConverter<ToolId>
    {
        public override ToolId Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String
                && Enum.TryParse<ToolId>(
                    reader.GetString(),
                    ignoreCase: true,
                    out var tool)
                && Enum.IsDefined(tool))
            {
                return tool;
            }

            if (reader.TokenType == JsonTokenType.Number
                && reader.TryGetInt32(out var numericValue)
                && Enum.IsDefined(typeof(ToolId), numericValue))
            {
                return (ToolId)numericValue;
            }

            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                using var ignoredValue = JsonDocument.ParseValue(ref reader);
            }

            return ToolId.Translation;
        }

        public override void Write(
            Utf8JsonWriter writer,
            ToolId value,
            JsonSerializerOptions options)
        {
            var normalizedValue = Enum.IsDefined(value)
                ? value
                : ToolId.Translation;
            writer.WriteStringValue(normalizedValue.ToString());
        }

    }

    private sealed class PanelSizePresetJsonConverter : JsonConverter<PanelSizePreset>
    {
        public override PanelSizePreset Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String
                && Enum.TryParse<PanelSizePreset>(
                    reader.GetString(),
                    ignoreCase: true,
                    out var preset)
                && Enum.IsDefined(preset))
            {
                return preset;
            }

            if (reader.TokenType == JsonTokenType.Number
                && reader.TryGetInt32(out var numericValue)
                && Enum.IsDefined(typeof(PanelSizePreset), numericValue))
            {
                return (PanelSizePreset)numericValue;
            }

            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                using var ignoredValue = JsonDocument.ParseValue(ref reader);
            }

            return PanelSizePreset.Standard;
        }

        public override void Write(
            Utf8JsonWriter writer,
            PanelSizePreset value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue(
                PanelSizeCalculator.NormalizePreset(value).ToString());
        }
    }

    private sealed class TranslationLanguageModeJsonConverter
        : JsonConverter<TranslationLanguageMode>
    {
        public override TranslationLanguageMode Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String
                && Enum.TryParse<TranslationLanguageMode>(
                    reader.GetString(), true, out var mode)
                && Enum.IsDefined(mode))
            {
                return mode;
            }

            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                using var ignoredValue = JsonDocument.ParseValue(ref reader);
            }

            return TranslationLanguageMode.Automatic;
        }

        public override void Write(
            Utf8JsonWriter writer,
            TranslationLanguageMode value,
            JsonSerializerOptions options) =>
            writer.WriteStringValue(
                Enum.IsDefined(value)
                    ? value.ToString()
                    : TranslationLanguageMode.Automatic.ToString());
    }

    private sealed class NormalizedEnumJsonConverter<TEnum>(TEnum defaultValue)
        : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        public override TEnum Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String
                && Enum.TryParse<TEnum>(reader.GetString(), true, out var parsed)
                && Enum.IsDefined(parsed))
            {
                return parsed;
            }

            if (reader.TokenType == JsonTokenType.Number
                && reader.TryGetInt32(out var numeric)
                && Enum.IsDefined(typeof(TEnum), numeric))
            {
                return (TEnum)Enum.ToObject(typeof(TEnum), numeric);
            }

            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                using var ignoredValue = JsonDocument.ParseValue(ref reader);
            }

            return defaultValue;
        }

        public override void Write(
            Utf8JsonWriter writer,
            TEnum value,
            JsonSerializerOptions options) =>
            writer.WriteStringValue(
                Enum.IsDefined(value) ? value.ToString() : defaultValue.ToString());
    }
}
