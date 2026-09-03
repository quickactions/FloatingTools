using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class LocalQuickChatImageStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "FloatingTools.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ImportPngBytes_CreatesOneManagedAssetWithDecodedMetadata()
    {
        var store = CreateStore();

        var managed = await store.ImportBytesAsync(CreateImageBytes("png", 3, 2));

        Assert.Equal("image/png", managed.MediaType);
        Assert.Equal(3, managed.Width);
        Assert.Equal(2, managed.Height);
        Assert.Equal(".png", Path.GetExtension(managed.AssetFileName));
        Assert.True(File.Exists(managed.AbsolutePath));
        Assert.Single(Directory.GetFiles(store.AssetsDirectory));
    }

    [Fact]
    public async Task ImportJpegFile_CreatesOneManagedAssetWithDecodedMetadata()
    {
        Directory.CreateDirectory(_directory);
        var sourcePath = Path.Combine(_directory, "camera-upload.jpeg");
        await File.WriteAllBytesAsync(sourcePath, CreateImageBytes("jpeg", 4, 5));
        var store = CreateStore();

        var managed = await store.ImportFileAsync(sourcePath);

        Assert.Equal("image/jpeg", managed.MediaType);
        Assert.Equal(4, managed.Width);
        Assert.Equal(5, managed.Height);
        Assert.Equal(".jpg", Path.GetExtension(managed.AssetFileName));
        Assert.True(File.Exists(managed.AbsolutePath));
        Assert.Single(Directory.GetFiles(store.AssetsDirectory));
    }

    [Fact]
    public async Task TwoImports_UseDistinctRelativeManagedNames()
    {
        var store = CreateStore();
        var bytes = CreateImageBytes("png", 1, 1);

        var first = await store.ImportBytesAsync(bytes);
        var second = await store.ImportBytesAsync(bytes);

        Assert.NotEqual(first.AssetFileName, second.AssetFileName);
        Assert.False(Path.IsPathRooted(first.AssetFileName));
        Assert.DoesNotContain(Path.DirectorySeparatorChar, first.AssetFileName);
        Assert.DoesNotContain(Path.AltDirectorySeparatorChar, first.AssetFileName);
        Assert.Equal(2, Directory.GetFiles(store.AssetsDirectory).Length);
    }

    [Fact]
    public async Task ImportFile_DetectsContentAndDoesNotTrustExternalNameOrExtension()
    {
        Directory.CreateDirectory(_directory);
        var sourcePath = Path.Combine(_directory, "customer-selected-name.jpg");
        await File.WriteAllBytesAsync(sourcePath, CreateImageBytes("png", 2, 2));
        var store = CreateStore();

        var managed = await store.ImportFileAsync(sourcePath);

        Assert.NotEqual(Path.GetFileName(sourcePath), managed.AssetFileName);
        Assert.EndsWith(".png", managed.AssetFileName, StringComparison.Ordinal);
        Assert.Equal("image/png", managed.MediaType);
    }

    [Fact]
    public async Task InvalidInput_IsRejectedWithoutLeavingManagedOrPartialFiles()
    {
        var store = CreateStore();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            store.ImportBytesAsync("not an image"u8.ToArray()));

        Assert.False(Directory.Exists(store.AssetsDirectory)
            && Directory.EnumerateFileSystemEntries(store.AssetsDirectory).Any());
    }

    [Fact]
    public async Task UnsupportedDecodedFormat_IsRejectedWithoutLeavingFiles()
    {
        var store = CreateStore();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            store.ImportBytesAsync(CreateImageBytes("bmp", 2, 2)));

        Assert.False(Directory.Exists(store.AssetsDirectory)
            && Directory.EnumerateFileSystemEntries(store.AssetsDirectory).Any());
    }

    [Fact]
    public async Task Resolve_ReturnsImportedAssetPathInsideManagedRoot()
    {
        var store = CreateStore();
        var managed = await store.ImportBytesAsync(CreateImageBytes("png", 1, 1));

        var resolved = store.GetAbsolutePath(managed.AssetFileName);

        Assert.Equal(managed.AbsolutePath, resolved);
        Assert.StartsWith(
            store.AssetsDirectory + Path.DirectorySeparatorChar,
            resolved,
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("..\\outside.png")]
    [InlineData("folder/image.png")]
    [InlineData("folder\\image.png")]
    [InlineData("ordinary-name.png")]
    public void Resolve_RejectsTraversalSeparatorsAndNonManagedNames(string assetFileName)
    {
        var store = CreateStore();

        Assert.Throws<ArgumentException>(() => store.GetAbsolutePath(assetFileName));
    }

    [Fact]
    public void Resolve_RejectsAbsolutePath()
    {
        var store = CreateStore();
        var absolutePath = Path.Combine(_directory, $"{Guid.NewGuid():N}.png");

        Assert.Throws<ArgumentException>(() => store.GetAbsolutePath(absolutePath));
    }

    [Fact]
    public async Task Delete_RemovesOnlyRequestedManagedAsset()
    {
        var store = CreateStore();
        var first = await store.ImportBytesAsync(CreateImageBytes("png", 1, 1));
        var second = await store.ImportBytesAsync(CreateImageBytes("jpeg", 1, 1));

        await store.DeleteAsync(first.AssetFileName);

        Assert.False(File.Exists(first.AbsolutePath));
        Assert.True(File.Exists(second.AbsolutePath));
    }

    [Fact]
    public async Task Delete_MissingManagedAsset_IsIdempotent()
    {
        var store = CreateStore();
        var missing = $"{Guid.NewGuid():N}.png";

        await store.DeleteAsync(missing);
        await store.DeleteAsync(missing);

        Assert.False(File.Exists(store.GetAbsolutePath(missing)));
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("..\\outside.png")]
    public async Task Delete_RejectsTraversal(string assetFileName)
    {
        var store = CreateStore();

        await Assert.ThrowsAsync<ArgumentException>(() => store.DeleteAsync(assetFileName));
    }

    [Fact]
    public async Task Delete_RejectsAbsolutePath()
    {
        var store = CreateStore();
        var absolutePath = Path.Combine(_directory, $"{Guid.NewGuid():N}.png");

        await Assert.ThrowsAsync<ArgumentException>(() => store.DeleteAsync(absolutePath));
    }

    [Fact]
    public async Task Assets_ArePhysicallySeparateFromNotesAssets()
    {
        var quickChatDirectory = Path.Combine(_directory, "quickchat-assets");
        var notesDirectory = Path.Combine(_directory, "notes-assets");
        Directory.CreateDirectory(notesDirectory);
        var notesSentinel = Path.Combine(notesDirectory, "existing-note-image.png");
        await File.WriteAllTextAsync(notesSentinel, "notes");
        var store = new LocalQuickChatImageStore(quickChatDirectory);

        var managed = await store.ImportBytesAsync(CreateImageBytes("png", 1, 1));

        Assert.Equal(quickChatDirectory, Path.GetDirectoryName(managed.AbsolutePath));
        Assert.Equal("notes", await File.ReadAllTextAsync(notesSentinel));
        Assert.Single(Directory.GetFiles(notesDirectory));
    }

    [Fact]
    public async Task ImageOperations_DoNotModifyConversationOrOtherProductData()
    {
        Directory.CreateDirectory(_directory);
        var sentinels = new[]
        {
            "quickchat.json",
            "notes.json",
            "settings.json",
            "translation-history.json"
        };
        foreach (var fileName in sentinels)
        {
            await File.WriteAllTextAsync(Path.Combine(_directory, fileName), fileName);
        }

        var store = CreateStore();
        var managed = await store.ImportBytesAsync(CreateImageBytes("png", 1, 1));
        await store.DeleteAsync(managed.AssetFileName);

        foreach (var fileName in sentinels)
        {
            Assert.Equal(
                fileName,
                await File.ReadAllTextAsync(Path.Combine(_directory, fileName)));
        }
    }

    [Fact]
    public async Task Enumerate_ReturnsOnlyValidatedManagedImageNames()
    {
        var store = CreateStore();
        var png = await store.ImportBytesAsync(CreateImageBytes("png", 1, 1));
        var jpeg = await store.ImportBytesAsync(CreateImageBytes("jpeg", 1, 1));
        await File.WriteAllTextAsync(
            Path.Combine(store.AssetsDirectory, "ordinary-name.png"),
            "not managed");
        await File.WriteAllTextAsync(
            Path.Combine(store.AssetsDirectory, $".{Guid.NewGuid():N}.png.tmp"),
            "partial");

        var managedNames = store.GetManagedAssetFileNames();

        Assert.Equal(
            new[] { png.AssetFileName, jpeg.AssetFileName }
                .Order(StringComparer.Ordinal),
            managedNames);
    }

    [Fact]
    public void DefaultDirectory_IsDedicatedQuickChatAssetsFolderUnderFloatingTools()
    {
        var path = LocalQuickChatImageStore.GetDefaultAssetsDirectory();

        Assert.Equal(LocalQuickChatImageStore.AssetDirectoryName, Path.GetFileName(path));
        Assert.Equal("FloatingTools", Path.GetFileName(Path.GetDirectoryName(path)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private LocalQuickChatImageStore CreateStore() =>
        new(Path.Combine(_directory, "quickchat-assets"));

    private static byte[] CreateImageBytes(
        string format,
        int width,
        int height)
    {
        const int bytesPerPixel = 4;
        var pixels = new byte[width * height * bytesPerPixel];
        Array.Fill(pixels, (byte)0x80);
        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            width * bytesPerPixel);
        BitmapEncoder encoder = format switch
        {
            "png" => new PngBitmapEncoder(),
            "jpeg" => new JpegBitmapEncoder(),
            "bmp" => new BmpBitmapEncoder(),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
