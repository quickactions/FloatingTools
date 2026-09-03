using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public interface ISavedWordsExportService
{
    Task<bool> ExportAsync(
        IReadOnlyList<SavedWord> items,
        SavedWordsExportFormat format,
        string title = "Saved Words",
        string fileNameStem = "saved-words",
        bool includeSavedAt = true,
        CancellationToken cancellationToken = default);
}
