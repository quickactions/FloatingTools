using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public sealed class InMemorySavedWordsStore : ISavedWordsStore
{
    private IReadOnlyList<SavedWord> _items = [];

    public Task<IReadOnlyList<SavedWord>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_items);
    }

    public Task SaveAsync(
        IReadOnlyList<SavedWord> items,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _items = items.ToArray();
        return Task.CompletedTask;
    }
}
