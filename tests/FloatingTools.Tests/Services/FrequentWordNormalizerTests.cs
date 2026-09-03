using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class FrequentWordNormalizerTests
{
    [Theory]
    [InlineData("hello", "hello", "hello")]
    [InlineData("Hello", "hello", "Hello")]
    [InlineData("  HELLO  ", "hello", "HELLO")]
    [InlineData("hello!", "hello", "hello")]
    [InlineData("hello,", "hello", "hello")]
    [InlineData("hello.", "hello", "hello")]
    [InlineData("GitHub", "github", "GitHub")]
    [InlineData("dotnet", "dotnet", "dotnet")]
    [InlineData("C#", "c#", "C#")]
    [InlineData("some_identifier", "some_identifier", "some_identifier")]
    public void EnglishSingleWord_NormalizesWithoutManglingIdentifiers(
        string input,
        string expectedKey,
        string expectedDisplay)
    {
        var result = FrequentWordNormalizer.TryNormalizeSingleWord(
            input,
            "en",
            out var key,
            out var display);

        Assert.True(result);
        Assert.Equal(expectedKey, key);
        Assert.Equal(expectedDisplay, display);
    }

    [Fact]
    public void HebrewSingleWord_IsUnicodeNormalizedAndPreserved()
    {
        var decomposed = "שָׁלוֹם".Normalize(System.Text.NormalizationForm.FormD);

        var result = FrequentWordNormalizer.TryNormalizeSingleWord(
            $"  {decomposed}! ",
            "he",
            out var key,
            out var display);

        Assert.True(result);
        Assert.Equal("שָׁלוֹם".Normalize(), key);
        Assert.Equal(key, display);
    }

    [Theory]
    [InlineData("hello world", "en")]
    [InlineData("אני רוצה", "he")]
    [InlineData("hello\nworld", "en")]
    [InlineData("hello\n", "en")]
    public void PhrasesAndMultilineInput_AreNotEligible(string input, string language)
    {
        Assert.False(FrequentWordNormalizer.TryNormalizeSingleWord(
            input,
            language,
            out _,
            out _));
    }

    [Fact]
    public void OppositeDirections_CreateSameCanonicalPairIdentity()
    {
        Assert.True(FrequentWordNormalizer.TryCreateCanonicalPair(
            "BALL!", "כדור", "en", "he", out var englishFirst));
        Assert.True(FrequentWordNormalizer.TryCreateCanonicalPair(
            "כדור", "ball", "he", "en", out var hebrewFirst));

        Assert.Equal(englishFirst.PairKey, hebrewFirst.PairKey);
        Assert.Equal("ball", englishFirst.EnglishKey);
        Assert.Equal("כדור", englishFirst.HebrewKey);
    }
}
