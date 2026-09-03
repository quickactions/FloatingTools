using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class OcrTextNormalizerTests
{
    [Fact]
    public void Normalize_ConvertsVisualLineBreaksToSpaces()
    {
        var result = OcrTextNormalizer.Normalize(
            "\r\n  \r\nI DON'T KNOW  \r\nWHAT HE WANTS.\r\n\r\n");

        Assert.Equal("I DON'T KNOW WHAT HE WANTS.", result);
    }

    [Fact]
    public void Normalize_CollapsesMixedRepeatedWhitespace()
    {
        var result = OcrTextNormalizer.Normalize(
            "  HEADMISTRESS\t\tPEARL,\r\nI'VE   TRIED\nMY BEST  ");

        Assert.Equal("HEADMISTRESS PEARL, I'VE TRIED MY BEST", result);
    }

    [Fact]
    public void Normalize_PreservesPunctuationExactly()
    {
        var result = OcrTextNormalizer.Normalize(
            "WHAT?\nI DON'T KNOW—REALLY!\n(YES), SHE SAID.");

        Assert.Equal("WHAT? I DON'T KNOW—REALLY! (YES), SHE SAID.", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n \n")]
    public void Normalize_EmptyContentReturnsEmpty(string? input)
    {
        Assert.Empty(OcrTextNormalizer.Normalize(input));
    }

    [Fact]
    public void ScreenSelection_CreateNormalizesDirectionAndClampsToMonitor()
    {
        var bounds = new PixelRect(-1920, 0, 0, 1080);

        var selection = ScreenSelectionCalculator.Create(
            new PixelPoint(-100, 900),
            new PixelPoint(-2000, -20),
            bounds);

        Assert.Equal(new PixelRect(-1920, 0, -100, 900), selection);
    }

    [Fact]
    public void ScreenSelection_TinySelectionCancels()
    {
        var selection = ScreenSelectionCalculator.Create(
            new PixelPoint(10, 10),
            new PixelPoint(12, 13),
            new PixelRect(0, 0, 1920, 1080));

        Assert.Null(selection);
    }

    [Fact]
    public void CapturedImage_DisposeClearsAndInvalidatesImageBytes()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var image = new CapturedScreenImage(bytes);

        image.Dispose();

        Assert.Equal(new byte[] { 0, 0, 0 }, bytes);
        Assert.Throws<ObjectDisposedException>(() => _ = image.Data);
    }

    [Fact]
    public async Task TesseractService_MissingEnglishDataFailsBeforeNativeOcr()
    {
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            "FloatingTools-tests",
            Guid.NewGuid().ToString("N"));
        using var service = new TesseractLocalOcrService(missingPath);
        using var image = new CapturedScreenImage([1, 2, 3]);

        var exception = await Assert.ThrowsAsync<FileNotFoundException>(
            () => service.RecognizeEnglishAsync(image));

        Assert.Contains("eng.traineddata", exception.FileName);
    }

    [Fact]
    public async Task LocalTesseractSmoke_RecognizesClearRenderedEnglishText()
    {
        var imageBytes = await RenderEnglishTextAsync("HELLO WORLD");
        using var service = new TesseractLocalOcrService(
            Path.Combine(AppContext.BaseDirectory, "tessdata"));
        using var image = new CapturedScreenImage(imageBytes);

        var result = await service.RecognizeEnglishAsync(image);

        Assert.Contains("HELLO", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WORLD", result, StringComparison.OrdinalIgnoreCase);
    }

    private static Task<byte[]> RenderEnglishTextAsync(string text)
    {
        var completion = new TaskCompletionSource<byte[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                const int width = 460;
                const int height = 130;
                var visual = new DrawingVisual();
                using (var drawing = visual.RenderOpen())
                {
                    drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
                    var formattedText = new FormattedText(
                        text,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Arial"),
                        48,
                        Brushes.Black,
                        1);
                    drawing.DrawText(formattedText, new Point(18, 31));
                }

                var bitmap = new RenderTargetBitmap(
                    width,
                    height,
                    96,
                    96,
                    PixelFormats.Pbgra32);
                bitmap.Render(visual);
                var encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = new MemoryStream();
                encoder.Save(stream);
                completion.TrySetResult(stream.ToArray());
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
