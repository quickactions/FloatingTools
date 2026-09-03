using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class OcrTextValidatorTests
{
    [Theory]
    [InlineData("*")]
    [InlineData("|")]
    [InlineData("7")]
    [InlineData("* | 7")]
    [InlineData("z")]
    [InlineData("  \r\n  ")]
    public void ContainsMeaningfulEnglishText_NoiseOnlyResultIsRejected(string text)
    {
        Assert.False(OcrTextValidator.ContainsMeaningfulEnglishText(text));
    }

    [Theory]
    [InlineData("HELLO")]
    [InlineData("NO!")]
    [InlineData("I")]
    [InlineData("A")]
    [InlineData("Chapter 7")]
    [InlineData("HEADMISTRESS * PEARL")]
    [InlineData("I DON'T KNOW WHAT HE WANTS. z")]
    public void ContainsMeaningfulEnglishText_RealTextIsAcceptedIntact(string text)
    {
        Assert.True(OcrTextValidator.ContainsMeaningfulEnglishText(text));
    }

    [Fact]
    public void Validation_DoesNotModifyAcceptedText()
    {
        const string text = "HEADMISTRESS * PEARL, I'VE TRIED MY BEST...";

        var accepted = OcrTextValidator.ContainsMeaningfulEnglishText(text);

        Assert.True(accepted);
        Assert.Equal("HEADMISTRESS * PEARL, I'VE TRIED MY BEST...", text);
    }
}
