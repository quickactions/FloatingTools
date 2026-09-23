using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FloatingTools.App.Models;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace FloatingTools.App.Services;

public static class NotePdfExporter
{
    private const double PageMargin = 46;
    private const double BlockSpacing = 9;
    private const double BodyFontSize = 11;
    private const double RenderScale = 1.5;
    private static readonly Typeface BodyTypeface = new("Segoe UI");
    private static readonly Typeface TitleTypeface = new(
        new FontFamily("Segoe UI"),
        FontStyles.Normal,
        FontWeights.SemiBold,
        FontStretches.Normal);
    static NotePdfExporter()
    {
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }

    public static NoteExportResult Create(NoteDocument note)
    {
        ArgumentNullException.ThrowIfNull(note);
        using var document = new PdfDocument();
        document.Info.Title = GetTitle(note);
        var context = AddPage(document);
        var hadImageError = false;

        using (var title = RenderText(
                   GetTitle(note),
                   context.ContentWidth,
                   18,
                   TitleTypeface))
        {
            DrawBitmapAcrossPages(document, ref context, title.Bitmap, title.PointHeight);
        }

        context.CursorY += 12;
        foreach (var block in note.Blocks)
        {
            switch (block)
            {
                case TextNoteBlock text when text.Text.Length > 0:
                    using (var rendered = RenderText(
                               text.Text,
                               context.ContentWidth,
                               BodyFontSize,
                               BodyTypeface))
                    {
                        EnsureBlockSpacing(document, ref context);
                        DrawBitmapAcrossPages(
                            document,
                            ref context,
                            rendered.Bitmap,
                            rendered.PointHeight);
                    }
                    break;

                case ImageNoteBlock image:
                    EnsureBlockSpacing(document, ref context);
                    if (!TryDrawImage(document, ref context, image))
                    {
                        hadImageError = true;
                        DrawImagePlaceholder(document, ref context);
                    }
                    break;

                case LinkListNoteBlock links when links.Items.Count > 0:
                    using (var rendered = RenderText(
                               string.Join(Environment.NewLine, links.Items.Select(item => item.VisibleName)),
                               context.ContentWidth,
                               BodyFontSize,
                               BodyTypeface))
                    {
                        EnsureBlockSpacing(document, ref context);
                        DrawBitmapAcrossPages(document, ref context, rendered.Bitmap, rendered.PointHeight);
                    }
                    break;
            }
        }

        context.Dispose();
        using var output = new MemoryStream();
        document.Save(output, closeStream: false);
        return new NoteExportResult(output.ToArray(), hadImageError);
    }

    private static PageContext AddPage(PdfDocument document)
    {
        var page = document.AddPage();
        page.Size = PageSize.A4;
        return new PageContext(
            XGraphics.FromPdfPage(page),
            page.Width.Point - (PageMargin * 2),
            PageMargin,
            page.Height.Point - PageMargin);
    }

    private static void EnsureBlockSpacing(PdfDocument document, ref PageContext context)
    {
        if (context.CursorY + BlockSpacing >= context.Bottom)
        {
            context.Dispose();
            context = AddPage(document);
            return;
        }

        context.CursorY += BlockSpacing;
    }

    private static void DrawBitmapAcrossPages(
        PdfDocument document,
        ref PageContext context,
        BitmapSource bitmap,
        double totalPointHeight)
    {
        var sourceY = 0;
        while (sourceY < bitmap.PixelHeight)
        {
            var availablePoints = context.Bottom - context.CursorY;
            if (availablePoints < 18)
            {
                context.Dispose();
                context = AddPage(document);
                availablePoints = context.Bottom - context.CursorY;
            }

            var remainingPixels = bitmap.PixelHeight - sourceY;
            var remainingPoints = totalPointHeight * remainingPixels / bitmap.PixelHeight;
            var drawPoints = Math.Min(availablePoints, remainingPoints);
            var pixelHeight = Math.Min(
                remainingPixels,
                Math.Max(1, (int)Math.Floor(drawPoints * bitmap.PixelHeight / totalPointHeight)));
            using var slice = XImage.FromBitmapSource(new CroppedBitmap(
                bitmap,
                new Int32Rect(0, sourceY, bitmap.PixelWidth, pixelHeight)));
            var sliceHeight = totalPointHeight * pixelHeight / bitmap.PixelHeight;
            context.Graphics.DrawImage(
                slice,
                PageMargin,
                context.CursorY,
                context.ContentWidth,
                sliceHeight);
            context.CursorY += sliceHeight;
            sourceY += pixelHeight;
        }
    }

    private static bool TryDrawImage(
        PdfDocument document,
        ref PageContext context,
        ImageNoteBlock image)
    {
        if (string.IsNullOrWhiteSpace(image.AssetPath) || !File.Exists(image.AssetPath))
        {
            return false;
        }

        try
        {
            using var source = XImage.FromFile(image.AssetPath);
            var requestedWidth = image.DisplayWidth > 0
                ? image.DisplayWidth * 72d / 96d
                : source.PointWidth;
            var scale = Math.Min(
                1,
                Math.Min(
                    context.ContentWidth / source.PointWidth,
                    requestedWidth / source.PointWidth));
            var width = source.PointWidth * scale;
            var height = source.PointHeight * scale;
            var pageHeight = context.Bottom - PageMargin;
            if (height > pageHeight)
            {
                scale *= pageHeight / height;
                width = source.PointWidth * scale;
                height = source.PointHeight * scale;
            }

            if (context.CursorY + height > context.Bottom)
            {
                context.Dispose();
                context = AddPage(document);
            }

            context.Graphics.DrawImage(
                source,
                CenterImageX(context.ContentWidth, width),
                context.CursorY,
                width,
                height);
            context.CursorY += height;
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

    internal static double CenterImageX(double contentWidth, double imageWidth) =>
        PageMargin + (contentWidth - imageWidth) / 2;

    private static void DrawImagePlaceholder(PdfDocument document, ref PageContext context)
    {
        const double height = 28;
        if (context.CursorY + height > context.Bottom)
        {
            context.Dispose();
            context = AddPage(document);
        }

        var font = new XFont("Segoe UI", 9, XFontStyleEx.Italic);
        context.Graphics.DrawString(
            "Image unavailable",
            font,
            XBrushes.Gray,
            new XRect(PageMargin, context.CursorY, context.ContentWidth, height),
            XStringFormats.CenterLeft);
        context.CursorY += height;
    }

    private static RenderedText RenderText(
        string text,
        double widthPoints,
        double fontSizePoints,
        Typeface typeface)
    {
        const double pointsToDips = 96d / 72d;
        var widthDips = widthPoints * pointsToDips;
        var layout = NotePdfTextLayout.Create(text, widthDips, typeface, fontSizePoints * pointsToDips);
        var heightDips = Math.Max(layout.Height, fontSizePoints * pointsToDips * 1.35);
        var bitmap = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(widthDips * RenderScale)),
            Math.Max(1, (int)Math.Ceiling(heightDips * RenderScale)),
            96 * RenderScale,
            96 * RenderScale,
            PixelFormats.Pbgra32);
        layout.RenderTo(bitmap);
        bitmap.Freeze();
        return new RenderedText(bitmap, heightDips * 72d / 96d);
    }

    private static string GetTitle(NoteDocument note) =>
        string.IsNullOrWhiteSpace(note.Title) ? "Untitled note" : note.Title.Trim();

    private sealed record RenderedText(BitmapSource Bitmap, double PointHeight) : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private sealed class PageContext(
        XGraphics graphics,
        double contentWidth,
        double cursorY,
        double bottom) : IDisposable
    {
        public XGraphics Graphics { get; } = graphics;
        public double ContentWidth { get; } = contentWidth;
        public double CursorY { get; set; } = cursorY;
        public double Bottom { get; } = bottom;
        public void Dispose() => Graphics.Dispose();
    }
}
