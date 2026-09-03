using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public interface INoteExportService
{
    Task<bool> ExportAsync(
        NoteDocument note,
        NoteExportFormat format,
        CancellationToken cancellationToken = default);
}
