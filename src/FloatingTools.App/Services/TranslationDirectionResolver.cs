using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public static class TranslationDirectionResolver
{
    public const string HebrewLanguageCode = "he";
    public const string EnglishLanguageCode = "en";

    public static TranslationDirection Resolve(string text)
        => Resolve(text, TranslationLanguageMode.Automatic);

    public static TranslationDirection Resolve(
        string text,
        TranslationLanguageMode languageMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        return languageMode switch
        {
            TranslationLanguageMode.HebrewToEnglish =>
                new TranslationDirection(HebrewLanguageCode, EnglishLanguageCode),
            TranslationLanguageMode.EnglishToHebrew =>
                new TranslationDirection(EnglishLanguageCode, HebrewLanguageCode),
            _ => ContainsHebrewLetter(text)
                ? new TranslationDirection(HebrewLanguageCode, EnglishLanguageCode)
                : new TranslationDirection(EnglishLanguageCode, HebrewLanguageCode)
        };
    }

    public static bool ContainsHebrewLetter(string? text) =>
        !string.IsNullOrEmpty(text) && text.Any(IsHebrewLetter);

    private static bool IsHebrewLetter(char character) =>
        char.IsLetter(character)
        && (character is >= '\u0590' and <= '\u05FF'
            or >= '\uFB1D' and <= '\uFB4F');
}
