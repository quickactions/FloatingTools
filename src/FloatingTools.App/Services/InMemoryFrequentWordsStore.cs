using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public sealed class InMemoryFrequentWordsStore : IFrequentWordsStore
{
    private IReadOnlyList<FrequentWord> _items = [];

    public Task<IReadOnlyList<FrequentWord>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_items);
    }

    public Task SaveAsync(
        IReadOnlyList<FrequentWord> items,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _items = items.ToArray();
        return Task.CompletedTask;
    }
}
