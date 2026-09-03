using System.IO;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class LocalNotesImageStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "FloatingTools.Tests", Guid.NewGuid().ToString("N"));

    // 1 x 1 PNG. Keeping bytes in the test avoids relying on WPF UI state.
    private static readonly byte[] PngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public async Task ClipboardPng_IsValidatedAndWrittenAsUniqueManagedCopy()
    {
        var store = new LocalNotesImageStore(_directory);

        var first = await store.ImportPngAsync(PngBytes);
        var second = await store.ImportPngAsync(PngBytes);

        Assert.NotEqual(first.AssetFileName, second.AssetFileName);
        Assert.True(File.Exists(first.AbsolutePath));
        Assert.True(first.NaturalWidth > 0);
        Assert.True(first.NaturalHeight > 0);
    }

    [Fact]
    public async Task DraggedFile_IsCopiedAndSurvivesOriginalDeletion()
    {
        Directory.CreateDirectory(_directory);
        var source = Path.Combine(_directory, "original.png");
        await File.WriteAllBytesAsync(source, PngBytes);
        var store = new LocalNotesImageStore(Path.Combine(_directory, "assets"));

        var managed = await store.ImportFileAsync(source);
        File.Delete(source);

        Assert.True(File.Exists(managed.AbsolutePath));
        Assert.Equal(PngBytes, await File.ReadAllBytesAsync(managed.AbsolutePath));
    }

    [Fact]
    public async Task Delete_RemovesOnlyRequestedManagedAsset()
    {
        var store = new LocalNotesImageStore(_directory);
        var first = await store.ImportPngAsync(PngBytes);
        var second = await store.ImportPngAsync(PngBytes);

        await store.DeleteAsync(first.AssetFileName);

        Assert.False(File.Exists(first.AbsolutePath));
        Assert.True(File.Exists(second.AbsolutePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
