using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public interface ISavedWordsStore
{
    Task<IReadOnlyList<SavedWord>> LoadAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        IReadOnlyList<SavedWord> items,
        CancellationToken cancellationToken = default);
}
