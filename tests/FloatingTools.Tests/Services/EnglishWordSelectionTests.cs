using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class EnglishWordSelectionTests
{
    [Theory]
    [InlineData("apply", 2, "apply")]
    [InlineData("Apply", 2, "Apply")]
    [InlineData("APPLY", 2, "APPLY")]
    [InlineData("I", 0, "I")]
    [InlineData("A", 0, "A")]
    [InlineData("(apply),", 3, "apply")]
    [InlineData("don't", 3, "don't")]
    [InlineData("I’m", 2, "I’m")]
    [InlineData("part-time", 7, "part-time")]
    public void EligibleEnglishWord_IsExtractedWithOriginalSpelling(
        string text,
        int characterIndex,
        string expected)
    {
        Assert.True(EnglishWordSelectionHelper.TryExtract(
            text,
            characterIndex,
            out var selection));

        Assert.Equal(expected, selection.Word);
        Assert.Equal(expected, text.Substring(selection.Start, selection.Length));
    }

    [Theory]
    [InlineData("שלום", 1)]
    [InlineData("123", 1)]
    [InlineData("...", 1)]
    [InlineData("https://example.com/apply", 21)]
    [InlineData("apply@example.com", 2)]
    [InlineData("Ctrl+Alt+T", 1)]
    [InlineData("apply123", 2)]
    [InlineData("example.com.", 2)]
    [InlineData("שלוםapply", 6)]
    [InlineData("applyשלום", 2)]
    public void IneligiblePosition_IsRejected(string text, int characterIndex)
    {
        Assert.False(EnglishWordSelectionHelper.TryExtract(
            text,
            characterIndex,
            out _));
    }

    [Fact]
    public void DelimitedEnglishInsideMixedText_IsAccepted()
    {
        const string text = "אני רוצה apply עכשיו";
        var index = text.IndexOf("apply", StringComparison.Ordinal) + 2;

        Assert.True(EnglishWordSelectionHelper.TryExtract(text, index, out var selection));
        Assert.Equal("apply", selection.Word);
    }
}
