using System.Collections.ObjectModel;
using System.IO;
using System.IO.Packaging;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using FloatingTools.App.Controls;
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
    public void Pdf_LongTextStillPaginatesAndKeepsFollowingImage()
    {
        var imagePath = CreatePng("after-text.png", 200, 100);
        var note = new NoteDocument
        {
            Title = "Pagination",
            Blocks = new ObservableCollection<NoteBlock>
            {
                new TextNoteBlock
                {
                    Text = string.Join(Environment.NewLine,
                        Enumerable.Repeat("A readable line of exported note text.", 120))
                },
                new ImageNoteBlock
                {
                    AssetPath = imagePath,
                    NaturalWidth = 200,
                    NaturalHeight = 100,
                    DisplayWidth = 160
                }
            }
        };

        var result = RunInSta(() => NotePdfExporter.Create(note));

        Assert.False(result.HasMissingOrUnreadableImages);
        using var stream = new MemoryStream(result.Content);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        Assert.True(document.Pages.Count > 1);
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

    [Theory]
    [InlineData("זהו טקסט עברי ארוך שמכיל מילים רבות כדי שיישבר לכמה שורות בחלון הייצוא ונוכל לבדוק את מיקום השורה האחרונה בתוך הרוחב הזמין", true)]
    [InlineData("זהו טקסט עברי עם Firefox ועם 123 מספרים וגם כתובת https://example.com כדי ליצור שורות עטופות ולבדוק את המיקום שלהן", true)]
    [InlineData("This English sentence contains enough words to wrap across several lines in the export width and to check where the final line lands", false)]
    public void Pdf_WrappedTextInkStaysOnTheEditorsPhysicalSide(string text, bool rtl)
    {
        RunInSta(() =>
        {
            const int width = 320;
            using var layout = NotePdfTextLayout.Create(text, width, new Typeface("Segoe UI"), 20);
            var bitmap = new RenderTargetBitmap(width, (int)Math.Ceiling(layout.Height) + 2,
                96, 96, PixelFormats.Pbgra32);
            layout.RenderTo(bitmap);

            var lines = InkLineBounds(bitmap);
            Assert.True(lines.Count >= 2);
            var shortLines = lines.Where(line => line.Width < width - 20).ToList();
            Assert.NotEmpty(shortLines);
            Assert.All(shortLines, line =>
            {
                var physicalGutter = rtl ? width - line.X - line.Width : line.X;
                Assert.InRange(physicalGutter, 0, 4);
            });
            return true;
        });
    }
    [Theory]
    [InlineData("English heading and readable body", false)]
    [InlineData("https://example.com/path?q=word", false)]
    [InlineData("שלום עולם בעברית", true)]
    [InlineData("שלום Firefox 123, עולם!", true)]
    public void Pdf_TextRasterPreservesSourceGlyphProportions(string text, bool rtl)
    {
        RunInSta(() =>
        {
            const double width = 480;
            const double fontSize = 16;
            var typeface = new Typeface("Segoe UI");
            using var layout = NotePdfTextLayout.Create(text, width, typeface, fontSize);
            var actual = new RenderTargetBitmap(
                (int)width, (int)Math.Ceiling(layout.Height) + 4,
                96, 96, PixelFormats.Pbgra32);
            layout.RenderTo(actual);

            var source = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                TextAlignment = rtl ? TextAlignment.Right : TextAlignment.Left,
                FontFamily = typeface.FontFamily,
                FontSize = fontSize,
                Foreground = Brushes.Black,
                Language = XmlLanguage.GetLanguage(rtl ? "he-IL" : "en-US")
            };
            source.Inlines.Add(new Run(text));
            source.Measure(new Size(width, double.PositiveInfinity));
            source.Arrange(new Rect(0, 0, source.DesiredSize.Width,
                Math.Max(source.DesiredSize.Height, fontSize * 1.35)));
            var expected = new RenderTargetBitmap(
                (int)width, actual.PixelHeight, 96, 96, PixelFormats.Pbgra32);
            expected.Render(source);

            var sourceBounds = InkBounds(expected);
            var pdfSourceBounds = InkBounds(actual);
            Assert.InRange((double)pdfSourceBounds.Width / sourceBounds.Width, 0.95, 1.05);
            Assert.InRange((double)pdfSourceBounds.Height / sourceBounds.Height, 0.95, 1.05);
            return true;
        });
    }

    private static Int32Rect InkBounds(BitmapSource bitmap)
    {
        var stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        var left = bitmap.PixelWidth;
        var right = -1;
        var top = bitmap.PixelHeight;
        var bottom = -1;
        for (var y = 0; y < bitmap.PixelHeight; y++)
        for (var x = 0; x < bitmap.PixelWidth; x++)
        {
            if (pixels[y * stride + x * 4 + 3] == 0) continue;
            left = Math.Min(left, x);
            right = Math.Max(right, x);
            top = Math.Min(top, y);
            bottom = Math.Max(bottom, y);
        }
        Assert.True(right >= left && bottom >= top);
        return new Int32Rect(left, top, right - left + 1, bottom - top + 1);
    }

    private static IReadOnlyList<Int32Rect> InkLineBounds(BitmapSource bitmap)
    {
        var stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        var lines = new List<Int32Rect>();
        var top = -1;
        var lastInkRow = -1;
        var left = bitmap.PixelWidth;
        var right = -1;
        for (var y = 0; y < bitmap.PixelHeight; y++)
        {
            var rowLeft = bitmap.PixelWidth;
            var rowRight = -1;
            for (var x = 0; x < bitmap.PixelWidth; x++)
            {
                if (pixels[y * stride + x * 4 + 3] <= 64) continue;
                rowLeft = Math.Min(rowLeft, x);
                rowRight = Math.Max(rowRight, x);
            }

            if (rowRight < 0) continue;
            if (top >= 0 && y - lastInkRow > 3)
            {
                lines.Add(new Int32Rect(left, top, right - left + 1, lastInkRow - top + 1));
                top = -1;
                left = bitmap.PixelWidth;
                right = -1;
            }

            if (top < 0) top = y;
            lastInkRow = y;
            left = Math.Min(left, rowLeft);
            right = Math.Max(right, rowRight);
        }

        if (top >= 0)
        {
            lines.Add(new Int32Rect(left, top, right - left + 1, lastInkRow - top + 1));
        }

        return lines;
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
        AssertBodyParagraphMatchesValidatedDirection(content[3], isRtl: true);
        Assert.Empty(content[1].Descendants<W.Hyperlink>());
        Assert.Empty(mainPart.HyperlinkRelationships);
        var extent = Assert.Single(content[2].Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>());
        Assert.Equal(2d, (double)extent.Cx!.Value / extent.Cy!.Value, 2);
        Assert.NotEmpty(mainPart.ImageParts);
        Assert.Equal(before, Snapshot(note));
    }

    [Fact]
    public void Word_UsesA4NormalMarginsArialBodyAndFittedImage()
    {
        var imagePath = CreatePng("large-word-image.png", 1200, 600);
        var note = new NoteDocument
        {
            Title = "Distinct title",
            HasManualTitle = true,
            Blocks = new ObservableCollection<NoteBlock>
            {
                new TextNoteBlock { Text = "English body text" },
                new ImageNoteBlock
                {
                    AssetPath = imagePath,
                    NaturalWidth = 1200,
                    NaturalHeight = 600,
                    DisplayWidth = 1200
                }
            }
        };

        var result = NoteWordExporter.Create(note);

        Assert.False(result.HasMissingOrUnreadableImages);
        using var stream = new MemoryStream(result.Content);
        using var document = WordprocessingDocument.Open(stream, false);
        Assert.Empty(new OpenXmlValidator().Validate(document));
        var body = Assert.IsType<W.Body>(document.MainDocumentPart?.Document?.Body);
        var section = Assert.Single(body.Elements<W.SectionProperties>());
        Assert.Equal((uint)11906, section.GetFirstChild<W.PageSize>()?.Width?.Value);
        Assert.Equal((uint)16838, section.GetFirstChild<W.PageSize>()?.Height?.Value);
        var margins = Assert.IsType<W.PageMargin>(section.GetFirstChild<W.PageMargin>());
        Assert.Equal(1440, margins.Top?.Value);
        Assert.Equal((uint)1440, margins.Right?.Value);
        Assert.Equal(1440, margins.Bottom?.Value);
        Assert.Equal((uint)1440, margins.Left?.Value);

        var paragraphs = body.Elements<W.Paragraph>().ToList();
        Assert.Equal(3, paragraphs.Count);
        Assert.Equal("Distinct title", paragraphs[0].InnerText);
        var titleRun = Assert.Single(paragraphs[0].Elements<W.Run>());
        Assert.NotNull(titleRun.RunProperties?.Bold);
        Assert.Equal("36", titleRun.RunProperties?.FontSize?.Val?.Value);
        Assert.Equal("Segoe UI", titleRun.RunProperties?.RunFonts?.Ascii?.Value);
        Assert.Equal("English body text", paragraphs[1].InnerText);
        AssertBodyParagraphMatchesValidatedDirection(paragraphs[1], isRtl: false);
        var extent = Assert.Single(paragraphs[2].Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>());
        const long contentWidthEmus = (11906L - 2 * 1440L) * 635L;
        Assert.InRange(extent.Cx!.Value, 1, contentWidthEmus);
        Assert.Equal(2d, (double)extent.Cx.Value / extent.Cy!.Value, 2);
    }
    [Theory]
    [InlineData("כותרת Firefox 123!", true)]
    [InlineData("English title שלום 123!", false)]
    public void Word_TitleMatchesEditorDirectionAndPreservesTitleFormatting(string title, bool isRtl)
    {
        var note = new NoteDocument
        {
            Title = title,
            HasManualTitle = true,
            Blocks = new ObservableCollection<NoteBlock>
            {
                new TextNoteBlock { Text = "Body" }
            }
        };

        var result = NoteWordExporter.Create(note);

        using var stream = new MemoryStream(result.Content);
        using var document = WordprocessingDocument.Open(stream, false);
        Assert.Empty(new OpenXmlValidator().Validate(document));
        var body = Assert.IsType<W.Body>(document.MainDocumentPart?.Document?.Body);
        var titleParagraph = body.Elements<W.Paragraph>().First();
        Assert.Equal(title, titleParagraph.InnerText);

        var properties = Assert.IsType<W.ParagraphProperties>(titleParagraph.ParagraphProperties);
        var run = Assert.Single(titleParagraph.Elements<W.Run>());
        var runProperties = Assert.IsType<W.RunProperties>(run.RunProperties);
        if (isRtl)
        {
            Assert.Null(properties.BiDi);
            Assert.Null(properties.Justification);
            Assert.Single(run.Descendants<W.RightToLeftText>());
        }
        else
        {
            Assert.Equal(false, Assert.IsType<W.BiDi>(properties.BiDi).Val?.Value);
            Assert.Equal(W.JustificationValues.Left, properties.Justification?.Val?.Value);
            Assert.Empty(run.Descendants<W.RightToLeftText>());
        }

        Assert.Equal("240", properties.SpacingBetweenLines?.After?.Value);
        Assert.NotNull(runProperties.Bold);
        Assert.Equal("36", runProperties.FontSize?.Val?.Value);
        Assert.Equal("36", runProperties.FontSizeComplexScript?.Val?.Value);
        Assert.Equal("Segoe UI", runProperties.RunFonts?.Ascii?.Value);
        Assert.Equal("Segoe UI", runProperties.RunFonts?.HighAnsi?.Value);
        Assert.Equal("Segoe UI", runProperties.RunFonts?.ComplexScript?.Value);
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
        Assert.All(hebrewParagraphs, paragraph => AssertBodyParagraphMatchesValidatedDirection(paragraph, isRtl: true));
        Assert.Empty(hebrewParagraphs[1].Descendants<W.Hyperlink>());

        var englishParagraph = Assert.Single(
            paragraphs,
            paragraph => paragraph.InnerText.Contains("English text on another block", StringComparison.Ordinal));
        AssertBodyParagraphMatchesValidatedDirection(englishParagraph, isRtl: false);
    }

    [Theory]
    [InlineData("שלום Firefox 123, https://example.com", true)]
    [InlineData("English שלום 123, https://example.com", false)]
    [InlineData("  123, ! ׳ Hello שלום", false)]
    [InlineData("  123, ! ־ Hello שלום", false)]
    [InlineData("é שלום Firefox", true)]
    [InlineData("123?!", false)]
    [InlineData("שלום ראשון\nEnglish second\nשלום שלישי", true)]
    [InlineData("English first\nשלום שני", false)]
    [InlineData("שלום ראשון\n\nEnglish third", true)]
    public void Word_TextBlockParagraphsMatchEditorDirection(string text, bool expectedRtl)
    {
        var editorDirection = RunInSta(() =>
        {
            var editor = new NoteDirectionalTextBox { Text = text };
            Assert.Equal(expectedRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                editor.FlowDirection);
            Assert.Equal(expectedRtl ? TextAlignment.Right : TextAlignment.Left,
                editor.TextAlignment);
            return editor.FlowDirection;
        });
        var note = new NoteDocument
        {
            Blocks = new ObservableCollection<NoteBlock> { new TextNoteBlock { Text = text } }
        };

        var result = NoteWordExporter.Create(note);

        using var stream = new MemoryStream(result.Content);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = Assert.IsType<W.Body>(document.MainDocumentPart?.Document?.Body);
        var paragraphs = body.Elements<W.Paragraph>().Skip(1).ToList();
        var sourceLines = text.Split('\n');
        Assert.Equal(sourceLines, paragraphs.Select(paragraph => paragraph.InnerText));
        Assert.Equal(expectedRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight, editorDirection);
        Assert.All(paragraphs, paragraph => AssertBodyParagraphMatchesValidatedDirection(paragraph, expectedRtl));
    }

    [Fact]
    public void Word_AdjacentTextBlocksKeepIndependentEditorDirections()
    {
        var note = new NoteDocument
        {
            Blocks = new ObservableCollection<NoteBlock>
            {
                new TextNoteBlock { Text = "שלום Firefox 50%\nEnglish continuation" },
                new TextNoteBlock { Text = "English שלום https://example.com" }
            }
        };

        var result = NoteWordExporter.Create(note);

        using var stream = new MemoryStream(result.Content);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = Assert.IsType<W.Body>(document.MainDocumentPart?.Document?.Body);
        var paragraphs = body.Elements<W.Paragraph>().Skip(1).ToList();
        Assert.Equal(3, paragraphs.Count);
        Assert.All(paragraphs.Take(2), paragraph => AssertBodyParagraphMatchesValidatedDirection(paragraph, isRtl: true));
        AssertBodyParagraphMatchesValidatedDirection(paragraphs[2], isRtl: false);
    }

    [Theory]
    [InlineData("׳ Hello שלום\nAnother line", false)]
    [InlineData("é שלום Firefox\nEnglish continuation", true)]
    [InlineData("שלום Firefox 123\nhttps://example.com", true)]
    [InlineData("English שלום\nעברית", false)]
    [InlineData("שלום ראשון\n\nEnglish third", true)]
    public void Pdf_TextBlockLinesMatchEditorDirection(string text, bool expectedRtl)
    {
        RunInSta(() =>
        {
            const double width = 480;
            var editor = new NoteDirectionalTextBox { Text = text };
            Assert.Equal(expectedRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                editor.FlowDirection);
            using var layout = NotePdfTextLayout.Create(text, width, new Typeface("Segoe UI"), 14);
            Assert.Equal(text.Split('\n').Length, layout.Lines.Count);
            Assert.All(layout.Lines, line =>
            {
                Assert.True(line.Width < width);
                if (editor.TextAlignment == TextAlignment.Right)
                {
                    Assert.Equal(width, line.Right, precision: 6);
                }
                else
                {
                    Assert.Equal(0, line.X, precision: 6);
                }
            });
            return true;
        });
    }

    [Fact]
    public void Pdf_ImagePositionIsCenteredInsideContentArea()
    {
        const double contentWidth = 500;
        const double imageWidth = 160;
        var x = NotePdfExporter.CenterImageX(contentWidth, imageWidth);

        Assert.True(x > 0);
        var leftEdge = NotePdfExporter.CenterImageX(contentWidth, contentWidth);
        Assert.Equal(contentWidth / 2, x + imageWidth / 2 - leftEdge, 6);
        Assert.True(x + imageWidth <= leftEdge + contentWidth);
    }

    private static void AssertBodyParagraphMatchesValidatedDirection(W.Paragraph paragraph, bool isRtl)
    {
        var properties = Assert.IsType<W.ParagraphProperties>(paragraph.ParagraphProperties);
        var run = Assert.Single(paragraph.Elements<W.Run>());
        var runProperties = Assert.IsType<W.RunProperties>(run.RunProperties);
        if (isRtl)
        {
            Assert.Null(properties.BiDi);
            Assert.Null(properties.Justification);
            Assert.Single(run.Descendants<W.RightToLeftText>());
        }
        else
        {
            Assert.Equal(false, Assert.IsType<W.BiDi>(properties.BiDi).Val?.Value);
            Assert.Equal(W.JustificationValues.Left, properties.Justification?.Val?.Value);
            Assert.Empty(run.Descendants<W.RightToLeftText>());
        }

        Assert.Equal("Arial", runProperties.RunFonts?.Ascii?.Value);
        Assert.Equal("Arial", runProperties.RunFonts?.HighAnsi?.Value);
        Assert.Equal("Arial", runProperties.RunFonts?.ComplexScript?.Value);
        Assert.Equal("22", runProperties.FontSize?.Val?.Value);
        Assert.Equal("22", runProperties.FontSizeComplexScript?.Val?.Value);
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
