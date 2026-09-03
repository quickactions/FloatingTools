using FloatingTools.App.Controls;

namespace FloatingTools.Tests.Controls;

public sealed class NotesTextSelectionTests
{
    [Theory]
    [InlineData("first line\nsecond line\nthird", 15, 11, 11)]
    [InlineData("first\r\nsecond", 2, 0, 5)]
    [InlineData("first\r\nsecond", 10, 7, 6)]
    [InlineData("single line", 5, 0, 11)]
    public void LogicalLineSelection_SelectsOnlyClickedLine(
        string text,
        int characterIndex,
        int expectedStart,
        int expectedLength)
    {
        Assert.Equal(
            new TextSelectionRange(expectedStart, expectedLength),
            NotesTextSelection.GetLogicalLine(text, characterIndex));
    }
}
