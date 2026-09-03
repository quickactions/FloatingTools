using System.Globalization;
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

public static class SavedWordsPdfExporter
{
    private const double PageMargin = 44;
    private const double ColumnGap = 18;
    private const double CellHorizontalPadding = 7;
    private const double CellVerticalPadding = 6;
    private const double BodyFontSize = 10;
    private const double BodyLineHeight = 14;
    private const double MinimumRowHeight = 28;
    private static readonly Typeface HebrewTypeface = new("Segoe UI");

    static SavedWordsPdfExporter()
    {
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }

    public static byte[] Create(
        IReadOnlyList<SavedWord> items,
        string title = "Saved Words")
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        using var document = new PdfDocument();
        document.Info.Title = title;
        var rows = SavedWordExportNormalizer.Normalize(items);
        PageContext? context = null;
        var isFirstPage = true;

        foreach (var row in rows)
        {
            context ??= AddPage(document, title, isFirstPage);
            isFirstPage = false;

            var englishLines = WrapEnglish(
                context.Graphics,
                row.English,
                context.BodyFont,
                context.ColumnWidth - (CellHorizontalPadding * 2));
            using var hebrew = RenderHebrew(
                row.Hebrew,
                context.ColumnWidth - (CellHorizontalPadding * 2));
            var rowHeight = Math.Max(
                MinimumRowHeight,
                Math.Max(
                    englishLines.Count * BodyLineHeight,
                    hebrew.PointHeight) + (CellVerticalPadding * 2));

            if (context.CursorY + rowHeight > context.Bottom)
            {
                context.Dispose();
                context = AddPage(document, title, isFirstPage: false);
            }

            DrawRow(context, englishLines, hebrew, rowHeight);
        }

        context?.Dispose();
        using var output = new MemoryStream();
        document.Save(output, closeStream: false);
        return output.ToArray();
    }

    private static PageContext AddPage(
        PdfDocument document,
        string title,
        bool isFirstPage)
    {
        var page = document.AddPage();
        page.Size = PageSize.A4;
        var graphics = XGraphics.FromPdfPage(page);
        var titleFont = new XFont("Segoe UI", 18, XFontStyleEx.Bold);
        var headerFont = new XFont("Segoe UI", 10, XFontStyleEx.Bold);
        var bodyFont = new XFont("Segoe UI", BodyFontSize, XFontStyleEx.Regular);
        var contentWidth = page.Width.Point - (PageMargin * 2);
        var columnWidth = (contentWidth - ColumnGap) / 2;
        var cursorY = PageMargin;

        if (isFirstPage)
        {
            graphics.DrawString(
                title,
                titleFont,
                XBrushes.Black,
                new XRect(PageMargin, cursorY, contentWidth, 25),
                XStringFormats.TopLeft);
            cursorY += 36;
        }

        graphics.DrawString(
            "English",
            headerFont,
            XBrushes.Black,
            new XRect(PageMargin + CellHorizontalPadding, cursorY, columnWidth, 18),
            XStringFormats.TopLeft);
        graphics.DrawString(
            "Hebrew",
            headerFont,
            XBrushes.Black,
            new XRect(
                PageMargin + columnWidth + ColumnGap,
                cursorY,
                columnWidth - CellHorizontalPadding,
                18),
            XStringFormats.TopRight);
        cursorY += 20;
        graphics.DrawLine(
            new XPen(XColors.LightGray, 0.7),
            PageMargin,
            cursorY,
            page.Width.Point - PageMargin,
            cursorY);

        return new PageContext(
            graphics,
            bodyFont,
            columnWidth,
            cursorY,
            page.Height.Point - PageMargin);
    }

    private static void DrawRow(
        PageContext context,
        IReadOnlyList<string> englishLines,
        XImage hebrew,
        double rowHeight)
    {
        var textY = context.CursorY + CellVerticalPadding;
        foreach (var line in englishLines)
        {
            context.Graphics.DrawString(
                line,
                context.BodyFont,
                XBrushes.Black,
                new XRect(
                    PageMargin + CellHorizontalPadding,
                    textY,
                    context.ColumnWidth - (CellHorizontalPadding * 2),
                    BodyLineHeight),
                XStringFormats.TopLeft);
            textY += BodyLineHeight;
        }

        var hebrewRight = PageMargin
            + (context.ColumnWidth * 2)
            + ColumnGap
            - CellHorizontalPadding;
        context.Graphics.DrawImage(
            hebrew,
            hebrewRight - hebrew.PointWidth,
            context.CursorY + CellVerticalPadding,
            hebrew.PointWidth,
            hebrew.PointHeight);

        context.CursorY += rowHeight;
        context.Graphics.DrawLine(
            new XPen(XColors.LightGray, 0.45),
            PageMargin,
            context.CursorY,
            PageMargin + (context.ColumnWidth * 2) + ColumnGap,
            context.CursorY);
    }

    private static IReadOnlyList<string> WrapEnglish(
        XGraphics graphics,
        string text,
        XFont font,
        double maximumWidth)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            var words = paragraph.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            var current = words[0];
            for (var index = 1; index < words.Length; index++)
            {
                var candidate = $"{current} {words[index]}";
                if (graphics.MeasureString(candidate, font).Width <= maximumWidth)
                {
                    current = candidate;
                }
                else
                {
                    lines.Add(current);
                    current = words[index];
                }
            }

            lines.Add(current);
        }

        return lines.Count == 0 ? [string.Empty] : lines;
    }

    private static XImage RenderHebrew(string text, double maximumWidthPoints)
    {
        const double pointsToDips = 96d / 72d;
        const double renderScale = 2d;
        var maximumWidthDips = maximumWidthPoints * pointsToDips;
        var formatted = new FormattedText(
            text,
            CultureInfo.GetCultureInfo("he-IL"),
            FlowDirection.RightToLeft,
            HebrewTypeface,
            BodyFontSize * pointsToDips,
            Brushes.Black,
            pixelsPerDip: 1)
        {
            MaxTextWidth = maximumWidthDips,
            TextAlignment = TextAlignment.Left
        };
        var heightDips = Math.Max(formatted.Height, BodyLineHeight * pointsToDips);
        var bitmap = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(maximumWidthDips * renderScale)),
            Math.Max(1, (int)Math.Ceiling(heightDips * renderScale)),
            96 * renderScale,
            96 * renderScale,
            PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawText(formatted, new Point(0, 0));
        }

        bitmap.Render(visual);
        bitmap.Freeze();
        return XImage.FromBitmapSource(CropToContent(bitmap));
    }

    private static BitmapSource CropToContent(BitmapSource bitmap)
    {
        const int bytesPerPixel = 4;
        var stride = bitmap.PixelWidth * bytesPerPixel;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        var minX = bitmap.PixelWidth;
        var minY = bitmap.PixelHeight;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < bitmap.PixelHeight; y++)
        {
            for (var x = 0; x < bitmap.PixelWidth; x++)
            {
                if (pixels[(y * stride) + (x * bytesPerPixel) + 3] == 0)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return bitmap;
        }

        var cropped = new CroppedBitmap(
            bitmap,
            new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1));
        cropped.Freeze();
        return cropped;
    }

    private sealed class PageContext(
        XGraphics graphics,
        XFont bodyFont,
        double columnWidth,
        double cursorY,
        double bottom) : IDisposable
    {
        public XGraphics Graphics { get; } = graphics;

        public XFont BodyFont { get; } = bodyFont;

        public double ColumnWidth { get; } = columnWidth;

        public double CursorY { get; set; } = cursorY;

        public double Bottom { get; } = bottom;

        public void Dispose() => Graphics.Dispose();
    }
}
