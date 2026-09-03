using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public sealed class SavedWordsService(ISavedWordsStore store) : ISavedWordsService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<SavedWord> _items = [];
    private bool _initialized;

    public event EventHandler? Changed;

    public IReadOnlyList<SavedWord> Items => _items.ToArray();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            _items = (await store.LoadAsync(cancellationToken))
                .OrderByDescending(item => item.SavedAt)
                .Take(ISavedWordsService.MaximumItemCount)
                .ToList();
            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Contains(string sourceText, string primaryTranslation) =>
        _items.Any(item => IsSamePair(item, sourceText, primaryTranslation));

    public async Task<SavedWordAddResult> AddAsync(
        string sourceText,
        string primaryTranslation,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceText);
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryTranslation);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLanguage);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLanguage);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_items.Any(item => IsSamePair(item, sourceText, primaryTranslation)))
            {
                return SavedWordAddResult.AlreadyExists;
            }

            if (_items.Count >= ISavedWordsService.MaximumItemCount)
            {
                return SavedWordAddResult.LimitReached;
            }

            var item = new SavedWord
            {
                Id = Guid.NewGuid(),
                SourceText = sourceText,
                PrimaryTranslation = primaryTranslation,
                SourceLanguage = sourceLanguage,
                TargetLanguage = targetLanguage,
                SavedAt = DateTimeOffset.UtcNow
            };
            var previous = _items;
            _items = [item, .. _items];
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
        return SavedWordAddResult.Added;
    }

    public async Task<bool> RemoveAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var existing = _items.FirstOrDefault(item => item.Id == id);
            if (existing is null)
            {
                return false;
            }

            var previous = _items;
            _items = _items.Where(item => item.Id != id).ToList();
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

    private static bool IsSamePair(
        SavedWord item,
        string sourceText,
        string primaryTranslation) =>
        item.SourceText.Equals(sourceText, StringComparison.Ordinal)
        && item.PrimaryTranslation.Equals(primaryTranslation, StringComparison.Ordinal);
}
