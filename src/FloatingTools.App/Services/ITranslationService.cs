using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public interface ITranslationService
{
    Task<TranslationResult> TranslateAsync(
        string text,
        string? sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken);

    Task<TranslationResult> TranslateEnglishWordInContextAsync(
        string word,
        string context,
        CancellationToken cancellationToken) =>
        TranslateAsync(word, "en", "he", cancellationToken);
    Task<string?> TranslateAlternativeAsync(
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        string primaryTranslation,
        IReadOnlyList<string> existingAlternatives,
        CancellationToken cancellationToken);
}
