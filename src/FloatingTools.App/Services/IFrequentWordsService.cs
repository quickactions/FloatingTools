using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public interface IFrequentWordsService
{
    event EventHandler? Changed;

    IReadOnlyList<FrequentWord> Items { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<bool> RecordSuccessfulTranslationAsync(
        string sourceText,
        string primaryTranslation,
        string sourceLanguage,
        string targetLanguage,
        DateTimeOffset? usedAt = null,
        CancellationToken cancellationToken = default);
}
