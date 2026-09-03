using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class InsertionZoneFocusResolverTests
{
    [Fact]
    public void Resolve_PrefersTextImmediatelyAboveTheZone()
    {
        var text = new TextNoteBlock();

        Assert.Same(text, Resolve([text, new ImageNoteBlock()], 1));
    }

    [Fact]
    public void Resolve_PrefersLinkListImmediatelyAboveTheZone()
    {
        var links = new LinkListNoteBlock();

        Assert.Same(links, Resolve([links, new ImageNoteBlock()], 1));
    }

    [Fact]
    public void Resolve_SkipsImagesWhileSearchingUpwardForText()
    {
        var text = new TextNoteBlock();

        Assert.Same(text, Resolve([text, new ImageNoteBlock(), new ImageNoteBlock()], 3));
    }

    [Fact]
    public void Resolve_SkipsImagesWhileSearchingUpwardForLinkList()
    {
        var links = new LinkListNoteBlock();

        Assert.Same(links, Resolve([links, new ImageNoteBlock(), new ImageNoteBlock()], 3));
    }

    [Fact]
    public void Resolve_FallsBackDownwardToTextWhenNothingEditableIsAbove()
    {
        var text = new TextNoteBlock();

        Assert.Same(text, Resolve([new ImageNoteBlock(), text], 0));
    }

    [Fact]
    public void Resolve_FallsBackDownwardToLinkListWhenNothingEditableIsAbove()
    {
        var links = new LinkListNoteBlock();

        Assert.Same(links, Resolve([new ImageNoteBlock(), links], 0));
    }

    [Fact]
    public void Resolve_ReturnsNullWhenThereAreNoEditableBlocks()
    {
        Assert.Null(Resolve([new ImageNoteBlock(), new ImageNoteBlock()], 1));
    }

    [Fact]
    public void Resolve_ReturnsNullForAnEmptyDocument()
    {
        Assert.Null(Resolve([], 0));
    }

    [Fact]
    public void Resolve_UsesDownwardFallbackAtTheFirstZone()
    {
        var text = new TextNoteBlock();

        Assert.Same(text, Resolve([text], 0));
    }

    [Fact]
    public void Resolve_UsesUpwardSearchAtTheTrailingZone()
    {
        var text = new TextNoteBlock();

        Assert.Same(text, Resolve([new ImageNoteBlock(), text], 2));
    }

    [Fact]
    public void Resolve_UsesTheNearestEditableBlockAbove()
    {
        var first = new TextNoteBlock();
        var nearest = new LinkListNoteBlock();

        Assert.Same(nearest, Resolve([first, new ImageNoteBlock(), nearest], 3));
    }

    [Fact]
    public void Resolve_UsesTheNearestEditableBlockBelow()
    {
        var nearest = new LinkListNoteBlock();
        var farther = new TextNoteBlock();

        Assert.Same(nearest, Resolve([new ImageNoteBlock(), nearest, new ImageNoteBlock(), farther], 0));
    }

    [Fact]
    public void Resolve_AlwaysPrefersAnEditableBlockAboveEvenWhenDownwardIsCloser()
    {
        var above = new TextNoteBlock();
        var below = new LinkListNoteBlock();

        Assert.Same(above, Resolve([above, new ImageNoteBlock(), new ImageNoteBlock(), below], 3));
    }

    [Fact]
    public void Resolve_TreatsAnEmptyLinkListAsEditable()
    {
        var emptyLinks = new LinkListNoteBlock();

        Assert.Empty(emptyLinks.Items);
        Assert.Same(emptyLinks, Resolve([emptyLinks], 1));
    }

    [Fact]
    public void Resolve_DoesNotMutateTheOrderedBlockCollection()
    {
        var blocks = new List<NoteBlock>
        {
            new TextNoteBlock(),
            new ImageNoteBlock(),
            new LinkListNoteBlock()
        };
        var before = blocks.ToArray();

        _ = InsertionZoneFocusResolver.Resolve(blocks, 2);

        Assert.Equal(before, blocks);
    }

    private static NoteBlock? Resolve(IReadOnlyList<NoteBlock> blocks, int insertionIndex) =>
        InsertionZoneFocusResolver.Resolve(blocks, insertionIndex);
}
