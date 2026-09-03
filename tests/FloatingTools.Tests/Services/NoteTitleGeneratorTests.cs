using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class NoteTitleGeneratorTests
{
    [Fact]
    public void EmptyContent_ReturnsUntitledNote() =>
        Assert.Equal("Untitled note", NoteTitleGenerator.Generate(" \r\n "));

    [Theory]
    [InlineData("one two three four five six", "one two three four")]
    [InlineData("לסיים את הפרויקט היום בבוקר מוקדם", "לסיים את הפרויקט היום")]
    public void Generate_UsesFirstFourWhitespaceNormalizedWords(string content, string expected) =>
        Assert.Equal(expected, NoteTitleGenerator.Generate(content));

    [Fact]
    public void Generate_UsesSensibleMaximumLength()
    {
        var title = NoteTitleGenerator.Generate(new string('a', 100));
        Assert.Equal(NoteTitleGenerator.MaximumTitleLength, title.Length);
    }
}
