using FloatingTools.App.Models;
using FloatingTools.App.Notes;

namespace FloatingTools.Tests.Notes;

public sealed class AutomaticTextPlaceholderTests
{
    private readonly ActiveNoteEditor _editor = new();
    private static readonly ImageBlockData Image = new("image.png", "image.png", 100, 50);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeleteOnlyTextAboveContent_AppendsReplacementAfterAllContent(bool multiple)
    {
        var text = new TextNoteBlock { Text = "delete" };
        var image = new ImageNoteBlock();
        var note = new NoteDocument { Id = Guid.NewGuid(), Blocks = [text, image] };
        if (multiple) note.Blocks.Add(new LinkListNoteBlock());
        _editor.Subscribe(note);
        var content = note.Blocks.Skip(1).ToArray();
        var result = _editor.DeleteTextBlock(note, text, 30)!;
        Assert.Equal(content, note.Blocks.Take(content.Length));
        Assert.Same(result.FocusTarget, note.Blocks.Last());
        Assert.Empty(Assert.Single(note.Blocks.OfType<TextNoteBlock>()).Text);
    }

    [Fact]
    public void ImageInsertedWithoutText_AppendsOnePlaceholderWhichExplicitInsertionReplaces()
    {
        var anchor = new ImageNoteBlock();
        var note = new NoteDocument { Id = Guid.NewGuid(), Blocks = [anchor] };
        _editor.Subscribe(note);
        // Exercise the insertion operation's zero-text defensive path directly.
        note.Blocks.Remove(note.Blocks.OfType<TextNoteBlock>().Single());
        var result = _editor.InsertImageAfterBlock(note, anchor, Image, 300, 30)!;
        Assert.Equal([anchor, result.ImageBlock, result.FocusTarget], note.Blocks);
        Assert.Empty(result.FocusTarget.Text);
        var explicitBlock = _editor.InsertTextBlock(note, anchor, 30)!.Block;
        Assert.Equal([explicitBlock, anchor, result.ImageBlock], note.Blocks);
    }

    [Theory]
    [InlineData(0, 0, "hello")]
    [InlineData(5, 0, "hello")]
    [InlineData(0, 5, "")]
    public void ImagePasteAtBoundaryOrOverSelection_DoesNotCreateEmptySplitBlock(int start, int length, string remaining)
    {
        var text = new TextNoteBlock { Text = "hello" };
        var other = new TextNoteBlock { Text = "other", PreserveBoundaryBefore = true };
        var note = new NoteDocument { Id = Guid.NewGuid(), Blocks = [text, other] };
        _editor.Subscribe(note);
        var result = _editor.InsertImageIntoText(note, text, start, length, Image, 300, 30)!;
        Assert.Equal([text, other], note.Blocks.OfType<TextNoteBlock>());
        Assert.Equal(remaining, text.Text);
        Assert.Same(text, result.FocusTarget);
        Assert.Equal(start == 0 && length == 0 ? 0 : 1, note.Blocks.IndexOf(result.ImageBlock));
        Assert.True(_editor.TryPopUndoSnapshot(note, out var snapshot));
        _editor.RestoreBlocks(note, snapshot.Blocks, _ => { });
        Assert.Equal(["hello", "other"], note.Blocks.OfType<TextNoteBlock>().Select(block => block.Text));
        Assert.Equal(2, note.Blocks.Count);
    }

    [Theory]
    [InlineData("before-content")]
    [InlineData("before-placeholder")]
    [InlineData("end")]
    public void ExplicitInsertion_ReplacesOnlyPlaceholderAtRequestedPositionAndUndoRestoresItsIdentity(string position)
    {
        var image = new ImageNoteBlock();
        var note = new NoteDocument { Id = Guid.NewGuid(), Blocks = [image] };
        _editor.Subscribe(note);
        var placeholder = note.Blocks.OfType<TextNoteBlock>().Single();
        NoteBlock? anchor = position == "before-content" ? image : position == "before-placeholder" ? placeholder : null;
        var inserted = _editor.InsertTextBlock(note, anchor, 30)!.Block;
        Assert.Same(inserted, Assert.Single(note.Blocks.OfType<TextNoteBlock>()));
        Assert.Equal(position == "before-content" ? 0 : 1, note.Blocks.IndexOf(inserted));
        Assert.True(_editor.TryPopUndoSnapshot(note, out var snapshot));
        _editor.RestoreBlocks(note, snapshot.Blocks, _ => { });
        Assert.Equal(placeholder.Id, Assert.Single(note.Blocks.OfType<TextNoteBlock>()).Id);
        // Repeating the insertion after undo must still recognize the restored placeholder.
        var repeated = _editor.InsertTextBlock(note, note.Blocks[0], 30)!.Block;
        Assert.Same(repeated, Assert.Single(note.Blocks.OfType<TextNoteBlock>()));
    }

    [Fact]
    public void TwoExplicitEmptyBlocks_SurviveNormalizationAndUndo()
    {
        var note = new NoteDocument { Id = Guid.NewGuid() };
        _editor.Subscribe(note);
        var first = _editor.InsertTextBlock(note, null, 30)!.Block;
        var second = _editor.InsertTextBlock(note, null, 30)!.Block;
        Assert.False(_editor.NormalizeActiveTextBlocks(note));
        Assert.Equal([first, second], note.Blocks);
        Assert.True(_editor.TryPopUndoSnapshot(note, out var snapshot));
        _editor.RestoreBlocks(note, snapshot.Blocks, _ => { });
        Assert.Equal(first.Id, Assert.Single(note.Blocks).Id);
        _editor.InsertTextBlock(note, null, 30);
        Assert.Equal(2, note.Blocks.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadedOrPreviouslyTypedEmptyBlock_IsNeverTreatedAsAutomatic(bool previouslyTyped)
    {
        var note = new NoteDocument { Id = Guid.NewGuid() };
        if (!previouslyTyped) note.Blocks.Add(new TextNoteBlock());
        _editor.Subscribe(note);
        var original = note.Blocks.OfType<TextNoteBlock>().Single();
        if (previouslyTyped)
        {
            original.Text = "user text";
            original.Text = "";
        }
        var inserted = _editor.InsertTextBlock(note, null, 30)!.Block;
        Assert.Equal([original, inserted], note.Blocks);
        Assert.False(_editor.NormalizeActiveTextBlocks(note));
    }

    [Fact]
    public void TwoExplicitEmptyBlocks_RemainSeparateWhenInterveningImageIsDeleted()
    {
        var note = new NoteDocument { Id = Guid.NewGuid() };
        _editor.Subscribe(note);
        var first = _editor.InsertTextBlock(note, null, 30)!.Block;
        var image = _editor.InsertImageAfterBlock(note, first, Image, 300, 30)!.ImageBlock;
        var second = _editor.InsertTextBlock(note, null, 30)!.Block;

        _editor.DeleteImage(note, image, 30);

        Assert.Equal([first, second], note.Blocks);
    }
}
