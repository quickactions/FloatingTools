using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public interface IFrequentWordsStore
{
    Task<IReadOnlyList<FrequentWord>> LoadAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        IReadOnlyList<FrequentWord> items,
        CancellationToken cancellationToken = default);
}
