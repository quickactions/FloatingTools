using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public sealed class FrequentWordsService(IFrequentWordsStore store)
    : IFrequentWordsService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<FrequentWord> _items = [];
    private bool _initialized;

    public event EventHandler? Changed;

    public IReadOnlyList<FrequentWord> Items => _items.ToArray();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            var loaded = await store.LoadAsync(cancellationToken);
            var canonicalized = loaded
                .Select(Canonicalize)
                .ToArray();
            _items = canonicalized
                .GroupBy(
                    item => item.CanonicalPairKey ?? $"legacy:{item.Id:N}",
                    StringComparer.Ordinal)
                .Select(MergeCanonicalGroup)
                .OrderByDescending(item => item.UsageCount)
                .ThenByDescending(item => item.LastUsedAt)
                .ThenBy(item => item.NormalizedSourceKey, StringComparer.Ordinal)
                .ToList();
            if (!loaded.SequenceEqual(_items, FrequentWordValueComparer.Instance))
            {
                try
                {
                    await store.SaveAsync(_items, cancellationToken);
                }
                catch
                {
                    // Migration remains available in memory even if the file
                    // cannot be rewritten during this run.
                }
            }

            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> RecordSuccessfulTranslationAsync(
        string sourceText,
        string primaryTranslation,
        string sourceLanguage,
        string targetLanguage,
        DateTimeOffset? usedAt = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryTranslation);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLanguage);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLanguage);
        if (!FrequentWordNormalizer.TryCreateCanonicalPair(
                sourceText,
                primaryTranslation,
                sourceLanguage,
                targetLanguage,
                out var pair))
        {
            return false;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var previous = _items;
            var existing = _items.FirstOrDefault(item =>
                item.CanonicalPairKey?.Equals(
                    pair.PairKey,
                    StringComparison.Ordinal) == true);
            var updated = new FrequentWord
            {
                Id = existing?.Id ?? Guid.NewGuid(),
                NormalizedSourceKey = pair.EnglishKey,
                CanonicalPairKey = pair.PairKey,
                SourceText = pair.English,
                PrimaryTranslation = pair.Hebrew,
                SourceLanguage = TranslationDirectionResolver.EnglishLanguageCode,
                TargetLanguage = TranslationDirectionResolver.HebrewLanguageCode,
                UsageCount = (existing?.UsageCount ?? 0) + 1,
                LastUsedAt = usedAt ?? DateTimeOffset.UtcNow
            };
            _items = existing is null
                ? [updated, .. _items]
                : _items.Select(item => item.Id == existing.Id ? updated : item).ToList();

            try
            {
                await store.SaveAsync(_items, cancellationToken);
            }
            catch
            {
                _items = previous;
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private static FrequentWord Canonicalize(FrequentWord item)
    {
        if (!FrequentWordNormalizer.TryCreateCanonicalPair(
                item.SourceText,
                item.PrimaryTranslation,
                item.SourceLanguage,
                item.TargetLanguage,
                out var pair))
        {
            return item;
        }

        return new FrequentWord
        {
            Id = item.Id,
            NormalizedSourceKey = pair.EnglishKey,
            CanonicalPairKey = pair.PairKey,
            SourceText = pair.English,
            PrimaryTranslation = pair.Hebrew,
            SourceLanguage = TranslationDirectionResolver.EnglishLanguageCode,
            TargetLanguage = TranslationDirectionResolver.HebrewLanguageCode,
            UsageCount = item.UsageCount,
            LastUsedAt = item.LastUsedAt
        };
    }

    private static FrequentWord MergeCanonicalGroup(
        IGrouping<string, FrequentWord> group)
    {
        var mostRecent = group
            .OrderByDescending(item => item.LastUsedAt)
            .ThenBy(item => item.Id)
            .First();
        var combinedCount = (int)Math.Min(
            int.MaxValue,
            group.Sum(item => (long)item.UsageCount));
        return new FrequentWord
        {
            Id = mostRecent.Id,
            NormalizedSourceKey = mostRecent.NormalizedSourceKey,
            CanonicalPairKey = mostRecent.CanonicalPairKey,
            SourceText = mostRecent.SourceText,
            PrimaryTranslation = mostRecent.PrimaryTranslation,
            SourceLanguage = mostRecent.SourceLanguage,
            TargetLanguage = mostRecent.TargetLanguage,
            UsageCount = combinedCount,
            LastUsedAt = group.Max(item => item.LastUsedAt)
        };
    }

    private sealed class FrequentWordValueComparer : IEqualityComparer<FrequentWord>
    {
        public static FrequentWordValueComparer Instance { get; } = new();

        public bool Equals(FrequentWord? x, FrequentWord? y) =>
            ReferenceEquals(x, y)
            || x is not null
            && y is not null
            && x.Id == y.Id
            && x.NormalizedSourceKey == y.NormalizedSourceKey
            && x.CanonicalPairKey == y.CanonicalPairKey
            && x.SourceText == y.SourceText
            && x.PrimaryTranslation == y.PrimaryTranslation
            && x.SourceLanguage == y.SourceLanguage
            && x.TargetLanguage == y.TargetLanguage
            && x.UsageCount == y.UsageCount
            && x.LastUsedAt == y.LastUsedAt;

        public int GetHashCode(FrequentWord obj) => obj.Id.GetHashCode();
    }
}
