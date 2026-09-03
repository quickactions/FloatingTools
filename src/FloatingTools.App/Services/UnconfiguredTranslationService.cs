using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public sealed class UnconfiguredTranslationService : ITranslationService
{
    public Task<TranslationResult> TranslateAsync(
        string text,
        string? sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<TranslationResult>(
            new TranslationProviderNotConfiguredException());
    }

    public Task<string?> TranslateAlternativeAsync(
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        string primaryTranslation,
        IReadOnlyList<string> existingAlternatives,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<string?>(
            new TranslationProviderNotConfiguredException());
    }
}
