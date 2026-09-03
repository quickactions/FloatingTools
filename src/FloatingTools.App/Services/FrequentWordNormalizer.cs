using System.Text;

namespace FloatingTools.App.Services;

public static class FrequentWordNormalizer
{
    private const string BoundaryPunctuation = "\"'“”‘’()[]{}<>,!?;:…";

    public static bool TryNormalizeSingleWord(
        string sourceText,
        string sourceLanguage,
        out string normalizedKey,
        out string displayText)
    {
        normalizedKey = string.Empty;
        displayText = string.Empty;
        if (string.IsNullOrWhiteSpace(sourceText)
            || sourceText.IndexOfAny(['\r', '\n']) >= 0)
        {
            return false;
        }

        if (!TryNormalizeText(
                sourceText,
                sourceLanguage,
                requireSingleWord: true,
                out normalizedKey,
                out displayText))
        {
            return false;
        }

        return true;
    }

    public static bool TryCreateCanonicalPair(
        string sourceText,
        string primaryTranslation,
        string sourceLanguage,
        string targetLanguage,
        out CanonicalFrequentWordPair pair)
    {
        pair = default;
        if (!TryNormalizeSingleWord(
                sourceText,
                sourceLanguage,
                out var sourceKey,
                out var sourceDisplay)
            || !TryNormalizeText(
                primaryTranslation,
                targetLanguage,
                requireSingleWord: false,
                out var translationKey,
                out var translationDisplay))
        {
            return false;
        }

        if (IsEnglish(sourceLanguage) && IsHebrew(targetLanguage))
        {
            pair = CreatePair(
                sourceKey,
                sourceDisplay,
                translationKey,
                translationDisplay);
            return true;
        }

        if (IsHebrew(sourceLanguage) && IsEnglish(targetLanguage))
        {
            pair = CreatePair(
                translationKey,
                translationDisplay,
                sourceKey,
                sourceDisplay);
            return true;
        }

        return false;
    }

    private static bool TryNormalizeText(
        string text,
        string language,
        bool requireSingleWord,
        out string normalizedKey,
        out string displayText)
    {
        normalizedKey = string.Empty;
        displayText = string.Empty;
        if (string.IsNullOrWhiteSpace(text)
            || (requireSingleWord && text.IndexOfAny(['\r', '\n']) >= 0))
        {
            return false;
        }

        var value = TrimBoundaryPunctuation(
            text.Trim().Normalize(NormalizationForm.FormC));
        if (value.Length == 0
            || (requireSingleWord && value.Any(char.IsWhiteSpace)))
        {
            return false;
        }

        displayText = value;
        normalizedKey = IsEnglish(language)
            ? value.ToLowerInvariant()
            : value;
        return true;
    }

    private static CanonicalFrequentWordPair CreatePair(
        string englishKey,
        string englishDisplay,
        string hebrewKey,
        string hebrewDisplay) =>
        new(
            $"{englishKey.Length}:{englishKey}{hebrewKey}",
            englishKey,
            hebrewKey,
            englishDisplay,
            hebrewDisplay);

    private static string TrimBoundaryPunctuation(string value)
    {
        var start = 0;
        var end = value.Length - 1;
        while (start <= end && BoundaryPunctuation.Contains(value[start]))
        {
            start++;
        }

        while (end >= start
               && (BoundaryPunctuation.Contains(value[end])
                   || value[end] == '.'))
        {
            end--;
        }

        return start > end ? string.Empty : value[start..(end + 1)];
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

public readonly record struct CanonicalFrequentWordPair(
    string PairKey,
    string EnglishKey,
    string HebrewKey,
    string English,
    string Hebrew);
