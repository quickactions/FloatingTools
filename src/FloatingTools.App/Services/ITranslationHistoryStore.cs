using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public interface ITranslationHistoryStore
{
    Task<IReadOnlyList<TranslationEntry>> LoadAsync(
        CancellationToken cancellationToken = default);

    Task AddOrUpdateAsync(
        TranslationEntry entry,
        CancellationToken cancellationToken = default);

    void TrimToLimit(int maximumEntries);

    void Clear();
}
