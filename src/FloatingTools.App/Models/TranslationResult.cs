namespace FloatingTools.App.Models;

public sealed record TranslationResult
{
    public TranslationResult(
        string mainTranslation,
        string? detectedLanguage = null,
        string? provider = null,
        IReadOnlyList<string>? alternativeTranslations = null,
        IReadOnlyList<string>? examples = null,
        string? correctedSourceText = null,
        TranslationCorrectionStatus correctionStatus = TranslationCorrectionStatus.None,
        string? targetLanguage = null)
    {
        if (correctionStatus != TranslationCorrectionStatus.Ambiguous)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(mainTranslation);
        }

        if (correctionStatus == TranslationCorrectionStatus.Confident)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(correctedSourceText);
        }

        MainTranslation = correctionStatus == TranslationCorrectionStatus.Ambiguous
            ? string.Empty
            : mainTranslation;
        DetectedLanguage = detectedLanguage;
        TargetLanguage = targetLanguage;
        Provider = provider;
        AlternativeTranslations = alternativeTranslations ?? [];
        Examples = examples ?? [];
        CorrectionStatus = correctionStatus;
        CorrectedSourceText = correctionStatus != TranslationCorrectionStatus.Confident
            || string.IsNullOrWhiteSpace(correctedSourceText)
            ? null
            : correctedSourceText.Trim();
    }

    public string MainTranslation { get; }

    public string? DetectedLanguage { get; }

    public string? TargetLanguage { get; }

    public string? Provider { get; }

    public IReadOnlyList<string> AlternativeTranslations { get; }

    public IReadOnlyList<string> Examples { get; }

    public string? CorrectedSourceText { get; }

    public TranslationCorrectionStatus CorrectionStatus { get; }
}
