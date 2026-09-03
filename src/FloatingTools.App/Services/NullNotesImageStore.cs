namespace FloatingTools.App.Services;

internal sealed class NullNotesImageStore : INotesImageStore
{
    public Task<ManagedNoteImage> ImportPngAsync(byte[] pngBytes, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Notes image storage is not configured.");

    public Task<ManagedNoteImage> ImportFileAsync(string sourcePath, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Notes image storage is not configured.");

    public string GetAbsolutePath(string assetFileName) => string.Empty;

    public Task DeleteAsync(string assetFileName, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
