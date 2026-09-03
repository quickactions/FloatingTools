using System.IO;
using System.Windows.Media.Imaging;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using FloatingTools.App.Models;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace FloatingTools.App.Services;

public static class NoteWordExporter
{
    private const long EmusPerDip = 9525;
    private const uint PageWidthTwips = 11906;
    private const uint PageHeightTwips = 16838;
    private const uint PageMarginTwips = 1134;
    private const long PrintableWidthEmus = (PageWidthTwips - (PageMarginTwips * 2L)) * 635L;

    public static NoteExportResult Create(NoteDocument note)
    {
        ArgumentNullException.ThrowIfNull(note);
        using var output = new MemoryStream();
        var hadImageError = false;
        using (var document = WordprocessingDocument.Create(
                   output,
                   WordprocessingDocumentType.Document,
                   autoSave: true))
        {
            var mainPart = document.AddMainDocumentPart();
            var body = new W.Body();
            mainPart.Document = new W.Document(body);
            body.Append(CreateTitleParagraph(GetTitle(note)));

            uint imageId = 1;
            foreach (var block in note.Blocks)
            {
                switch (block)
                {
                    case TextNoteBlock text:
                        var isRtl = ParagraphDirectionResolver.Resolve(text.Text)
                            == System.Windows.FlowDirection.RightToLeft;
                        foreach (var slice in EnumerateParagraphs(text.Text))
                        {
                            body.Append(CreateTextParagraph(mainPart, text, slice, isRtl));
                        }
                        break;

                    case ImageNoteBlock image:
                        if (TryCreateImageParagraph(mainPart, image, imageId++, out var paragraph))
                        {
                            body.Append(paragraph);
                        }
                        else
                        {
                            hadImageError = true;
                            body.Append(CreateImagePlaceholder());
                        }
                        break;

                    case LinkListNoteBlock links when links.Items.Count > 0:
                        var linkText = string.Join(Environment.NewLine, links.Items.Select(item => item.VisibleName));
                        foreach (var slice in EnumerateParagraphs(linkText))
                        {
                            var text = new TextNoteBlock { Text = linkText };
                            body.Append(CreateTextParagraph(mainPart, text, slice, isRtl: false));
                        }
                        break;
                }
            }

            body.Append(new W.SectionProperties(
                new W.PageSize { Width = PageWidthTwips, Height = PageHeightTwips },
                new W.PageMargin
                {
                    Top = (int)PageMarginTwips,
                    Right = PageMarginTwips,
                    Bottom = (int)PageMarginTwips,
                    Left = PageMarginTwips,
                    Header = 0,
                    Footer = 0,
                    Gutter = 0
                }));
            mainPart.Document.Save();
        }

        return new NoteExportResult(output.ToArray(), hadImageError);
    }

    private static W.Paragraph CreateTitleParagraph(string title)
    {
        var isRtl = ParagraphDirectionResolver.Resolve(title) == System.Windows.FlowDirection.RightToLeft;
        var properties = CreateParagraphProperties(isRtl, afterTwips: 240);
        var runProperties = CreateRunProperties(isRtl, isBold: true, fontSize: "36");
        return new W.Paragraph(
            properties,
            new W.Run(runProperties, CreateText(title)));
    }

    private static W.Paragraph CreateTextParagraph(
        MainDocumentPart mainPart,
        TextNoteBlock block,
        ParagraphSlice slice,
        bool isRtl)
    {
        var paragraph = new W.Paragraph(CreateParagraphProperties(isRtl, afterTwips: 120));
        AppendPlainRun(paragraph, block.Text, slice.Start, slice.End - slice.Start, isRtl);
        if (!paragraph.Elements<W.Run>().Any())
        {
            paragraph.Append(new W.Run(CreateRunProperties(isRtl), CreateText(string.Empty)));
        }

        return paragraph;
    }

    private static void AppendPlainRun(
        W.Paragraph paragraph,
        string source,
        int start,
        int length,
        bool isRtl)
    {
        if (length <= 0)
        {
            return;
        }

        paragraph.Append(new W.Run(
            CreateRunProperties(isRtl),
            CreateText(source.Substring(start, length))));
    }


    private static bool TryCreateImageParagraph(
        MainDocumentPart mainPart,
        ImageNoteBlock image,
        uint imageId,
        out W.Paragraph paragraph)
    {
        paragraph = null!;
        if (string.IsNullOrWhiteSpace(image.AssetPath) || !File.Exists(image.AssetPath))
        {
            return false;
        }

        try
        {
            var imageType = ResolveImageType(Path.GetExtension(image.AssetPath));
            var imagePart = mainPart.AddImagePart(imageType);
            using (var stream = File.OpenRead(image.AssetPath))
            {
                imagePart.FeedData(stream);
            }

            var (naturalWidth, naturalHeight) = ReadImageDimensions(image.AssetPath);
            var requestedWidth = image.DisplayWidth > 0
                ? Math.Min(image.DisplayWidth, naturalWidth)
                : naturalWidth;
            var widthEmus = Math.Min(
                PrintableWidthEmus,
                Math.Max(1, (long)Math.Round(requestedWidth * EmusPerDip)));
            var heightEmus = Math.Max(
                1,
                (long)Math.Round(widthEmus * naturalHeight / naturalWidth));
            var relationshipId = mainPart.GetIdOfPart(imagePart);
            paragraph = new W.Paragraph(
                new W.ParagraphProperties(
                    new W.SpacingBetweenLines { After = "120" },
                    new W.Justification { Val = W.JustificationValues.Center }),
                new W.Run(CreateDrawing(
                    relationshipId,
                    Path.GetFileName(image.AssetPath),
                    imageId,
                    widthEmus,
                    heightEmus)));
            return true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or NotSupportedException)
        {
            return false;
        }
    }

    private static W.Drawing CreateDrawing(
        string relationshipId,
        string name,
        uint imageId,
        long widthEmus,
        long heightEmus) =>
        new(
            new DW.Inline(
                new DW.Extent { Cx = widthEmus, Cy = heightEmus },
                new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                new DW.DocProperties { Id = imageId, Name = name },
                new DW.NonVisualGraphicFrameDrawingProperties(
                    new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = imageId, Name = name },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip
                                {
                                    Embed = relationshipId,
                                    CompressionState = A.BlipCompressionValues.Print
                                },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0, Y = 0 },
                                    new A.Extents { Cx = widthEmus, Cy = heightEmus }),
                                new A.PresetGeometry(new A.AdjustValueList())
                                {
                                    Preset = A.ShapeTypeValues.Rectangle
                                })))
                    {
                        Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture"
                    }))
            {
                DistanceFromTop = 0,
                DistanceFromBottom = 0,
                DistanceFromLeft = 0,
                DistanceFromRight = 0
            });

    private static W.Paragraph CreateImagePlaceholder() =>
        new(
            new W.ParagraphProperties(new W.SpacingBetweenLines { After = "120" }),
            new W.Run(
                new W.RunProperties(
                    new W.Italic(),
                    new W.Color { Val = "777777" },
                    new W.FontSize { Val = "18" }),
                CreateText("Image unavailable")));

    private static W.ParagraphProperties CreateParagraphProperties(bool isRtl, int afterTwips)
    {
        var properties = new W.ParagraphProperties();
        if (isRtl)
        {
            properties.Append(new W.BiDi());
        }

        properties.Append(
            new W.SpacingBetweenLines { After = afterTwips.ToString() },
            new W.Justification
            {
                Val = isRtl ? W.JustificationValues.Right : W.JustificationValues.Left
            });

        return properties;
    }

    private static W.RunProperties CreateRunProperties(
        bool isRtl,
        bool isBold = false,
        string fontSize = "22")
    {
        var properties = new W.RunProperties(
            new W.RunFonts
            {
                Ascii = "Segoe UI",
                HighAnsi = "Segoe UI",
                ComplexScript = "Segoe UI"
            });
        if (isBold)
        {
            properties.Append(new W.Bold(), new W.BoldComplexScript());
        }

        properties.Append(
            new W.FontSize { Val = fontSize },
            new W.FontSizeComplexScript { Val = fontSize });
        if (isRtl)
        {
            properties.Append(new W.RightToLeftText());
        }

        return properties;
    }

    private static W.Text CreateText(string value) =>
        new(value) { Space = SpaceProcessingModeValues.Preserve };

    private static IReadOnlyList<ParagraphSlice> EnumerateParagraphs(string text)
    {
        var result = new List<ParagraphSlice>();
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\r' && text[index] != '\n')
            {
                continue;
            }

            result.Add(new ParagraphSlice(start, index - start));
            if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
            {
                index++;
            }

            start = index + 1;
        }

        result.Add(new ParagraphSlice(start, text.Length - start));
        return result;
    }

    private static PartTypeInfo ResolveImageType(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".png" => ImagePartType.Png,
            ".jpg" or ".jpeg" => ImagePartType.Jpeg,
            ".bmp" => ImagePartType.Bmp,
            _ => throw new NotSupportedException("Unsupported note image format.")
        };

    private static (double Width, double Height) ReadImageDimensions(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var dpiX = frame.DpiX > 0 ? frame.DpiX : 96;
        var dpiY = frame.DpiY > 0 ? frame.DpiY : 96;
        return (
            frame.PixelWidth * 96d / dpiX,
            frame.PixelHeight * 96d / dpiY);
    }

    private static string GetTitle(NoteDocument note) =>
        string.IsNullOrWhiteSpace(note.Title) ? "Untitled note" : note.Title.Trim();

    private readonly record struct ParagraphSlice(int Start, int Length)
    {
        public int End => Start + Length;
    }
}
