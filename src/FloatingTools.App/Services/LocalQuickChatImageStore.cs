using System.IO;
using System.Windows.Media.Imaging;

namespace FloatingTools.App.Services;

public sealed class LocalQuickChatImageStore : IQuickChatImageStore
{
    public const string AssetDirectoryName = "quickchat-assets";

    private readonly string _assetsDirectory;

    public LocalQuickChatImageStore()
        : this(GetDefaultAssetsDirectory())
    {
    }

    public LocalQuickChatImageStore(string assetsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetsDirectory);
        _assetsDirectory = Path.GetFullPath(assetsDirectory);
    }

    public string AssetsDirectory => _assetsDirectory;

    public static string GetDefaultAssetsDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FloatingTools",
            AssetDirectoryName);

    public async Task<ManagedQuickChatImage> ImportFileAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await ImportStreamAsync(source, cancellationToken);
    }

    public async Task<ManagedQuickChatImage> ImportBytesAsync(
        ReadOnlyMemory<byte> imageBytes,
        CancellationToken cancellationToken = default)
    {
        if (imageBytes.IsEmpty)
        {
            throw new InvalidDataException("The image is empty.");
        }

        await using var source = new MemoryStream(imageBytes.ToArray(), writable: false);
        return await ImportStreamAsync(source, cancellationToken);
    }

    public string GetAbsolutePath(string assetFileName)
    {
        ValidateManagedFileName(assetFileName);
        var path = Path.GetFullPath(Path.Combine(_assetsDirectory, assetFileName));
        var rootPrefix = _assetsDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The asset file name must resolve inside Quick Chat managed storage.",
                nameof(assetFileName));
        }

        return path;
    }

    public IReadOnlyList<string> GetManagedAssetFileNames()
    {
        if (!Directory.Exists(_assetsDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(_assetsDirectory, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(IsValidManagedFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public Task DeleteAsync(
        string assetFileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetAbsolutePath(assetFileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private async Task<ManagedQuickChatImage> ImportStreamAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var metadata = ReadMetadata(source);
        source.Position = 0;

        Directory.CreateDirectory(_assetsDirectory);
        var assetFileName = $"{Guid.NewGuid():N}{metadata.Extension}";
        var destinationPath = GetAbsolutePath(assetFileName);
        var temporaryPath = Path.Combine(
            _assetsDirectory,
            $".{assetFileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await source.CopyToAsync(destination, cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, destinationPath);
            return new ManagedQuickChatImage(
                assetFileName,
                metadata.MediaType,
                metadata.Width,
                metadata.Height,
                destinationPath);
        }
        catch
        {
            TryDelete(temporaryPath);
            TryDelete(destinationPath);
            throw;
        }
    }

    private static ImageMetadata ReadMetadata(Stream stream)
    {
        try
        {
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0)
            {
                throw new InvalidDataException("The image has invalid dimensions.");
            }

            return decoder switch
            {
                PngBitmapDecoder => new ImageMetadata(
                    ".png", "image/png", frame.PixelWidth, frame.PixelHeight),
                JpegBitmapDecoder => new ImageMetadata(
                    ".jpg", "image/jpeg", frame.PixelWidth, frame.PixelHeight),
                _ => throw new NotSupportedException(
                    "Only decoded PNG and JPEG images are supported.")
            };
        }
        catch (FileFormatException exception)
        {
            throw new InvalidDataException("The supplied data is not a valid image.", exception);
        }
    }

    private static void ValidateManagedFileName(string assetFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetFileName);
        if (Path.IsPathRooted(assetFileName)
            || !string.Equals(
                assetFileName,
                Path.GetFileName(assetFileName),
                StringComparison.Ordinal)
            || assetFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException(
                "The asset file name must be a managed relative file name.",
                nameof(assetFileName));
        }

        if (!IsValidManagedFileName(assetFileName))
        {
            throw new ArgumentException(
                "The asset file name is not a valid Quick Chat managed image name.",
                nameof(assetFileName));
        }
    }

    private static bool IsValidManagedFileName(string assetFileName)
    {
        var extension = Path.GetExtension(assetFileName);
        var identifier = Path.GetFileNameWithoutExtension(assetFileName);
        return extension is ".png" or ".jpg"
            && Guid.TryParseExact(identifier, "N", out _);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record ImageMetadata(
        string Extension,
        string MediaType,
        double Width,
        double Height);
}
