using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class NoteTitleGeneratorTests
{
    [Fact]
    public void EmptyContent_ReturnsUntitledNote() =>
        Assert.Equal("Untitled note", NoteTitleGenerator.Generate(" \r\n "));

    [Theory]
    [InlineData("one two three four five six", "one two three")]
    [InlineData("לסיים את הפרויקט היום בבוקר מוקדם", "לסיים את הפרויקט")]
    public void Generate_UsesFirstThreeWhitespaceNormalizedWords(string content, string expected) =>
        Assert.Equal(expected, NoteTitleGenerator.Generate(content));

    [Fact]
    public void Generate_UsesSensibleMaximumLength()
    {
        var title = NoteTitleGenerator.Generate(new string('a', 100));
        Assert.Equal(NoteTitleGenerator.MaximumTitleLength, title.Length);
    }

    [Theory]
    [InlineData("Meeting notes,", "Meeting notes")]
    [InlineData("Meeting notes.", "Meeting notes")]
    [InlineData("Hello:", "Hello")]
    [InlineData("Hello;", "Hello")]
    [InlineData("What happened?", "What happened")]
    [InlineData("Really?!", "Really")]
    [InlineData("Wait…", "Wait")]
    [InlineData("word ,", "word")]
    [InlineData("שלום עולם.", "שלום עולם")]
    [InlineData("שלום־", "שלום")]
    public void Generate_RemovesOrdinaryTrailingPunctuation(
        string content,
        string expected) =>
        Assert.Equal(expected, NoteTitleGenerator.Generate(content));

    [Theory]
    [InlineData("Test: example")]
    [InlineData("hello-world test")]
    [InlineData("וכו׳")]
    [InlineData("צה״ל")]
    [InlineData("Hello 😀")]
    [InlineData("Price 5$")]
    [InlineData("Learn C#")]
    [InlineData("Discount 50%")]
    [InlineData("email@")]
    [InlineData("R&D")]
    [InlineData("value*")]
    [InlineData("path/")]
    [InlineData("Test (example)")]
    public void Generate_PreservesInternalAndMeaningfulTrailingCharacters(string content) =>
        Assert.Equal(content, NoteTitleGenerator.Generate(content));

    [Fact]
    public void Generate_PunctuationOnlyContentReturnsUntitledNote() =>
        Assert.Equal(NoteTitleGenerator.UntitledTitle, NoteTitleGenerator.Generate("?! …"));

    [Fact]
    public void Generate_RemovesPunctuationExposedByMaximumLengthTruncation()
    {
        var content = new string('a', NoteTitleGenerator.MaximumTitleLength - 1) + ",tail";

        Assert.Equal(
            new string('a', NoteTitleGenerator.MaximumTitleLength - 1),
            NoteTitleGenerator.Generate(content));
    }
}
