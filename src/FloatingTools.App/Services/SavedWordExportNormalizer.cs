using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public static class SavedWordExportNormalizer
{
    public static IReadOnlyList<SavedWordExportRow> Normalize(
        IReadOnlyList<SavedWord> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items.Select(Normalize).ToArray();
    }

    public static SavedWordExportRow Normalize(SavedWord item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (IsEnglish(item.SourceLanguage) && IsHebrew(item.TargetLanguage))
        {
            return new(item.SourceText, item.PrimaryTranslation, item.SavedAt);
        }

        if (IsHebrew(item.SourceLanguage) && IsEnglish(item.TargetLanguage))
        {
            return new(item.PrimaryTranslation, item.SourceText, item.SavedAt);
        }

        // Valid metadata always wins. Detection is only a compatibility fallback
        // for malformed legacy entries that predate normalized export.
        var direction = TranslationDirectionResolver.Resolve(item.SourceText);
        return direction.SourceLanguage == TranslationDirectionResolver.HebrewLanguageCode
            ? new(item.PrimaryTranslation, item.SourceText, item.SavedAt)
            : new(item.SourceText, item.PrimaryTranslation, item.SavedAt);
    }

    private static bool IsEnglish(string language) =>
        language.Equals(TranslationDirectionResolver.EnglishLanguageCode,
            StringComparison.OrdinalIgnoreCase)
        || language.Equals("English", StringComparison.OrdinalIgnoreCase);

    private static bool IsHebrew(string language) =>
        language.Equals(TranslationDirectionResolver.HebrewLanguageCode,
            StringComparison.OrdinalIgnoreCase)
        || language.Equals("Hebrew", StringComparison.OrdinalIgnoreCase);
}
