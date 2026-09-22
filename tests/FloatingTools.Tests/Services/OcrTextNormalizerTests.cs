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
}
