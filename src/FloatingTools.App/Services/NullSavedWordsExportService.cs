using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public sealed class NullSavedWordsExportService : ISavedWordsExportService
{
    public Task<bool> ExportAsync(
        IReadOnlyList<SavedWord> items,
        SavedWordsExportFormat format,
        string title = "Saved Words",
        string fileNameStem = "saved-words",
        bool includeSavedAt = true,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }
}
