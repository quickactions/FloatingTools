using System.Collections.ObjectModel;
using System.IO;
using System.IO.Packaging;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using PdfSharp.Pdf.IO;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace FloatingTools.Tests.Services;

public sealed class NoteExportTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), $"FloatingTools-note-export-{Guid.NewGuid():N}");

    public NoteExportTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void Pdf_CreatesReadableMultipurposeDocumentWithoutMutatingNote()
    {
        var imagePath = CreatePng("wide.png", 200, 100);
        var note = CreateMixedNote(imagePath);
        var before = Snapshot(note);

        var result = RunInSta(() => NotePdfExporter.Create(note));

        Assert.False(result.HasMissingOrUnreadableImages);
        using var stream = new MemoryStream(result.Content);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        Assert.NotEmpty(document.Pages);
        Assert.Equal("Export note", document.Info.Title);
        Assert.Equal(before, Snapshot(note));
    }

    [Fact]
    public void Pdf_ImageOnlyAndMissingImageAreHandledGracefully()
    {
        var note = new NoteDocument
        {
            Title = "Image only",
            Blocks = new ObservableCollection<NoteBlock>
            {
                new ImageNoteBlock { AssetPath = Path.Combine(_directory, "missing.png") }
            }
        };

        var result = RunInSta(() => NotePdfExporter.Create(note));

        Assert.True(result.HasMissingOrUnreadableImages);
        Assert.NotEmpty(result.Content);
    }

    [Fact]
    public void Pdf_UsesTheNotesDirectionResolverForHebrewAndEnglishTextBlocks()
    {
        var note = new NoteDocument
        {
            Blocks = new ObservableCollection<NoteBlock>
            {
                new TextNoteBlock { Text = "עברית בשורה הזאת" },
                new TextNoteBlock { Text = "English text on another block" }
            }
        };

        var result = RunInSta(() => NotePdfExporter.Create(note));

        Assert.NotEmpty(result.Content);
        Assert.Equal(System.Windows.TextAlignment.Right,
            ParagraphDirectionResolver.ResolveAlignment("עברית בשורה הזאת"));
        Assert.Equal(System.Windows.TextAlignment.Left,
            ParagraphDirectionResolver.ResolveAlignment("English text on another block"));
    }

    [Fact]
    public void Pdf_TextLayoutPlacesHebrewLinesAgainstTheRightEdgeAndEnglishLinesAtTheLeftEdge()
    {
        RunInSta(() =>
        {
            const double availableWidth = 480;
            using var hebrewLayout = NotePdfTextLayout.Create(
                "אני כותבת טקסט בעברית\nעוד שורה בעברית",
                availableWidth,
                new System.Windows.Media.Typeface("Segoe UI"),
                14);
            using var englishLayout = NotePdfTextLayout.Create(
                "English text on another block",
                availableWidth,
                new System.Windows.Media.Typeface("Segoe UI"),
                14);

            Assert.NotEmpty(hebrewLayout.Lines);
            Assert.All(hebrewLayout.Lines, line =>
            {
                Assert.True(line.X > 0);
                Assert.Equal(availableWidth, line.Right, precision: 6);
            });
            Assert.NotEmpty(englishLayout.Lines);
            Assert.All(englishLayout.Lines, line => Assert.Equal(0, line.X, precision: 6));
            return true;
        });
    }

    [Fact]
    public void Word_CreatesValidOpenXmlWithOrderedPlainTextImageAndRtl()
    {
        var imagePath = CreatePng("wide.png", 200, 100);
        var note = CreateMixedNote(imagePath);
        var before = Snapshot(note);

        var result = NoteWordExporter.Create(note);

        Assert.False(result.HasMissingOrUnreadableImages);
        using var stream = new MemoryStream(result.Content);
        using var document = WordprocessingDocument.Open(stream, false);
        var validationErrors = new OpenXmlValidator().Validate(document).ToList();
        Assert.True(validationErrors.Count == 0, string.Join(Environment.NewLine,
            validationErrors.Select(error => $"{error.Description} Path={error.Path?.XPath}")));
        var mainPart = Assert.IsType<MainDocumentPart>(document.MainDocumentPart);
        var wordDocument = Assert.IsType<W.Document>(mainPart.Document);
        var body = Assert.IsType<W.Body>(wordDocument.Body);
        var content = body.Elements().Where(element => element is W.Paragraph).Cast<W.Paragraph>().ToList();
        Assert.True(content.Count >= 4);
        Assert.Contains("Export note", content[0].InnerText);
        Assert.Contains("Read docs now", content[1].InnerText);
        Assert.NotNull(content[2].Descendants<W.Drawing>().SingleOrDefault());
        Assert.Contains("שלום עולם", content[3].InnerText);
        Assert.NotNull(content[3].ParagraphProperties?.BiDi);
        Assert.Equal(W.JustificationValues.Right, content[3].ParagraphProperties?.Justification?.Val?.Value);
        Assert.Empty(content[1].Descendants<W.Hyperlink>());
        Assert.Empty(mainPart.HyperlinkRelationships);
        var extent = Assert.Single(content[2].Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>());
        Assert.Equal(2d, (double)extent.Cx!.Value / extent.Cy!.Value, 2);
        Assert.NotEmpty(mainPart.ImageParts);
        Assert.Equal(before, Snapshot(note));
    }

    [Fact]
    public void Word_AllowsZeroTextBlocksAndReportsMissingImage()
    {
        var note = new NoteDocument
        {
            Title = "Manual title",
            HasManualTitle = true,
            Blocks = new ObservableCollection<NoteBlock>
            {
                new ImageNoteBlock { AssetPath = Path.Combine(_directory, "missing.png") }
            }
        };

        var result = NoteWordExporter.Create(note);

        Assert.True(result.HasMissingOrUnreadableImages);
        using var stream = new MemoryStream(result.Content);
        using var document = WordprocessingDocument.Open(stream, false);
        var validationErrors = new OpenXmlValidator().Validate(document).ToList();
        Assert.True(validationErrors.Count == 0, string.Join(Environment.NewLine,
            validationErrors.Select(error => $"{error.Description} Path={error.Path?.XPath}")));
        var mainPart = Assert.IsType<MainDocumentPart>(document.MainDocumentPart);
        var wordDocument = Assert.IsType<W.Document>(mainPart.Document);
        var body = Assert.IsType<W.Body>(wordDocument.Body);
        Assert.Contains("Image unavailable", body.InnerText);
    }

    [Fact]
    public void Word_AppliesTheTextBlockDirectionToEveryPlainTextParagraph()
    {
        const string hebrewText = "עברית בשורה הזאת\nקישור";
        var note = new NoteDocument
        {
            Blocks = new ObservableCollection<NoteBlock>
            {
                new TextNoteBlock { Text = hebrewText },
                new TextNoteBlock { Text = "English text on another block" }
            }
        };

        var result = NoteWordExporter.Create(note);

        using var stream = new MemoryStream(result.Content);
        using var document = WordprocessingDocument.Open(stream, false);
        var mainPart = Assert.IsType<MainDocumentPart>(document.MainDocumentPart);
        var wordDocument = Assert.IsType<W.Document>(mainPart.Document);
        var body = Assert.IsType<W.Body>(wordDocument.Body);
        var paragraphs = body.Elements<W.Paragraph>().ToList();

        var hebrewParagraphs = paragraphs
            .Where(paragraph => paragraph.InnerText.Contains("עברית", StringComparison.Ordinal)
                || paragraph.InnerText.Contains("קישור", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(2, hebrewParagraphs.Count);
        Assert.All(hebrewParagraphs, paragraph =>
        {
            Assert.NotNull(paragraph.ParagraphProperties?.BiDi);
            Assert.Equal(
                W.JustificationValues.Right,
                paragraph.ParagraphProperties?.Justification?.Val?.Value);
        });
        Assert.Empty(hebrewParagraphs[1].Descendants<W.Hyperlink>());

        var englishParagraph = Assert.Single(
            paragraphs,
            paragraph => paragraph.InnerText.Contains("English text on another block", StringComparison.Ordinal));
        Assert.Null(englishParagraph.ParagraphProperties?.BiDi);
        Assert.Equal(
            W.JustificationValues.Left,
            englishParagraph.ParagraphProperties?.Justification?.Val?.Value);
    }

    private NoteDocument CreateMixedNote(string imagePath) => new()
    {
        Title = "Export note",
        Blocks = new ObservableCollection<NoteBlock>
        {
            new TextNoteBlock { Text = "Read docs now" },
            new ImageNoteBlock
            {
                AssetFileName = Path.GetFileName(imagePath),
                AssetPath = imagePath,
                NaturalWidth = 200,
                NaturalHeight = 100,
                DisplayWidth = 160
            },
            new TextNoteBlock { Text = "שלום עולם" }
        }
    };

    private string CreatePng(string name, int width, int height)
    {
        var path = Path.Combine(_directory, name);
        RunInSta(() =>
        {
            var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(path);
            encoder.Save(stream);
            return true;
        });
        return path;
    }

    private static string Snapshot(NoteDocument note) => string.Join("|", note.Blocks.Select(block =>
        block switch
        {
            TextNoteBlock text => $"T:{text.Text}",
            ImageNoteBlock image => $"I:{image.AssetPath}:{image.DisplayWidth}",
            _ => block.GetType().Name
        }));

    private static T RunInSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception exception)
            {
                error = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
        {
            throw error;
        }

        return result!;
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }
}
