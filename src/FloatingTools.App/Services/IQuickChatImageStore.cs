namespace FloatingTools.App.Services;

public sealed record ManagedQuickChatImage(
    string AssetFileName,
    string MediaType,
    double Width,
    double Height,
    string AbsolutePath);

public interface IQuickChatImageStore
{
    Task<ManagedQuickChatImage> ImportFileAsync(
        string sourcePath,
        CancellationToken cancellationToken = default);

    Task<ManagedQuickChatImage> ImportBytesAsync(
        ReadOnlyMemory<byte> imageBytes,
        CancellationToken cancellationToken = default);

    string GetAbsolutePath(string assetFileName);

    IReadOnlyList<string> GetManagedAssetFileNames();

    Task DeleteAsync(
        string assetFileName,
        CancellationToken cancellationToken = default);
}
