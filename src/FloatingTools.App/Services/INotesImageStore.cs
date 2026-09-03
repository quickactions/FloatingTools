namespace FloatingTools.App.Services;

public sealed record ManagedNoteImage(
    string AssetFileName,
    string AbsolutePath,
    double NaturalWidth,
    double NaturalHeight);

public interface INotesImageStore
{
    Task<ManagedNoteImage> ImportPngAsync(
        byte[] pngBytes,
        CancellationToken cancellationToken = default);

    Task<ManagedNoteImage> ImportFileAsync(
        string sourcePath,
        CancellationToken cancellationToken = default);

    string GetAbsolutePath(string assetFileName);

    Task DeleteAsync(
        string assetFileName,
        CancellationToken cancellationToken = default);
}
