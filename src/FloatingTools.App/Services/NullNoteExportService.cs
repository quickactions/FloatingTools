using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public sealed class NullNoteExportService : INoteExportService
{
    public Task<bool> ExportAsync(
        NoteDocument note,
        NoteExportFormat format,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
