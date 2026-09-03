using FloatingTools.App.Controls;

namespace FloatingTools.Tests.Controls;

public sealed class NotesPageFocusResolverTests
{
    [Fact]
    public void ClickBelowFinalContent_FocusesFinalTextAtEnd()
    {
        EditableTextBounds[] blocks =
        [
            new(0, 30, 5),
            new(160, 190, 12)
        ];

        var target = NotesPageFocusResolver.Resolve(500, blocks);

        Assert.Equal(new EditableTextFocusTarget(1, 12), target);
    }

    [Fact]
    public void ClickBetweenBlocks_ChoosesNearestEditablePositionNotImageSpace()
    {
        EditableTextBounds[] blocks =
        [
            new(0, 30, 5),
            new(160, 190, 12)
        ];

        Assert.Equal(
            new EditableTextFocusTarget(0, 5),
            NotesPageFocusResolver.Resolve(60, blocks));
        Assert.Equal(
            new EditableTextFocusTarget(1, 0),
            NotesPageFocusResolver.Resolve(140, blocks));
    }

    [Fact]
    public void NoTextBlock_RequestsCreationInsteadOfInventingCoordinates()
    {
        Assert.Null(NotesPageFocusResolver.Resolve(200, []));
    }
}
