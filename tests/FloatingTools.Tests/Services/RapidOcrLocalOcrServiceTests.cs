using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FloatingTools.App.Services;
using SkiaSharp;
using Xunit.Abstractions;

namespace FloatingTools.Tests.Services;

public sealed class RapidOcrLocalOcrServiceTests(
    RapidOcrServiceFixture fixture,
    ITestOutputHelper output) : IClassFixture<RapidOcrServiceFixture>
{
    [Fact]
    public async Task MissingModels_ReportsDetectorPathBeforeNativeOcr()
    {
        var missingDirectory = Path.Combine(
            Path.GetTempPath(),
            "FloatingTools-tests",
            Guid.NewGuid().ToString("N"));
        using var service = new RapidOcrLocalOcrService(missingDirectory);
        using var image = new CapturedScreenImage([1, 2, 3]);

        var exception = await Assert.ThrowsAsync<FileNotFoundException>(
            () => service.RecognizeEnglishAsync(image));

        var expectedPath = Path.Combine(
            missingDirectory,
            "ch_PP-OCRv5_mobile_det.onnx");
        Assert.Equal(expectedPath, exception.FileName);
    }

    [Fact]
    public async Task RealEngine_RecognizesClearEnglishAndReturnsEmptyForBlankImage()
    {
        var clearImageBytes = await RenderBmpAsync("HELLO WORLD");

        var first = await RecognizeTimedAsync(clearImageBytes);
        output.WriteLine($"Initialization and first recognition: {first.Elapsed.TotalMilliseconds:F1} ms");
        Assert.Contains("HELLO", first.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WORLD", first.Text, StringComparison.OrdinalIgnoreCase);

        var second = await RecognizeTimedAsync(clearImageBytes);
        output.WriteLine($"Second recognition: {second.Elapsed.TotalMilliseconds:F1} ms");
        Assert.Contains("HELLO", second.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WORLD", second.Text, StringComparison.OrdinalIgnoreCase);

        var blankImageBytes = await RenderBmpAsync(null);
        using var blankImage = new CapturedScreenImage(blankImageBytes);
        var blankResult = await fixture.Service.RecognizeEnglishAsync(blankImage);
        Assert.Empty(blankResult);
    }

    private async Task<(string Text, TimeSpan Elapsed)> RecognizeTimedAsync(byte[] bytes)
    {
        using var image = new CapturedScreenImage(bytes.ToArray());
        var stopwatch = Stopwatch.StartNew();
        var text = await fixture.Service.RecognizeEnglishAsync(image);
        stopwatch.Stop();
        return (text, stopwatch.Elapsed);
    }

    [Fact]
    public async Task AlphaZeroBgraCaptureBmp_PreservesVisibleEnglishText()
    {
        var bytes = await RenderBmpAsync("HELLO WORLD", zeroAlpha: true);
        output.WriteLine($"BMP bits per pixel: {BitConverter.ToUInt16(bytes, 28)}; compression: {BitConverter.ToUInt32(bytes, 30)} (0 = BI_RGB)");
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        Assert.NotNull(codec);
        output.WriteLine($"SKCodec alpha type: {codec.Info.AlphaType}");
        using var decoded = SKBitmap.Decode(bytes);
        Assert.NotNull(decoded);
        Assert.Contains(decoded.Pixels, pixel => pixel.Red > 200 && pixel.Green > 200 && pixel.Blue > 200);
        Assert.Contains(decoded.Pixels, pixel => pixel.Red < 50 && pixel.Green < 50 && pixel.Blue < 50);
        using var image = new CapturedScreenImage(bytes);
        var result = await fixture.Service.RecognizeEnglishAsync(image);
        Assert.Contains("HELLO", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WORLD", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("OK")]
    [InlineData("A")]
    public async Task ShortEnglishToken_RecognizedAt24Pixels(string token)
    {
        using var image = new CapturedScreenImage(await RenderBmpAsync(
            token, fontFamily: "Segoe UI", fontSize: 24, width: 120, height: 90));
        var result = await fixture.Service.RecognizeEnglishAsync(image);
        output.WriteLine($"Token: {token}; recognized: {result}");
        Assert.Contains(token, result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreCancelledToken_CancelsBeforeModelValidationOrNativeInitialization()
    {
        var missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(missingDirectory));
        using var service = new RapidOcrLocalOcrService(missingDirectory);
        using var image = new CapturedScreenImage([1, 2, 3]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.RecognizeEnglishAsync(image, cancellation.Token));
        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    private static Task<byte[]> RenderBmpAsync(
        string? text, bool zeroAlpha = false, string fontFamily = "Arial",
        double fontSize = 52, int width = 520, int height = 150)
    {
        var completion = new TaskCompletionSource<byte[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var visual = new DrawingVisual();
                using (var drawing = visual.RenderOpen())
                {
                    drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
                    if (text is not null)
                    {
                        var formattedText = new FormattedText(
                            text,
                            CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight,
                            new Typeface(fontFamily),
                            fontSize,
                            Brushes.Black,
                            1);
                        drawing.DrawText(formattedText, new Point(24, 35));
                    }
                }

                var bitmap = new RenderTargetBitmap(
                    width,
                    height,
                    96,
                    96,
                    PixelFormats.Pbgra32);
                bitmap.Render(visual);
                BitmapSource source = bitmap;
                if (zeroAlpha)
                {
                    var stride = width * 4;
                    var pixels = new byte[stride * height];
                    bitmap.CopyPixels(pixels, stride, 0);
                    for (var index = 3; index < pixels.Length; index += 4)
                    {
                        pixels[index] = 0;
                    }

                    source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
                    var verified = new byte[pixels.Length];
                    source.CopyPixels(verified, stride, 0);
                    Assert.Equal(PixelFormats.Bgra32, source.Format);
                    Assert.Equal(pixels, verified);
                    Assert.All(Enumerable.Range(0, width * height), index => Assert.Equal(0, verified[index * 4 + 3]));
                    Assert.Contains(Enumerable.Range(0, width * height), index => verified[index * 4] > 200);
                    Assert.Contains(Enumerable.Range(0, width * height), index => verified[index * 4] < 50);
                }
                var encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));
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

public sealed class RapidOcrServiceFixture : IDisposable
{
    public RapidOcrServiceFixture()
    {
        Service = new RapidOcrLocalOcrService(
            Path.Combine(AppContext.BaseDirectory, "models", "v5"));
    }

    public RapidOcrLocalOcrService Service { get; }

    public void Dispose() => Service.Dispose();
}
