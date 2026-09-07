using FloatingTools.App.Models;
using FloatingTools.App.Notes;

namespace FloatingTools.Tests.Notes;

public sealed class ActiveNoteEditorTests
{
    private readonly ActiveNoteEditor _editor = new();

    [Fact]
    public void CloneBlocks_PreservesOrderAndCreatesIndependentBlocks()
    {
        var text = new TextNoteBlock { Text = "one" };
        var image = new ImageNoteBlock { AssetFileName = "two.png", DisplayWidth = 42 };
        var links = new LinkListNoteBlock { Items = [new NoteLinkItem { Url = "https://three.test" }] };

        var clone = _editor.CloneBlocks([text, image, links]);
        ((TextNoteBlock)clone[0]).Text = "changed";
        ((LinkListNoteBlock)clone[2]).Items[0].Url = "https://changed.test";

        Assert.Collection(clone,
            block => Assert.IsType<TextNoteBlock>(block),
            block => Assert.IsType<ImageNoteBlock>(block),
            block => Assert.IsType<LinkListNoteBlock>(block));
        Assert.Equal("one", text.Text);
        Assert.Equal("https://three.test", links.Items[0].Url);
    }

    [Fact]
    public void CloneBlock_TextPreservesCurrentFields()
    {
        var id = Guid.NewGuid();
        var source = new TextNoteBlock { Id = id, Text = "text", PreserveBoundaryBefore = true };

        var clone = Assert.IsType<TextNoteBlock>(_editor.CloneBlock(source));

        Assert.Equal(id, clone.Id);
        Assert.Equal("text", clone.Text);
        Assert.True(clone.PreserveBoundaryBefore);
    }

    [Fact]
    public void CloneBlock_ImagePreservesCurrentFields()
    {
        var source = new ImageNoteBlock
        {
            Id = Guid.NewGuid(), AssetFileName = "image.png", NaturalWidth = 100,
            NaturalHeight = 40, DisplayWidth = 80, AssetPath = "C:\\image.png"
        };

        var clone = Assert.IsType<ImageNoteBlock>(_editor.CloneBlock(source));

        Assert.Equivalent(source, clone, strict: true);
    }

    [Fact]
    public void CloneBlock_LinkListPreservesItemsAndIdsAsDeepCopies()
    {
        var source = new LinkListNoteBlock
        {
            Id = Guid.NewGuid(),
            Items = [new NoteLinkItem { Id = Guid.NewGuid(), Url = "https://one.test", DisplayName = "One" }]
        };

        var clone = Assert.IsType<LinkListNoteBlock>(_editor.CloneBlock(source));

        Assert.Equal(source.Id, clone.Id);
        Assert.Equal(source.Items[0].Id, clone.Items[0].Id);
        Assert.Equal(source.Items[0].Url, clone.Items[0].Url);
        Assert.NotSame(source.Items[0], clone.Items[0]);
    }

    [Fact]
    public void SelectImage_ReplacesAndClearsTheEditorSelection()
    {
        var first = new ImageNoteBlock();
        var second = new ImageNoteBlock();
        var changes = 0;
        _editor.SelectedImageChanged += (_, _) => changes++;

        _editor.SelectImage(first);
        Assert.Same(first, _editor.SelectedImageBlock);
        Assert.True(first.IsSelected);

        _editor.SelectImage(second);
        Assert.Same(second, _editor.SelectedImageBlock);
        Assert.False(first.IsSelected);
        Assert.True(second.IsSelected);

        _editor.SelectImage(null);
        Assert.Null(_editor.SelectedImageBlock);
        Assert.False(second.IsSelected);
        Assert.Equal(3, changes);
    }

    [Fact]
    public void GetImageAssetNames_ReturnsOnlyCurrentImageReferences()
    {
        var note = new NoteDocument
        {
            Blocks =
            [
                new TextNoteBlock { Text = "not-an-asset" },
                new ImageNoteBlock { AssetFileName = "Current.png" },
                new ImageNoteBlock { AssetFileName = "current.PNG" },
                new ImageNoteBlock { AssetFileName = " " }
            ]
        };

        Assert.Equal(["Current.png"], _editor.GetImageAssetNames(note));
        Assert.DoesNotContain("unreferenced.png", _editor.GetImageAssetNames(note));
    }

    [Fact]
    public void InsertImageIntoText_EmptyTextKeepsExistingEditorWithOneSnapshot()
    {
        var target = new TextNoteBlock();
        var note = new NoteDocument { Id = Guid.NewGuid(), Blocks = [target] };
        _editor.Subscribe(note);
        var mutations = 0;
        _editor.NoteMutated += _ => mutations++;

        var result = _editor.InsertImageIntoText(
            note,
            target,
            0,
            0,
            new ImageBlockData("image.png", "C:\\assets\\image.png", 640, 320),
            300,
            30);

        Assert.NotNull(result);
        Assert.Equal([target, result.ImageBlock], note.Blocks);
        Assert.Same(target, result.FocusTarget);
        Assert.Equal("image.png", result.ImageBlock.AssetFileName);
        Assert.Equal("C:\\assets\\image.png", result.ImageBlock.AssetPath);
        Assert.Equal(300, result.ImageBlock.DisplayWidth);
        Assert.Equal(2, result.ImageBlock.AspectRatio);
        Assert.Empty(target.Text);
        Assert.Empty(result.FocusTarget.Text);
        Assert.Null(_editor.SelectedImageBlock);
        Assert.Equal(0, mutations);

        Assert.True(_editor.TryPopUndoSnapshot(note, out var snapshot));
        _editor.RestoreBlocks(note, snapshot.Blocks, _ => { });
        Assert.Equal(target.Id, Assert.IsType<TextNoteBlock>(Assert.Single(note.Blocks)).Id);
        Assert.False(_editor.TryPopUndoSnapshot(note, out _));
    }

    [Fact]
    public void InsertImageAfterBlock_PreservesExistingTextOrderAndSelection()
    {
        var first = new TextNoteBlock { Text = "before" };
        var links = new LinkListNoteBlock();
        var selected = new ImageNoteBlock { AssetFileName = "selected.png" };
        var last = new TextNoteBlock { Text = "after" };
        var note = new NoteDocument { Id = Guid.NewGuid(), Blocks = [first, links, selected, last] };
        _editor.Subscribe(note);
        _editor.SelectImage(selected);

        var result = _editor.InsertImageAfterBlock(
            note,
            links,
            new ImageBlockData("inserted.png", "C:\\assets\\inserted.png", 200, 100),
            400,
            30);

        Assert.NotNull(result);
        Assert.Equal(
            [first, links, result.ImageBlock, selected, last],
            note.Blocks);
        Assert.Same(last, result.FocusTarget);
        Assert.Same(selected, _editor.SelectedImageBlock);
        Assert.True(selected.IsSelected);
        Assert.True(_editor.TryPopUndoSnapshot(note, out var snapshot));
        _editor.RestoreBlocks(note, snapshot.Blocks, _ => { });
        Assert.Equal([first.Id, links.Id, selected.Id, last.Id], note.Blocks.Select(block => block.Id));
    }

    [Fact]
    public void InsertImageIntoText_SplitsTextAndUndoRestoresExactPreviousStructure()
    {
        var target = new TextNoteBlock { Id = Guid.NewGuid(), Text = "before after" };
        var note = new NoteDocument { Id = Guid.NewGuid(), Blocks = [target] };
        _editor.Subscribe(note);

        var result = _editor.InsertImageIntoText(
            note,
            target,
            7,
            0,
            new ImageBlockData("split.png", "C:\\assets\\split.png", 100, 50),
            300,
            30);

        Assert.NotNull(result);
        Assert.Equal("before ", target.Text);
        Assert.Equal("after", result.FocusTarget.Text);
        Assert.Equal([target, result.ImageBlock, result.FocusTarget], note.Blocks);

        Assert.True(_editor.TryPopUndoSnapshot(note, out var snapshot));
        _editor.RestoreBlocks(note, snapshot.Blocks, _ => { });
        var restored = Assert.IsType<TextNoteBlock>(Assert.Single(note.Blocks));
        Assert.Equal(target.Id, restored.Id);
        Assert.Equal("before after", restored.Text);
    }

    [Fact]
    public void DeleteImage_ClearsSelectionNormalizesTextAndKeepsUndoAssetReferenced()
    {
        var before = new TextNoteBlock { Text = "before" };
        var image = new ImageNoteBlock { AssetFileName = "deleted.png" };
        var after = new TextNoteBlock { Text = "after" };
        var note = new NoteDocument { Id = Guid.NewGuid(), Blocks = [before, image, after] };
        _editor.Subscribe(note);
        _editor.SelectImage(image);

        var result = _editor.DeleteImage(note, image, 30);

        Assert.NotNull(result);
        Assert.Equal("deleted.png", result.AssetFileName);
        Assert.Null(_editor.SelectedImageBlock);
        Assert.False(image.IsSelected);
        Assert.Equal("beforeafter", Assert.IsType<TextNoteBlock>(Assert.Single(note.Blocks)).Text);
        Assert.Equal(["deleted.png"], _editor.GetReferencedUndoAssetNames());

        Assert.True(_editor.TryPopUndoSnapshot(note, out var snapshot));
        _editor.RestoreBlocks(note, snapshot.Blocks, restored =>
            restored.AssetPath = "C:\\assets\\" + restored.AssetFileName);
        Assert.Equal([before.Id, image.Id, after.Id], note.Blocks.Select(block => block.Id));
        Assert.Equal("C:\\assets\\deleted.png", Assert.IsType<ImageNoteBlock>(note.Blocks[1]).AssetPath);
    }

    [Fact]
    public void ResizeImage_ClampsLiveStateWithoutUndoAndCommitCreatesOneSnapshot()
    {
        var image = new ImageNoteBlock
        {
            AssetFileName = "resize.png",
            NaturalWidth = 400,
            NaturalHeight = 200,
            DisplayWidth = 100
        };
        var note = new NoteDocument { Id = Guid.NewGuid(), Blocks = [image] };
        _editor.Subscribe(note);

        _editor.ResizeImage(image, 900, 320);

        Assert.Equal(320, image.DisplayWidth);
        Assert.Equal(2, image.AspectRatio);
        Assert.False(_editor.HasUndoHistory(note));

        var result = _editor.CommitImageResize(note, image, 290, 350, 30);

        Assert.NotNull(result);
        Assert.Equal(290, image.DisplayWidth);
        Assert.True(_editor.TryPopUndoSnapshot(note, out var snapshot));
        Assert.Collection(snapshot.Blocks,
            block => Assert.Equal(320, Assert.IsType<ImageNoteBlock>(block).DisplayWidth),
            block => Assert.Empty(Assert.IsType<TextNoteBlock>(block).Text));
        Assert.False(_editor.TryPopUndoSnapshot(note, out _));
    }

    [Fact]
    public void InsertLinkListBlock_UsesRequestedStartMiddleAndEndPositionsWithUndo()
    {
        var first = new TextNoteBlock { Text = "first" };
        var image = new ImageNoteBlock { AssetFileName = "image.png" };
        var last = new TextNoteBlock { Text = "last" };
        var note = new NoteDocument { Id = Guid.NewGuid(), Blocks = [first, image, last] };
        _editor.Subscribe(note);

        var atStart = _editor.InsertLinkListBlock(note, first, 30);
        var inMiddle = _editor.InsertLinkListBlock(note, image, 30);
        var atEnd = _editor.InsertLinkListBlock(note, null, 30);

        Assert.NotNull(atStart);
        Assert.NotNull(inMiddle);
        Assert.NotNull(atEnd);
        Assert.Equal(
            [atStart.Block, first, inMiddle.Block, image, last, atEnd.Block],
            note.Blocks);
        Assert.Empty(atStart.Block.Items);
        Assert.True(_editor.TryPopUndoSnapshot(note, out var snapshot));
        _editor.RestoreBlocks(note, snapshot.Blocks, _ => { });
        Assert.Equal(
            [atStart.Block.Id, first.Id, inMiddle.Block.Id, image.Id, last.Id],
            note.Blocks.Select(block => block.Id));
    }

    [Fact]
    public void CommitLinkTokens_PreservesOrderDuplicatesInvalidDraftAndUndo()
    {
        var block = new LinkListNoteBlock();
        var note = new NoteDocument { Id = Guid.NewGuid(), Blocks = [block] };
        _editor.Subscribe(note);

        var result = _editor.CommitLinkTokens(
            block,
            "https://one.test not-a-url https://two.test https://one.test",
            30);

        Assert.Equal("not-a-url", result.InvalidDraft);
        Assert.Equal(
            ["https://one.test", "https://two.test", "https://one.test"],
            result.AcceptedItems.Select(item => item.Url));
        Assert.Equal(
            ["https://one.test", "https://two.test", "https://one.test"],
            block.Items.Select(item => item.DisplayName));
        Assert.True(_editor.TryPopUndoSnapshot(note, out var snapshot));
        _editor.RestoreBlocks(note, snapshot.Blocks, _ => { });
        Assert.Collection(note.Blocks,
            block => Assert.Empty(Assert.IsType<LinkListNoteBlock>(block).Items),
            block => Assert.Empty(Assert.IsType<TextNoteBlock>(block).Text));
    }

    [Fact]
    public void EditLinkItem_UpdatesOnlyTheRequestedItemPreservesIdAndUndo()
    {
        var first = new NoteLinkItem { Url = "https://first.test", DisplayName = "First" };
        var second = new NoteLinkItem { Url = "https://second.test", DisplayName = "Second" };
        var block = new LinkListNoteBlock { Items = [first, second] };
        var note = new NoteDocument { Id = Guid.NewGuid(), Blocks = [block] };
        _editor.Subscribe(note);

        var result = _editor.EditLinkItem(block, second, "  ", "https://renamed.test", 30);

        Assert.NotNull(result);
        Assert.Equal("https://first.test", first.Url);
        Assert.Equal("https://renamed.test", second.Url);
        Assert.Equal(second.Url, second.DisplayName);
        var secondId = second.Id;
        Assert.Null(_editor.EditLinkItem(block, second, "Ignored", "file:///c:/x", 30));
        Assert.Equal(secondId, second.Id);
        Assert.True(_editor.TryPopUndoSnapshot(note, out var snapshot));
        _editor.RestoreBlocks(note, snapshot.Blocks, _ => { });
        Assert.Collection(note.Blocks, actual => Assert.IsType<LinkListNoteBlock>(actual),
            actual => Assert.Empty(Assert.IsType<TextNoteBlock>(actual).Text));
        var restored = Assert.IsType<LinkListNoteBlock>(note.Blocks[0]);
        Assert.Equal(["https://first.test", "https://second.test"], restored.Items.Select(item => item.Url));
        Assert.Equal(secondId, restored.Items[1].Id);
    }

    [Fact]
    public void DeleteLinkItemAndBackspace_PreserveOrderThenRemoveTheEmptyBlock()
    {
        var before = new TextNoteBlock { Text = "before" };
        var first = new NoteLinkItem { Url = "https://first.test" };
        var second = new NoteLinkItem { Url = "https://second.test" };
        var block = new LinkListNoteBlock { Items = [first, second] };
        var after = new TextNoteBlock { Text = "after" };
        var note = new NoteDocument { Id = Guid.NewGuid(), Blocks = [before, block, after] };
        _editor.Subscribe(note);

        Assert.NotNull(_editor.DeleteLinkItem(block, first, 30));
        Assert.Equal([second], block.Items);
        Assert.True(_editor.TryPopUndoSnapshot(note, out var itemSnapshot));
        _editor.RestoreBlocks(note, itemSnapshot.Blocks, _ => { });
        block = Assert.IsType<LinkListNoteBlock>(note.Blocks[1]);

        var firstBackspace = _editor.DeleteLastLinkItemOrBlock(block, 30);
        var secondBackspace = _editor.DeleteLastLinkItemOrBlock(block, 30);
        var emptyBackspace = _editor.DeleteLastLinkItemOrBlock(block, 30);

        Assert.False(firstBackspace!.BlockDeleted);
        Assert.False(secondBackspace!.BlockDeleted);
        Assert.True(emptyBackspace!.BlockDeleted);
        Assert.Equal("beforeafter", Assert.IsType<TextNoteBlock>(Assert.Single(note.Blocks)).Text);
    }

    [Fact]
    public void LinkListHistories_AreIndependentPerNote()
    {
        var aBlock = new LinkListNoteBlock();
        var bBlock = new LinkListNoteBlock();
        var a = new NoteDocument { Id = Guid.NewGuid(), Blocks = [aBlock] };
        var b = new NoteDocument { Id = Guid.NewGuid(), Blocks = [bBlock] };
        _editor.Subscribe(a);
        _editor.Subscribe(b);

        _editor.CommitLinkTokens(aBlock, "https://a.test", 30);
        _editor.CommitLinkTokens(bBlock, "https://b.test", 30);

        Assert.True(_editor.TryPopUndoSnapshot(a, out var aSnapshot));
        Assert.Collection(aSnapshot.Blocks,
            block => Assert.Empty(Assert.IsType<LinkListNoteBlock>(block).Items),
            block => Assert.Empty(Assert.IsType<TextNoteBlock>(block).Text));
        Assert.True(_editor.HasUndoHistory(b));
        Assert.Equal("https://b.test", Assert.Single(bBlock.Items).Url);
    }

    [Fact]
    public void NormalizeTextBlocksCore_MergesAdjacentTextUnlessBoundaryIsPreserved()
    {
        var note = new NoteDocument
        {
            Blocks = [new TextNoteBlock { Text = "one" }, new TextNoteBlock { Text = "two" }]
        };

        var changed = _editor.NormalizeTextBlocksCore(note);

        Assert.True(changed);
        Assert.Equal("onetwo", Assert.IsType<TextNoteBlock>(Assert.Single(note.Blocks)).Text);
    }

    [Fact]
    public void NormalizeTextBlocksCore_PreservesExplicitBoundaryAndEmptyBlocks()
    {
        var first = new TextNoteBlock { Text = "one" };
        var boundary = new TextNoteBlock { Text = string.Empty, PreserveBoundaryBefore = true };
        var note = new NoteDocument { Blocks = [first, boundary] };

        var changed = _editor.NormalizeTextBlocks(note);

        Assert.False(changed);
        Assert.Equal([first, boundary], note.Blocks);
        Assert.Equal(string.Empty, boundary.Text);
    }

    [Fact]
    public void NormalizeTextBlocksCore_PreservesSurvivingBlockIdAndOrder()
    {
        var first = new TextNoteBlock { Id = Guid.NewGuid(), Text = "a" };
        var second = new TextNoteBlock { Id = Guid.NewGuid(), Text = "b" };
        var image = new ImageNoteBlock { Id = Guid.NewGuid(), AssetFileName = "image.png" };
        var note = new NoteDocument { Blocks = [first, second, image] };

        _editor.NormalizeTextBlocksCore(note);

        Assert.Equal([first, image], note.Blocks);
        Assert.Equal("ab", first.Text);
        Assert.Equal(first.Id, note.Blocks[0].Id);
    }

    [Fact]
    public void Subscribe_TracksExistingAndNewBlocksWithoutDuplicates()
    {
        var existing = new TextNoteBlock();
        var note = new NoteDocument { Blocks = [existing] };
        var mutations = 0;
        _editor.NoteMutated += _ => mutations++;

        _editor.Subscribe(note);
        _editor.Subscribe(note);
        var added = new TextNoteBlock();
        note.Blocks.Add(added);
        added.Text = "changed";

        Assert.True(_editor.TryGetOwner(existing, out var existingOwner));
        Assert.True(_editor.TryGetOwner(added, out var addedOwner));
        Assert.Same(note, existingOwner);
        Assert.Same(note, addedOwner);
        Assert.Equal(2, mutations);
    }

    [Fact]
    public void RemovingOrUnsubscribingBlock_StopsOwnershipAndMutations()
    {
        var block = new TextNoteBlock();
        var note = new NoteDocument { Blocks = [block] };
        var mutations = 0;
        _editor.NoteMutated += _ => mutations++;
        _editor.Subscribe(note);

        note.Blocks.Remove(block);
        block.Text = "old";
        _editor.Unsubscribe(note);

        Assert.False(_editor.TryGetOwner(block, out _));
        Assert.Equal(1, mutations);
    }

    [Fact]
    public void SubscribedBlockMutation_RaisesOnceAndOldNoteDoesNotFireAfterSwitch()
    {
        var aBlock = new TextNoteBlock();
        var bBlock = new TextNoteBlock();
        var a = new NoteDocument { Blocks = [aBlock] };
        var b = new NoteDocument { Blocks = [bBlock] };
        var mutations = new List<NoteDocument>();
        _editor.NoteMutated += mutations.Add;

        _editor.Subscribe(a);
        _editor.Unsubscribe(a);
        _editor.Subscribe(b);
        _editor.Subscribe(a);
        aBlock.Text = "a";
        bBlock.Text = "b";

        Assert.Equal([a, b], mutations);
    }

    [Fact]
    public void NotePropertyChange_IsForwardedWithoutBecomingBlockMutation()
    {
        var note = new NoteDocument { Blocks = [new TextNoteBlock()] };
        var changes = 0;
        var mutations = 0;
        _editor.NotePropertyChanged += (_, args) =>
        {
            Assert.Same(note, args.Note);
            Assert.Equal(nameof(NoteDocument.Title), args.Change.PropertyName);
            changes++;
        };
        _editor.NoteMutated += _ => mutations++;
        _editor.Subscribe(note);

        note.Title = "renamed";

        Assert.Equal(1, changes);
        Assert.Equal(0, mutations);
    }

    [Fact]
    public void Suppression_PreventsMutationsAndReleasesAfterDisposeAndException()
    {
        var block = new TextNoteBlock();
        var note = new NoteDocument { Blocks = [block] };
        var mutations = 0;
        _editor.NoteMutated += _ => mutations++;
        _editor.Subscribe(note);

        using (_editor.SuppressChangeTracking())
        {
            block.Text = "suppressed";
            using (_editor.SuppressChangeTracking()) block.Text = "still suppressed";
        }
        Action throwsDuringSuppression = () =>
        {
            using (_editor.SuppressChangeTracking()) throw new InvalidOperationException();
        };
        Assert.Throws<InvalidOperationException>(throwsDuringSuppression);
        block.Text = "tracked";

        Assert.Equal(1, mutations);
    }

    [Fact]
    public void CollectionMutationDuringSuppression_TracksNewBlockAfterScope()
    {
        var note = new NoteDocument { Blocks = [new TextNoteBlock()] };
        var mutations = 0;
        _editor.NoteMutated += _ => mutations++;
        _editor.Subscribe(note);
        var added = new TextNoteBlock();

        using (_editor.SuppressChangeTracking()) note.Blocks.Add(added);
        added.Text = "tracked";

        Assert.True(_editor.TryGetOwner(added, out var owner));
        Assert.Same(note, owner);
        Assert.Equal(1, mutations);
    }

    [Fact]
    public void UndoSnapshots_AreLifoAndIsolatedPerNote()
    {
        var a = new NoteDocument { Id = Guid.NewGuid(), Blocks = [new TextNoteBlock { Text = "a1" }] };
        var b = new NoteDocument { Id = Guid.NewGuid(), Blocks = [new TextNoteBlock { Text = "b1" }] };
        _editor.PushUndoSnapshot(a, 30);
        ((TextNoteBlock)a.Blocks[0]).Text = "a2";
        _editor.PushUndoSnapshot(a, 30);
        _editor.PushUndoSnapshot(b, 30);

        Assert.True(_editor.TryPopUndoSnapshot(a, out var latestA));
        Assert.Equal("a2", Assert.IsType<TextNoteBlock>(latestA.Blocks[0]).Text);
        Assert.True(_editor.TryPopUndoSnapshot(b, out var firstB));
        Assert.Equal("b1", Assert.IsType<TextNoteBlock>(firstB.Blocks[0]).Text);
        Assert.True(_editor.TryPopUndoSnapshot(a, out var firstA));
        Assert.Equal("a1", Assert.IsType<TextNoteBlock>(firstA.Blocks[0]).Text);
        Assert.False(_editor.TryPopUndoSnapshot(a, out _));
    }

    [Fact]
    public void DiscardUndoHistory_RemovesOnlyRequestedNote()
    {
        var a = new NoteDocument { Id = Guid.NewGuid(), Blocks = [new ImageNoteBlock { AssetFileName = "a.png" }] };
        var b = new NoteDocument { Id = Guid.NewGuid(), Blocks = [new ImageNoteBlock { AssetFileName = "b.png" }] };
        _editor.PushUndoSnapshot(a, 30);
        _editor.PushUndoSnapshot(b, 30);

        var discarded = _editor.DiscardUndoHistory(a.Id);

        Assert.Equal(["a.png"], discarded);
        Assert.False(_editor.HasUndoHistory(a));
        Assert.True(_editor.HasUndoHistory(b));
        Assert.Equal(["b.png"], _editor.GetReferencedUndoAssetNames());
    }

    [Fact]
    public void RestoreBlocks_PreservesBlockDataAndDoesNotEmitMutation()
    {
        var note = new NoteDocument { Blocks = [new TextNoteBlock { Text = "current" }] };
        _editor.Subscribe(note);
        var mutations = 0;
        _editor.NoteMutated += _ => mutations++;
        var imageId = Guid.NewGuid();
        var snapshot = new NoteBlock[]
        {
            new TextNoteBlock { Id = Guid.NewGuid(), Text = "text" },
            new ImageNoteBlock { Id = imageId, AssetFileName = "image.png", DisplayWidth = 42 },
            new LinkListNoteBlock { Items = [new NoteLinkItem { Url = "https://link.test", DisplayName = "Link" }] }
        };

        _editor.RestoreBlocks(note, snapshot, image => image.AssetPath = "resolved/" + image.AssetFileName);

        Assert.Equal(3, note.Blocks.Count);
        Assert.Equal("text", Assert.IsType<TextNoteBlock>(note.Blocks[0]).Text);
        var image = Assert.IsType<ImageNoteBlock>(note.Blocks[1]);
        Assert.Equal(imageId, image.Id);
        Assert.Equal("resolved/image.png", image.AssetPath);
        Assert.Equal("https://link.test", Assert.IsType<LinkListNoteBlock>(note.Blocks[2]).Items[0].Url);
        Assert.Equal(0, mutations);
    }

    [Fact]
    public void RestoreBlocks_PreservesNormalizationAndDoesNotCreateHistory()
    {
        var note = new NoteDocument { Blocks = [new TextNoteBlock { Text = "current" }] };
        _editor.RestoreBlocks(note, [new TextNoteBlock { Text = "one" }, new TextNoteBlock { Text = "two" }], _ => { });

        Assert.Equal("onetwo", Assert.IsType<TextNoteBlock>(Assert.Single(note.Blocks)).Text);
        Assert.False(_editor.HasUndoHistory(note));
    }

    [Fact]
    public void UndoHistoryDepth_EvictsOldestSnapshotAndExposesItsAsset()
    {
        var note = new NoteDocument { Blocks = [new ImageNoteBlock { AssetFileName = "old.png" }] };
        _editor.PushUndoSnapshot(note, 1);
        ((ImageNoteBlock)note.Blocks[0]).AssetFileName = "new.png";

        var evicted = _editor.PushUndoSnapshot(note, 1);

        Assert.Equal(["old.png"], evicted);
        Assert.Equal(["new.png"], _editor.GetReferencedUndoAssetNames());
    }

    [Fact]
    public async Task ScheduleDebouncedSave_InvokesCallbackOnceAfterDelay()
    {
        var editor = new ActiveNoteEditor(TimeSpan.FromMilliseconds(5));
        var calls = 0;

        await editor.ScheduleDebouncedSave(_ =>
        {
            calls++;
            return Task.CompletedTask;
        });

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ScheduleDebouncedSave_ReplacesEarlierPendingCallback()
    {
        var editor = new ActiveNoteEditor(TimeSpan.FromMilliseconds(30));
        var calls = new List<string>();
        var first = editor.ScheduleDebouncedSave(_ =>
        {
            calls.Add("A");
            return Task.CompletedTask;
        });
        var second = editor.ScheduleDebouncedSave(_ =>
        {
            calls.Add("B");
            return Task.CompletedTask;
        });

        await Task.WhenAll(first, second);

        Assert.Equal(["B"], calls);
    }

    [Fact]
    public async Task CancelPendingSave_PreventsCallbackAndDoesNotSurfaceCancellation()
    {
        var editor = new ActiveNoteEditor(TimeSpan.FromMilliseconds(30));
        var calls = 0;
        var pending = editor.ScheduleDebouncedSave(_ =>
        {
            calls++;
            return Task.CompletedTask;
        });

        editor.CancelPendingSave();
        await pending;

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task ScheduleDebouncedSave_DoesNotSwallowUnexpectedCallbackException()
    {
        var editor = new ActiveNoteEditor(TimeSpan.Zero);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            editor.ScheduleDebouncedSave(_ => throw new InvalidOperationException("save failed")));
    }

    [Fact]
    public void InsertTextBlock_ReplacesAutomaticPlaceholderWithOneUndoSnapshot()
    {
        var note = new NoteDocument { Id = Guid.NewGuid(), Blocks = [] };
        _editor.Subscribe(note);
        var mutations = 0;
        _editor.NoteMutated += _ => mutations++;

        var result = _editor.InsertTextBlock(note, null, 30);

        Assert.NotNull(result);
        Assert.Same(result.Block, Assert.Single(note.Blocks));
        Assert.Equal(string.Empty, result.Block.Text);
        Assert.False(result.Block.PreserveBoundaryBefore);
        Assert.True(_editor.HasUndoHistory(note));
        Assert.Equal(0, mutations);
    }

    [Fact]
    public void InsertTextBlock_PreservesRequestedIndexAroundImageAndLinkList()
    {
        var first = new TextNoteBlock { Id = Guid.NewGuid(), Text = "first" };
        var image = new ImageNoteBlock { Id = Guid.NewGuid(), AssetFileName = "image.png" };
        var links = new LinkListNoteBlock { Id = Guid.NewGuid() };
        var last = new TextNoteBlock { Id = Guid.NewGuid(), Text = "last" };
        var note = new NoteDocument { Id = Guid.NewGuid(), Blocks = [first, image, links, last] };
        _editor.Subscribe(note);

        var beforeImage = _editor.InsertTextBlock(note, image, 30);
        var beforeLinks = _editor.InsertTextBlock(note, links, 30);
        var atEnd = _editor.InsertTextBlock(note, null, 30);

        Assert.Equal([first, beforeImage!.Block, image, beforeLinks!.Block, links, last, atEnd!.Block], note.Blocks);
        Assert.True(beforeImage.Block.PreserveBoundaryBefore);
        Assert.True(beforeLinks.Block.PreserveBoundaryBefore);
        Assert.True(atEnd.Block.PreserveBoundaryBefore);
        Assert.Equal(first.Id, note.Blocks[0].Id);
        Assert.Equal(image.Id, note.Blocks[2].Id);
    }

    [Fact]
    public void InsertTextBlock_BetweenTextBlocksPreservesBoundariesAndUndoRestoresPriorState()
    {
        var first = new TextNoteBlock { Id = Guid.NewGuid(), Text = "one" };
        var second = new TextNoteBlock { Id = Guid.NewGuid(), Text = "two" };
        var note = new NoteDocument { Id = Guid.NewGuid(), Blocks = [first, second] };
        _editor.Subscribe(note);

        var result = _editor.InsertTextBlock(note, second, 30);

        Assert.NotNull(result);
        Assert.True(result.Block.PreserveBoundaryBefore);
        Assert.True(second.PreserveBoundaryBefore);
        Assert.Equal([first, result.Block, second], note.Blocks);
        Assert.True(_editor.TryPopUndoSnapshot(note, out var snapshot));
        _editor.RestoreBlocks(note, snapshot.Blocks, _ => { });
        var restored = Assert.IsType<TextNoteBlock>(Assert.Single(note.Blocks));
        Assert.Equal(first.Id, restored.Id);
        Assert.Equal("onetwo", restored.Text);
    }

    [Fact]
    public void InsertTextBlock_InvalidAnchorDoesNotMutateOrCreateUndoHistory()
    {
        var note = new NoteDocument { Id = Guid.NewGuid(), Blocks = [new TextNoteBlock()] };
        var other = new NoteDocument { Id = Guid.NewGuid(), Blocks = [new TextNoteBlock()] };
        _editor.Subscribe(note);
        _editor.Subscribe(other);

        var result = _editor.InsertTextBlock(note, other.Blocks[0], 30);

        Assert.Null(result);
        Assert.Single(note.Blocks);
        Assert.False(_editor.HasUndoHistory(note));
    }

    [Fact]
    public void DeleteTextBlock_ReturnsHistoricalFocusAndOneUndoSnapshot()
    {
        var first = new TextNoteBlock { Id = Guid.NewGuid(), Text = "first" };
        var middle = new TextNoteBlock { Id = Guid.NewGuid(), Text = "middle", PreserveBoundaryBefore = true };
        var last = new TextNoteBlock { Id = Guid.NewGuid(), Text = "last", PreserveBoundaryBefore = true };
        var note = new NoteDocument { Id = Guid.NewGuid(), Blocks = [first, middle, last] };
        _editor.Subscribe(note);
        var mutations = 0;
        _editor.NoteMutated += _ => mutations++;

        var result = _editor.DeleteTextBlock(note, middle, 30);

        Assert.NotNull(result);
        Assert.Same(last, result.FocusTarget);
        Assert.Equal([first, last], note.Blocks);
        Assert.True(_editor.HasUndoHistory(note));
        Assert.Equal(0, mutations);
    }

    [Fact]
    public void DeleteTextBlock_AroundImageAndLinkListPreservesOrderAndUndo()
    {
        var image = new ImageNoteBlock { AssetFileName = "image.png" };
        var target = new TextNoteBlock { Text = "delete" };
        var links = new LinkListNoteBlock();
        var note = new NoteDocument { Id = Guid.NewGuid(), Blocks = [image, target, links] };
        _editor.Subscribe(note);

        var result = _editor.DeleteTextBlock(note, target, 30);

        var replacement = Assert.Single(note.Blocks.OfType<TextNoteBlock>());
        Assert.Same(replacement, result!.FocusTarget);
        Assert.Empty(replacement.Text);
        Assert.Equal([image, links, replacement], note.Blocks);
        Assert.True(_editor.TryPopUndoSnapshot(note, out var snapshot));
        _editor.RestoreBlocks(note, snapshot.Blocks, _ => { });
        Assert.Equal([image.Id, target.Id, links.Id], note.Blocks.Select(block => block.Id));
    }

    [Fact]
    public void MergeEmptyTextBlockBackward_UsesExistingPreviousTextRule()
    {
        var first = new TextNoteBlock { Text = "first" };
        var image = new ImageNoteBlock { AssetFileName = "image.png" };
        var empty = new TextNoteBlock { Text = string.Empty };
        var note = new NoteDocument { Id = Guid.NewGuid(), Blocks = [first, image, empty] };
        _editor.Subscribe(note);

        var result = _editor.MergeEmptyTextBlockBackward(note, empty, 30);

        Assert.NotNull(result);
        Assert.Same(first, result.FocusTarget);
        Assert.Equal([first, image], note.Blocks);
        Assert.Null(_editor.MergeEmptyTextBlockBackward(note, first, 30));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemoveOnlyTextBlock_ReturnsEmptySubscribedReplacementAndSupportsUndo(bool backspace)
    {
        var original = new TextNoteBlock { Text = backspace ? "" : "deleted text" };
        var note = new NoteDocument { Id = Guid.NewGuid(), Blocks = [original] };
        _editor.Subscribe(note);

        var result = backspace
            ? _editor.MergeEmptyTextBlockBackward(note, original, 30)
            : _editor.DeleteTextBlock(note, original, 30);

        var replacement = Assert.IsType<TextNoteBlock>(Assert.Single(note.Blocks));
        Assert.NotSame(original, replacement);
        Assert.Empty(replacement.Text);
        Assert.Same(replacement, result!.FocusTarget);
        Assert.True(_editor.TryGetOwner(replacement, out var owner));
        Assert.Same(note, owner);
        Assert.False(_editor.TryGetOwner(original, out _));
        Assert.True(_editor.TryPopUndoSnapshot(note, out var snapshot));
        Assert.Equal(original.Id, Assert.Single(snapshot.Blocks).Id);
        Assert.False(_editor.HasUndoHistory(note));
        _editor.RestoreBlocks(note, snapshot.Blocks, _ => { });
        Assert.Equal(original.Text, Assert.IsType<TextNoteBlock>(Assert.Single(note.Blocks)).Text);
    }

    [Fact]
    public void BackspaceLastTextAfterNonTextBlocks_ReturnsReplacementWithoutDeletingOtherBlocks()
    {
        var image = new ImageNoteBlock();
        var links = new LinkListNoteBlock();
        var empty = new TextNoteBlock();
        var note = new NoteDocument { Id = Guid.NewGuid(), Blocks = [image, links, empty] };
        _editor.Subscribe(note);

        var result = _editor.MergeEmptyTextBlockBackward(note, empty, 30);

        var replacement = Assert.Single(note.Blocks.OfType<TextNoteBlock>());
        Assert.Same(replacement, result!.FocusTarget);
        Assert.Empty(replacement.Text);
        Assert.Equal([image, links, replacement], note.Blocks);
    }

    [Fact]
    public void NormalizeActiveTextBlocks_NormalizesWithoutMutationOrUndo()
    {
        var note = new NoteDocument
        {
            Id = Guid.NewGuid(),
            Blocks = [new TextNoteBlock { Text = "one" }, new TextNoteBlock { Text = "two" }]
        };
        _editor.Subscribe(note);
        var mutations = 0;
        _editor.NoteMutated += _ => mutations++;

        var changed = _editor.NormalizeActiveTextBlocks(note);

        Assert.True(changed);
        Assert.Equal("onetwo", Assert.IsType<TextNoteBlock>(Assert.Single(note.Blocks)).Text);
        Assert.False(_editor.HasUndoHistory(note));
        Assert.Equal(0, mutations);
    }
}
