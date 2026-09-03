using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class TranslationInputLimitsTests
{
    [Fact]
    public void CountWords_OneEnglishWord_ReturnsOne()
    {
        Assert.Equal(1, TranslationInputLimits.CountWords("hello"));
    }

    [Fact]
    public void CountWords_ExactlyThirtyWords_IsWithinLimit()
    {
        var text = string.Join(' ', Enumerable.Repeat("word", 30));

        Assert.Equal(30, TranslationInputLimits.CountWords(text));
        Assert.True(TranslationInputLimits.IsWithinWordLimit(text));
    }

    [Fact]
    public void CountWords_ThirtyOneWords_ExceedsLimit()
    {
        var text = string.Join(' ', Enumerable.Repeat("word", 31));

        Assert.Equal(31, TranslationInputLimits.CountWords(text));
        Assert.False(TranslationInputLimits.IsWithinWordLimit(text));
    }

    [Fact]
    public void CountWords_MultipleWhitespaceCharacters_DoNotCreateWords()
    {
        Assert.Equal(
            3,
            TranslationInputLimits.CountWords("one    two\t\tthree"));
    }

    [Fact]
    public void CountWords_MultipleLines_CountsOneCombinedRequest()
    {
        Assert.Equal(
            4,
            TranslationInputLimits.CountWords("one two\r\nthree\n\nfour"));
    }

    [Fact]
    public void CountWords_HebrewText_UsesSameWhitespaceRule()
    {
        Assert.Equal(3, TranslationInputLimits.CountWords("שלום   עולם יפה"));
    }

    [Fact]
    public void CountWords_EnglishText_UsesSameWhitespaceRule()
    {
        Assert.Equal(3, TranslationInputLimits.CountWords("translate this sentence"));
    }
}
