using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public interface ISavedWordsService
{
    const int MaximumItemCount = 200;

    event EventHandler? Changed;

    IReadOnlyList<SavedWord> Items { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    bool Contains(string sourceText, string primaryTranslation);

    Task<SavedWordAddResult> AddAsync(
        string sourceText,
        string primaryTranslation,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
