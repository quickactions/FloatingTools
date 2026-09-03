using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public sealed class InMemoryTranslationHistoryStore : ITranslationHistoryStore
{
    private readonly Lock _syncRoot = new();
    private readonly List<TranslationEntry> _entries = [];

    public Task<IReadOnlyList<TranslationEntry>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            return Task.FromResult<IReadOnlyList<TranslationEntry>>(
                _entries.OrderBy(entry => entry.CreatedAt).ToArray());
        }
    }

    public Task AddOrUpdateAsync(
        TranslationEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            var index = _entries.FindIndex(existing => existing.Id == entry.Id);
            if (index >= 0)
            {
                _entries[index] = entry;
            }
            else
            {
                _entries.Add(entry);
            }
        }

        return Task.CompletedTask;
    }

    public void TrimToLimit(int maximumEntries)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEntries);
        lock (_syncRoot)
        {
            if (_entries.Count <= maximumEntries)
            {
                return;
            }

            var retainedIds = _entries
                .OrderByDescending(entry => entry.CreatedAt)
                .Take(maximumEntries)
                .Select(entry => entry.Id)
                .ToHashSet();
            _entries.RemoveAll(entry => !retainedIds.Contains(entry.Id));
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _entries.Clear();
        }
    }
}
