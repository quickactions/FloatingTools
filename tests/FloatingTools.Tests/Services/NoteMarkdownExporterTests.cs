using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class NoteMarkdownExporterTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), $"FloatingTools-markdown-{Guid.NewGuid():N}");

    public NoteMarkdownExporterTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void TextOnly_ProducesUtf8MarkdownWithTitleFallbackAndNoBom()
    {
        var note = new NoteDocument
        {
            Title = "  ",
            Blocks = new ObservableCollection<NoteBlock>
            {
                new TextNoteBlock { Text = "שלום English 123" }
            }
        };

        var result = NoteMarkdownExporter.Create(note, "Untitled note");

        Assert.False(result.HasMissingOrUnreadableImages);
        Assert.False(result.Content.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.Equal($"# Untitled note{Environment.NewLine}{Environment.NewLine}שלום English 123{Environment.NewLine}",
            Encoding.UTF8.GetString(result.Content));
        Assert.Throws<InvalidDataException>(() => OpenZip(result.Content));
    }

    [Fact]
    public void TextBlocks_PreserveOrderLineBreaksAndEscapeMarkdownSyntax()
    {
        var note = new NoteDocument
        {
            Title = "Title #1",
            Blocks =
            [
                new TextNoteBlock { Text = "# heading\r\n- item\r\n\r\n1. item" },
                new TextNoteBlock { Text = "*literal* [text] `code`" }
            ]
        };

        var markdown = Encoding.UTF8.GetString(NoteMarkdownExporter.Create(note, "Title").Content);

        Assert.StartsWith("# Title \\#1", markdown);
        Assert.Contains("\\# heading  " + Environment.NewLine + "\\- item", markdown);
        Assert.Contains(Environment.NewLine + Environment.NewLine + "1\\. item", markdown);
        Assert.Contains("\\*literal\\* \\[text\\] \\`code\\`", markdown);
        Assert.True(markdown.IndexOf("\\# heading", StringComparison.Ordinal)
            < markdown.IndexOf("\\*literal", StringComparison.Ordinal));
    }

    [Fact]
    public void LinkList_PreservesOrderAndEscapesLabelsAndDestinations()
    {
        var note = new NoteDocument
        {
            Title = "Links",
            Blocks =
            [
                new LinkListNoteBlock
                {
                    Items =
                    [
                        new NoteLinkItem { DisplayName = "A [label]", Url = "https://example.com/a(b)" },
                        new NoteLinkItem { DisplayName = "", Url = "https://example.com/second path" }
                    ]
                }
            ]
        };

        var markdown = Encoding.UTF8.GetString(NoteMarkdownExporter.Create(note, "Links").Content);

        Assert.Contains("- [A \\[label\\]](https://example.com/a\\(b\\))", markdown);
        Assert.Contains("- [https://example.com/second path](https://example.com/second%20path)", markdown);
        Assert.True(markdown.IndexOf("A ", StringComparison.Ordinal)
            < markdown.IndexOf("second path", StringComparison.Ordinal));
    }

    [Fact]
    public void Images_ZipPreservesBlockOrderMarkersReferencesAndOriginalBytes()
    {
        var png = CreateImage("source.png", ImageFormat.Png);
        var jpeg = CreateImage("source.jpeg", ImageFormat.Jpeg);
        var note = new NoteDocument
        {
            Title = "תמונות",
            Blocks =
            [
                new TextNoteBlock { Text = "before" },
                Image(png),
                new TextNoteBlock { Text = "between" },
                Image(jpeg),
                new TextNoteBlock { Text = "after" }
            ]
        };

        var result = NoteMarkdownExporter.Create(note, "תמונות");

        using var archive = OpenZip(result.Content);
        var markdown = ReadText(AssertEntry(archive, "תמונות.md"));
        Assert.Equal(File.ReadAllBytes(png), ReadBytes(AssertEntry(archive, "images/image-001.png")));
        Assert.Equal(File.ReadAllBytes(jpeg), ReadBytes(AssertEntry(archive, "images/image-002.jpeg")));
        Assert.Contains("[תמונה 1]" + Environment.NewLine + "![תמונה 1](images/image-001.png)", markdown);
        Assert.Contains("[תמונה 2]" + Environment.NewLine + "![תמונה 2](images/image-002.jpeg)", markdown);
        Assert.True(Index(markdown, "before") < Index(markdown, "[תמונה 1]"));
        Assert.True(Index(markdown, "[תמונה 1]") < Index(markdown, "between"));
        Assert.True(Index(markdown, "between") < Index(markdown, "[תמונה 2]"));
        Assert.True(Index(markdown, "[תמונה 2]") < Index(markdown, "after"));
        Assert.All(archive.Entries, entry =>
        {
            Assert.DoesNotContain("\\", entry.FullName);
            Assert.DoesNotContain("..", entry.FullName);
            Assert.False(Path.IsPathRooted(entry.FullName));
        });
    }

    [Fact]
    public void Bmp_IsConvertedToReadablePngWithoutChangingDimensions()
    {
        var bmp = CreateImage("source.bmp", ImageFormat.Bmp);
        var note = new NoteDocument { Title = "Bitmap", Blocks = [Image(bmp)] };

        var result = NoteMarkdownExporter.Create(note, "Bitmap");

        using var archive = OpenZip(result.Content);
        var imageBytes = ReadBytes(AssertEntry(archive, "images/image-001.png"));
        using var stream = new MemoryStream(imageBytes);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        Assert.Equal(3, decoder.Frames[0].PixelWidth);
        Assert.Equal(2, decoder.Frames[0].PixelHeight);
        Assert.NotEqual(File.ReadAllBytes(bmp), imageBytes);
    }

    [Fact]
    public void MissingImage_RemainsInPlaceSetsWarningAndStillProducesZip()
    {
        var note = new NoteDocument
        {
            Title = "Missing",
            Blocks =
            [
                new TextNoteBlock { Text = "before" },
                new ImageNoteBlock { AssetPath = Path.Combine(_directory, "missing.png") },
                new TextNoteBlock { Text = "after" }
            ]
        };

        var result = NoteMarkdownExporter.Create(note, "Missing");

        Assert.True(result.HasMissingOrUnreadableImages);
        using var archive = OpenZip(result.Content);
        var markdown = ReadText(AssertEntry(archive, "Missing.md"));
        Assert.Contains("[תמונה 1]" + Environment.NewLine + "> **Image unavailable:** Image 1", markdown);
        Assert.True(Index(markdown, "before") < Index(markdown, "[תמונה 1]"));
        Assert.True(Index(markdown, "[תמונה 1]") < Index(markdown, "after"));
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("images/", StringComparison.Ordinal));
    }

    [Fact]
    public void EmptyNote_ProducesTitleOnlyMarkdown()
    {
        var note = new NoteDocument { Title = "Empty" };

        var markdown = Encoding.UTF8.GetString(NoteMarkdownExporter.Create(note, "Empty").Content);

        Assert.Equal($"# Empty{Environment.NewLine}", markdown);
    }

    [Fact]
    public void InvalidImage_UsesPlaceholderAndDoesNotCreateImageEntry()
    {
        var invalid = Path.Combine(_directory, "invalid.png");
        File.WriteAllText(invalid, "not an image");
        var note = new NoteDocument { Title = "Invalid", Blocks = [Image(invalid)] };

        var result = NoteMarkdownExporter.Create(note, "Invalid");

        Assert.True(result.HasMissingOrUnreadableImages);
        using var archive = OpenZip(result.Content);
        Assert.Contains("> **Image unavailable:** Image 1", ReadText(AssertEntry(archive, "Invalid.md")));
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("images/", StringComparison.Ordinal));
    }
    [Fact]
    public void FileOptions_SelectMarkdownOrZipBeforeDialog()
    {
        var textOnly = new NoteDocument { Blocks = [new TextNoteBlock { Text = "text" }] };
        var withImage = new NoteDocument { Blocks = [new ImageNoteBlock()] };

        var markdown = WindowsNoteExportService.GetFileOptions(textOnly, NoteExportFormat.Markdown);
        var zip = WindowsNoteExportService.GetFileOptions(withImage, NoteExportFormat.Markdown);

        Assert.Equal(("Markdown files (*.md)|*.md", ".md", ".md"),
            (markdown.Filter, markdown.Extension, markdown.Suffix));
        Assert.Equal(("ZIP archives (*.zip)|*.zip", ".zip", ".zip"),
            (zip.Filter, zip.Extension, zip.Suffix));
    }

    [Fact]
    public void ZipMarkdownName_RemovesTraversalFromProvidedStem()
    {
        var image = CreateImage("image.png", ImageFormat.Png);
        var note = new NoteDocument { Blocks = [Image(image)] };

        using var archive = OpenZip(NoteMarkdownExporter.Create(note, "../unsafe").Content);

        Assert.NotNull(archive.GetEntry("unsafe.md"));
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains("..", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); }
        catch { }
    }

    private ImageNoteBlock Image(string path) => new() { AssetPath = path, AssetFileName = Path.GetFileName(path) };

    private string CreateImage(string name, ImageFormat format)
    {
        var path = Path.Combine(_directory, name);
        var pixels = new byte[]
        {
            0, 0, 255, 255, 0, 255, 0, 255, 255, 0, 0, 255,
            255, 255, 255, 255, 0, 0, 0, 255, 128, 128, 128, 255
        };
        var bitmap = BitmapSource.Create(3, 2, 96, 96, PixelFormats.Bgra32, null, pixels, 12);
        BitmapEncoder encoder = format switch
        {
            ImageFormat.Png => new PngBitmapEncoder(),
            ImageFormat.Jpeg => new JpegBitmapEncoder { QualityLevel = 90 },
            ImageFormat.Bmp => new BmpBitmapEncoder(),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }

    private static ZipArchive OpenZip(byte[] content) =>
        new(new MemoryStream(content, writable: false), ZipArchiveMode.Read);

    private static ZipArchiveEntry AssertEntry(ZipArchive archive, string name) =>
        Assert.Single(archive.Entries, entry => entry.FullName == name);

    private static byte[] ReadBytes(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }

    private static string ReadText(ZipArchiveEntry entry) => Encoding.UTF8.GetString(ReadBytes(entry));
    private static int Index(string text, string value) => text.IndexOf(value, StringComparison.Ordinal);

    private enum ImageFormat { Png, Jpeg, Bmp }
}