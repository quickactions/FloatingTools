using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FloatingTools.App.Services;

// PDFsharp receives one bitmap per text block. The DrawingVisual positions each
// paragraph explicitly, so the PDF bitmap has the same right/left edge as the editor.
internal sealed class NotePdfTextLayout : IDisposable
{
    private readonly DrawingVisual _visual;

    private NotePdfTextLayout(DrawingVisual visual, IReadOnlyList<NotePdfTextLineLayout> lines, double height)
    {
        _visual = visual;
        Lines = lines;
        Height = height;
    }

    internal IReadOnlyList<NotePdfTextLineLayout> Lines { get; }
    internal double Height { get; }

    internal static NotePdfTextLayout Create(string text, double width, Typeface typeface, double fontSize)
    {
        var direction = ParagraphDirectionResolver.Resolve(text);
        var isRtl = direction == FlowDirection.RightToLeft;
        var visual = new DrawingVisual();
        var lines = new List<NotePdfTextLineLayout>();
        var y = 0d;

        using (var drawing = visual.RenderOpen())
        {
            foreach (var slice in EnumerateParagraphSlices(text))
            {
                var textBlock = CreateTextBlock(text, slice, direction, typeface, fontSize);
                textBlock.Measure(new Size(width, double.PositiveInfinity));
                var lineWidth = Math.Min(width, Math.Max(1, textBlock.DesiredSize.Width));
                var lineHeight = Math.Max(textBlock.DesiredSize.Height, fontSize * 1.35);
                var x = isRtl ? width - lineWidth : 0;
                textBlock.Width = lineWidth;
                textBlock.Height = lineHeight;
                textBlock.Arrange(new Rect(0, 0, lineWidth, lineHeight));
                drawing.DrawRectangle(
                    new VisualBrush(textBlock),
                    null,
                    new Rect(x, y, lineWidth, lineHeight));
                lines.Add(new NotePdfTextLineLayout(x, lineWidth, y, lineHeight, width));
                y += lineHeight;
            }
        }

        return new NotePdfTextLayout(visual, lines, y);
    }

    internal void RenderTo(RenderTargetBitmap bitmap) => bitmap.Render(_visual);

    public void Dispose()
    {
    }

    private static TextBlock CreateTextBlock(
        string text,
        ParagraphSlice slice,
        FlowDirection direction,
        Typeface typeface,
        double fontSize)
    {
        var textBlock = new TextBlock
        {
            Padding = new Thickness(0),
            TextWrapping = TextWrapping.Wrap,
            FlowDirection = direction,
            TextAlignment = direction == FlowDirection.RightToLeft ? TextAlignment.Right : TextAlignment.Left,
            FontFamily = typeface.FontFamily,
            FontStyle = typeface.Style,
            FontWeight = typeface.Weight,
            FontStretch = typeface.Stretch,
            FontSize = fontSize,
            Foreground = Brushes.Black,
            Language = XmlLanguage.GetLanguage(direction == FlowDirection.RightToLeft ? "he-IL" : "en-US")
        };

        AppendRun(textBlock, text, slice.Start, slice.End - slice.Start);
        return textBlock;
    }

    private static void AppendRun(TextBlock textBlock, string source, int start, int length)
    {
        if (length <= 0)
        {
            return;
        }

        textBlock.Inlines.Add(new Run(source.Substring(start, length)));
    }

    private static IEnumerable<ParagraphSlice> EnumerateParagraphSlices(string text)
    {
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] is not ('\r' or '\n'))
            {
                continue;
            }

            yield return new ParagraphSlice(start, index);
            if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
            {
                index++;
            }

            start = index + 1;
        }

        yield return new ParagraphSlice(start, text.Length);
    }

    private readonly record struct ParagraphSlice(int Start, int End);
}

internal readonly record struct NotePdfTextLineLayout(double X, double Width, double Y, double Height, double AvailableWidth)
{
    internal double Right => X + Width;
}
