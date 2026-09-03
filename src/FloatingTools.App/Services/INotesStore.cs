using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public interface INotesStore
{
    Task<NotesStorageState> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        NotesStorageState state,
        CancellationToken cancellationToken = default);
}
