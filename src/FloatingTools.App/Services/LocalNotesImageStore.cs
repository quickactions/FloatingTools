using System.IO;
using System.Windows.Media.Imaging;

namespace FloatingTools.App.Services;

public sealed class LocalNotesImageStore(string assetsDirectory) : INotesImageStore
{
    private static readonly HashSet<string> SupportedExtensions = new(
        [".png", ".jpg", ".jpeg", ".bmp"],
        StringComparer.OrdinalIgnoreCase);

    public async Task<ManagedNoteImage> ImportPngAsync(
        byte[] pngBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        if (pngBytes.Length == 0)
        {
            throw new InvalidDataException("The clipboard image is empty.");
        }

        await using var source = new MemoryStream(pngBytes, writable: false);
        var dimensions = ReadDimensions(source);
        source.Position = 0;
        return await WriteManagedCopyAsync(source, ".png", dimensions, cancellationToken);
    }

    public async Task<ManagedNoteImage> ImportFileAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var extension = Path.GetExtension(sourcePath);
        if (!SupportedExtensions.Contains(extension))
        {
            throw new NotSupportedException("Only PNG, JPG/JPEG, and BMP images are supported.");
        }

        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var dimensions = ReadDimensions(source);
        source.Position = 0;
        return await WriteManagedCopyAsync(source, extension.ToLowerInvariant(), dimensions, cancellationToken);
    }

    public string GetAbsolutePath(string assetFileName)
    {
        var safeName = Path.GetFileName(assetFileName);
        return Path.Combine(assetsDirectory, safeName);
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

    private async Task<ManagedNoteImage> WriteManagedCopyAsync(
        Stream source,
        string extension,
        (double Width, double Height) dimensions,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(assetsDirectory);
        var assetFileName = $"{Guid.NewGuid():N}{extension}";
        var destinationPath = GetAbsolutePath(assetFileName);
        try
        {
            await using var destination = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await source.CopyToAsync(destination, cancellationToken);
            await destination.FlushAsync(cancellationToken);
            return new ManagedNoteImage(
                assetFileName,
                destinationPath,
                dimensions.Width,
                dimensions.Height);
        }
        catch
        {
            try
            {
                File.Delete(destinationPath);
            }
            catch
            {
            }

            throw;
        }
    }

    private static (double Width, double Height) ReadDimensions(Stream stream)
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

        var dpiX = double.IsFinite(frame.DpiX) && frame.DpiX > 0 ? frame.DpiX : 96;
        var dpiY = double.IsFinite(frame.DpiY) && frame.DpiY > 0 ? frame.DpiY : 96;
        return (
            frame.PixelWidth * 96d / dpiX,
            frame.PixelHeight * 96d / dpiY);
    }
}
