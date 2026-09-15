using System.Collections.ObjectModel;
using FloatingTools.App.Controls;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.ViewModels;

public sealed class RichNotesViewModelTests
{
    [Fact]
    public async Task ImageInsertedInsideText_SplitsTextAndPersistsStructuredBlocks()
    {
        var (viewModel, store, _) = await CreateAsync();
        viewModel.Content = "before after";
        var target = Assert.IsType<TextNoteBlock>(Assert.Single(viewModel.ActiveBlocks));

        var next = await viewModel.InsertClipboardImageAsync(
            target, 7, 0, [1, 2, 3], 300);

        Assert.Equal(3, viewModel.ActiveBlocks.Count);
        Assert.Equal("before ", Assert.IsType<TextNoteBlock>(viewModel.ActiveBlocks[0]).Text);
        var image = Assert.IsType<ImageNoteBlock>(viewModel.ActiveBlocks[1]);
        Assert.Equal("after", Assert.IsType<TextNoteBlock>(viewModel.ActiveBlocks[2]).Text);
        Assert.Same(next, viewModel.ActiveBlocks[2]);
        Assert.Equal(300, image.DisplayWidth);
        Assert.Collection(Assert.Single(store.State.Notes).Blocks,
            block => Assert.IsType<TextNoteBlock>(block),
            block => Assert.IsType<ImageNoteBlock>(block),
            block => Assert.IsType<TextNoteBlock>(block));
    }

    [Fact]
    public async Task ClipboardImageAtLineBoundaryConsumesNewlineAndPersistsBoundaryFlag()
    {
        var (viewModel, store, _) = await CreateAsync();
        viewModel.Content = "Line 1\r\nLine 2";
        var target = Assert.IsType<TextNoteBlock>(Assert.Single(viewModel.ActiveBlocks));

        var next = await viewModel.InsertClipboardImageAsync(
            target,
            "Line 1".Length,
            0,
            [1, 2, 3],
            300);

        Assert.Equal("Line 1", target.Text);
        Assert.NotNull(next);
        Assert.Equal("Line 2", next.Text);
        Assert.True(next.PreserveBoundaryBefore);
        var savedBlocks = Assert.Single(store.State.Notes).Blocks;
        Assert.Equal("Line 1", Assert.IsType<TextNoteBlock>(savedBlocks[0]).Text);
        var savedLower = Assert.IsType<TextNoteBlock>(savedBlocks[2]);
        Assert.Equal("Line 2", savedLower.Text);
        Assert.True(savedLower.PreserveBoundaryBefore);
    }

    [Fact]
    public async Task ImageAtEnd_KeepsExistingEditorWithoutAddingTrailingText()
    {
        var (viewModel, _, _) = await CreateAsync();
        viewModel.Content = "text";
        var target = Assert.IsType<TextNoteBlock>(Assert.Single(viewModel.ActiveBlocks));

        var next = await viewModel.InsertClipboardImageAsync(
            target, target.Text.Length, 0, [1], 400);

        Assert.Same(target, next);
        Assert.Equal("text", target.Text);
        Assert.Collection(viewModel.ActiveBlocks,
            block => Assert.Same(target, block),
            block => Assert.IsType<ImageNoteBlock>(block));
    }

    [Fact]
    public async Task OversizedImage_IsFittedWithoutUpscalingSmallImages()
    {
        var largeStore = new FakeImageStore(800, 400);
        var (largeVm, _, _) = await CreateAsync(largeStore);
        await largeVm.InsertClipboardImageAsync(
            Assert.IsType<TextNoteBlock>(largeVm.ActiveBlocks[0]), 0, 0, [1], 280);
        Assert.Equal(280, largeVm.ActiveBlocks.OfType<ImageNoteBlock>().Single().DisplayWidth);

        var smallStore = new FakeImageStore(120, 60);
        var (smallVm, _, _) = await CreateAsync(smallStore);
        await smallVm.InsertClipboardImageAsync(
            Assert.IsType<TextNoteBlock>(smallVm.ActiveBlocks[0]), 0, 0, [1], 280);
        Assert.Equal(120, smallVm.ActiveBlocks.OfType<ImageNoteBlock>().Single().DisplayWidth);
    }

    [Fact]
    public async Task Resize_ClampsWidthPreservesAspectAndPersists()
    {
        var (viewModel, store, _) = await CreateAsync(new FakeImageStore(400, 200));
        await viewModel.InsertClipboardImageAsync(
            Assert.IsType<TextNoteBlock>(viewModel.ActiveBlocks[0]), 0, 0, [1], 350);
        var image = viewModel.ActiveBlocks.OfType<ImageNoteBlock>().Single();

        viewModel.ResizeImage(image, 900, 320);
        await viewModel.SaveNowAsync();

        Assert.Equal(320, image.DisplayWidth);
        Assert.Equal(2, image.AspectRatio);
        Assert.Equal(320, Assert.Single(store.State.Notes)
            .Blocks.OfType<ImageNoteBlock>().Single().DisplayWidth);
    }

    [Fact]
    public async Task ResizeCommit_PersistsOnceAndCanBeUndone()
    {
        var (viewModel, store, _) = await CreateAsync(new FakeImageStore(400, 200));
        await viewModel.InsertClipboardImageAsync(
            Assert.IsType<TextNoteBlock>(viewModel.ActiveBlocks[0]), 0, 0, [1], 350);
        var image = viewModel.ActiveBlocks.OfType<ImageNoteBlock>().Single();
        var writesBeforeDrag = store.SaveCount;

        await viewModel.CommitImageResizeAsync(image, 290, 350);

        Assert.Equal(writesBeforeDrag + 1, store.SaveCount);
        Assert.Equal(290, image.DisplayWidth);
        Assert.Equal(2, image.AspectRatio);

        await viewModel.UndoDocumentOperationAsync();

        Assert.Equal(350,
            viewModel.ActiveBlocks.OfType<ImageNoteBlock>().Single().DisplayWidth);
    }

    [Fact]
    public async Task ResizePreview_ChangesLayoutSizeWithoutChangingModelOrPersistence()
    {
        var (viewModel, store, _) = await CreateAsync(new FakeImageStore(400, 200));
        await viewModel.InsertClipboardImageAsync(
            Assert.IsType<TextNoteBlock>(viewModel.ActiveBlocks[0]), 0, 0, [1], 350);
        var image = viewModel.ActiveBlocks.OfType<ImageNoteBlock>().Single();
        var committedWidth = image.DisplayWidth;
        var savesBeforePreview = store.SaveCount;

        image.SetResizePreview(280);

        Assert.Equal(committedWidth, image.DisplayWidth);
        Assert.Equal(280, image.LayoutWidth);
        Assert.Equal(140, image.LayoutHeight);
        Assert.Equal(280, image.PreviewWidth);
        Assert.Equal(140, image.PreviewHeight);
        Assert.Equal(savesBeforePreview, store.SaveCount);

        image.SetResizePreview(null);
        Assert.Equal(committedWidth, image.LayoutWidth);
        Assert.Equal(committedWidth / image.AspectRatio, image.LayoutHeight);
    }

    [Fact]
    public async Task ResponsivePanelClamp_DoesNotChangeSavedWidthOrPersist()
    {
        var (viewModel, store, _) = await CreateAsync(new FakeImageStore(600, 400));
        await viewModel.InsertClipboardImageAsync(
            Assert.IsType<TextNoteBlock>(viewModel.ActiveBlocks[0]), 0, 0, [1], 600);
        var image = viewModel.ActiveBlocks.OfType<ImageNoteBlock>().Single();
        var savesBeforePanelChange = store.SaveCount;

        var standard = ResponsiveImageSizeCalculator.Calculate(
            image.DisplayWidth, 332, image.AspectRatio);
        var large = ResponsiveImageSizeCalculator.Calculate(
            image.DisplayWidth, 700, image.AspectRatio);

        Assert.Equal(600, image.DisplayWidth);
        Assert.Equal(300, standard.Width);
        Assert.Equal(200, standard.Height);
        Assert.Equal(600, large.Width);
        Assert.Equal(400, large.Height);
        Assert.Equal(savesBeforePanelChange, store.SaveCount);
    }

    [Fact]
    public async Task ImageOnlyNote_IsPersisted_AndAutomaticTitleIgnoresImage()
    {
        var (viewModel, store, _) = await CreateAsync();

        await viewModel.InsertClipboardImageAsync(
            Assert.IsType<TextNoteBlock>(viewModel.ActiveBlocks[0]), 0, 0, [1], 300);

        Assert.Equal("Untitled note", viewModel.ActiveTitle);
        Assert.True(NotesToolViewModel.ShouldPersist(viewModel.ActiveNote!));
        Assert.Single(store.State.Notes);
    }

    [Fact]
    public async Task PageFocusRequest_AddsOneFinalTextBlockWhenDocumentHasOnlyImage()
    {
        var (viewModel, _, _) = await CreateAsync();
        viewModel.ActiveNote!.Blocks.Clear();
        viewModel.ActiveNote.Blocks.Add(new ImageNoteBlock
        {
            AssetFileName = "image.png",
            NaturalWidth = 100,
            NaturalHeight = 50,
            DisplayWidth = 100
        });

        var text = viewModel.EnsureFinalEditableTextBlock();
        var repeated = viewModel.EnsureFinalEditableTextBlock();

        Assert.Same(text, repeated);
        Assert.Collection(viewModel.ActiveBlocks,
            block => Assert.IsType<ImageNoteBlock>(block),
            block => Assert.Same(text, Assert.IsType<TextNoteBlock>(block)));

        var explicitText = await viewModel.InsertTextBlockBeforeAsync(viewModel.ActiveBlocks[0]);
        Assert.Same(explicitText, Assert.Single(viewModel.ActiveBlocks.OfType<TextNoteBlock>()));
    }

    [Fact]
    public async Task ImageOnlyTemporary_CanBeKept()
    {
        var (viewModel, store, _) = await CreateAsync();
        await viewModel.NewTemporaryNoteCommand.ExecuteAsync(null);
        await viewModel.InsertClipboardImageAsync(
            Assert.IsType<TextNoteBlock>(viewModel.ActiveBlocks[0]), 0, 0, [1], 300);

        await viewModel.KeepTemporaryNoteCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsTemporary);
        Assert.Single(store.State.Notes);
        Assert.Single(Assert.Single(store.State.Notes).Blocks.OfType<ImageNoteBlock>());
    }

    [Fact]
    public async Task DiscardedTemporary_DeletesItsManagedImage()
    {
        var imageStore = new FakeImageStore();
        var (viewModel, _, _) = await CreateAsync(imageStore);
        viewModel.Content = "saved";
        await viewModel.SaveNowAsync();
        var saved = viewModel.ActiveNote!;
        await viewModel.NewTemporaryNoteCommand.ExecuteAsync(null);
        await viewModel.InsertClipboardImageAsync(
            Assert.IsType<TextNoteBlock>(viewModel.ActiveBlocks[0]), 0, 0, [1], 300);
        var asset = viewModel.ActiveBlocks.OfType<ImageNoteBlock>().Single().AssetFileName;

        await viewModel.SelectNoteCommand.ExecuteAsync(saved);

        Assert.Contains(asset, imageStore.Deleted);
    }

    [Fact]
    public async Task DeleteSelectedImage_IsUndoableAndDefersAssetDeletion()
    {
        var imageStore = new FakeImageStore();
        var (viewModel, _, _) = await CreateAsync(imageStore);
        viewModel.Content = "before after";
        await viewModel.InsertClipboardImageAsync(
            Assert.IsType<TextNoteBlock>(viewModel.ActiveBlocks[0]), 7, 0, [1], 300);
        var image = viewModel.ActiveBlocks.OfType<ImageNoteBlock>().Single();
        viewModel.SelectImage(image);

        await viewModel.DeleteSelectedImageAsync();

        Assert.DoesNotContain(viewModel.ActiveBlocks, block => block is ImageNoteBlock);
        Assert.Null(viewModel.SelectedImageBlock);
        Assert.Equal("before after",
            Assert.IsType<TextNoteBlock>(Assert.Single(viewModel.ActiveBlocks)).Text);
        Assert.DoesNotContain(image.AssetFileName, imageStore.Deleted);

        await viewModel.UndoDocumentOperationAsync();

        Assert.Collection(viewModel.ActiveBlocks,
            block => Assert.Equal("before ", Assert.IsType<TextNoteBlock>(block).Text),
            block => Assert.Equal(image.AssetFileName,
                Assert.IsType<ImageNoteBlock>(block).AssetFileName),
            block => Assert.Equal("after", Assert.IsType<TextNoteBlock>(block).Text));
        Assert.Null(viewModel.SelectedImageBlock);
        Assert.DoesNotContain(image.AssetFileName, imageStore.Deleted);
    }

    [Fact]
    public async Task ImageSelection_UsesTheViewModelFacadeAndDoesNotLeakAcrossNotes()
    {
        var firstImage = new ImageNoteBlock { AssetFileName = "first.png" };
        var secondImage = new ImageNoteBlock { AssetFileName = "second.png" };
        var first = NoteWithBlocks(firstImage);
        var second = NoteWithBlocks(secondImage);
        var store = new RecordingNotesStore(new NotesStorageState
        {
            Notes = [first, second],
            LastOpenedNoteId = first.Id
        });
        var viewModel = new NotesToolViewModel(store, imageStore: new FakeImageStore());
        await viewModel.InitializeAsync();

        viewModel.SelectedImageBlock = firstImage;
        Assert.Same(firstImage, viewModel.SelectedImageBlock);
        Assert.True(firstImage.IsSelected);

        await viewModel.SelectNoteCommand.ExecuteAsync(second);

        Assert.Null(viewModel.SelectedImageBlock);
        Assert.False(firstImage.IsSelected);
        Assert.False(secondImage.IsSelected);

        viewModel.SelectImage(secondImage);
        Assert.Same(secondImage, viewModel.SelectedImageBlock);
        Assert.True(secondImage.IsSelected);

        viewModel.SelectedImageBlock = null;
        Assert.Null(viewModel.SelectedImageBlock);
        Assert.False(secondImage.IsSelected);
    }

    [Fact]
    public async Task ImagePasteUndo_RestoresOriginalTextAndCleansAsset()
    {
        var images = new FakeImageStore();
        var (viewModel, _, _) = await CreateAsync(images);
        viewModel.Content = "before after";
        var target = Assert.IsType<TextNoteBlock>(viewModel.ActiveBlocks[0]);
        await viewModel.InsertClipboardImageAsync(target, 7, 0, [1], 300);
        var asset = viewModel.ActiveBlocks.OfType<ImageNoteBlock>().Single().AssetFileName;

        await viewModel.UndoDocumentOperationAsync();

        Assert.Equal("before after",
            Assert.IsType<TextNoteBlock>(Assert.Single(viewModel.ActiveBlocks)).Text);
        Assert.Contains(asset, images.Deleted);
    }

    [Fact]
    public async Task ClipboardImageInsertedAfterImage_CreatesIndependentManagedBlockAndIsUndoable()
    {
        var images = new FakeImageStore();
        var (viewModel, _, _) = await CreateAsync(images);
        await viewModel.InsertClipboardImageAsync(
            Assert.IsType<TextNoteBlock>(viewModel.ActiveBlocks[0]), 0, 0, [1], 300);
        var original = viewModel.ActiveBlocks.OfType<ImageNoteBlock>().Single();

        await viewModel.InsertClipboardImageAfterBlockAsync(original, [2], 300);

        var inserted = viewModel.ActiveBlocks.OfType<ImageNoteBlock>().Last();
        Assert.NotEqual(original.AssetFileName, inserted.AssetFileName);
        Assert.Equal(original.AspectRatio, inserted.AspectRatio);

        await viewModel.UndoDocumentOperationAsync();

        Assert.Single(viewModel.ActiveBlocks.OfType<ImageNoteBlock>());
        Assert.Contains(inserted.AssetFileName, images.Deleted);
    }

    [Fact]
    public async Task AdjacentTextBlocks_AreMergedAndMissingTextBlockIsRestored()
    {
        var (viewModel, _, _) = await CreateAsync();
        viewModel.ActiveNote!.Blocks.Clear();
        viewModel.ActiveNote.Blocks.Add(new TextNoteBlock { Text = "before" });
        viewModel.ActiveNote.Blocks.Add(new TextNoteBlock());
        viewModel.ActiveNote.Blocks.Add(new TextNoteBlock { Text = "after" });

        await viewModel.NormalizeActiveTextBlocksAsync();

        Assert.Equal("beforeafter",
            Assert.IsType<TextNoteBlock>(Assert.Single(viewModel.ActiveBlocks)).Text);

        viewModel.ActiveNote.Blocks.Clear();
        await viewModel.NormalizeActiveTextBlocksAsync();
        Assert.Empty(Assert.IsType<TextNoteBlock>(Assert.Single(viewModel.ActiveBlocks)).Text);
    }

    [Fact]
    public async Task ImageBoundary_PreservesTextBlocksOnBothSides()
    {
        var (viewModel, _, _) = await CreateAsync();
        viewModel.Content = "beforeafter";
        await viewModel.InsertClipboardImageAsync(
            Assert.IsType<TextNoteBlock>(viewModel.ActiveBlocks[0]), 6, 0, [1], 300);

        await viewModel.NormalizeActiveTextBlocksAsync();

        Assert.Collection(viewModel.ActiveBlocks,
            block => Assert.Equal("before", Assert.IsType<TextNoteBlock>(block).Text),
            block => Assert.IsType<ImageNoteBlock>(block),
            block => Assert.Equal("after", Assert.IsType<TextNoteBlock>(block).Text));
    }

    [Fact]
    public async Task NewNote_ExplicitTextInsertionConsumesInitialPlaceholderOnlyOnce()
    {
        var (vm, _, _) = await CreateAsync();
        var automatic = Assert.Single(vm.ActiveBlocks.OfType<TextNoteBlock>());
        var first = await vm.InsertTextBlockBeforeAsync(null);
        Assert.NotNull(first);
        Assert.NotSame(automatic, first);
        Assert.Same(first, Assert.Single(vm.ActiveBlocks));

        var second = await vm.InsertTextBlockBeforeAsync(null);
        Assert.NotNull(second);
        await vm.NormalizeActiveTextBlocksAsync();
        Assert.Equal([first, second], vm.ActiveBlocks);

        await vm.UndoDocumentOperationAsync();
        Assert.Equal(first!.Id, Assert.Single(vm.ActiveBlocks).Id);
        await vm.UndoDocumentOperationAsync();
        Assert.Equal(automatic.Id, Assert.Single(vm.ActiveBlocks).Id);
        var repeated = await vm.InsertTextBlockBeforeAsync(null);
        Assert.Same(repeated, Assert.Single(vm.ActiveBlocks));
    }

    [Fact]
    public async Task ImageFileAtEnd_DoesNotCreateExtraTextAndUndoRestoresDocument()
    {
        var (vm, _, _) = await CreateAsync();
        vm.Content = "keep this text";
        var text = Assert.Single(vm.ActiveBlocks.OfType<TextNoteBlock>());
        var focus = await vm.InsertImageFileAsync(text, text.Text.Length, "source.png", 300);
        Assert.Same(text, focus);
        Assert.Collection(vm.ActiveBlocks,
            block => Assert.Same(text, block),
            block => Assert.IsType<ImageNoteBlock>(block));
        await vm.UndoDocumentOperationAsync();
        Assert.Equal("keep this text", Assert.IsType<TextNoteBlock>(Assert.Single(vm.ActiveBlocks)).Text);
    }

    [Fact]
    public async Task BackspaceMerge_RemovesEmptyBlockAndReturnsPreviousCaretTarget()
    {
        var (viewModel, _, _) = await CreateAsync();
        var previous = Assert.IsType<TextNoteBlock>(viewModel.ActiveBlocks[0]);
        previous.Text = "text";
        var empty = new TextNoteBlock();
        viewModel.ActiveNote!.Blocks.Add(empty);

        var focusTarget = await viewModel.MergeEmptyTextBlockBackwardAsync(empty);

        Assert.Same(previous, focusTarget);
        Assert.Same(previous, Assert.Single(viewModel.ActiveBlocks));
    }

    [Fact]
    public async Task Backspace_RemovesEmptyTextAfterImageWhenAnotherEditorRemains()
    {
        var (viewModel, _, _) = await CreateAsync();
        var first = Assert.IsType<TextNoteBlock>(viewModel.ActiveBlocks[0]);
        first.Text = "editable";
        var image = new ImageNoteBlock { AssetFileName = "image.png" };
        var empty = new TextNoteBlock { PreserveBoundaryBefore = true };
        viewModel.ActiveNote!.Blocks.Add(image);
        viewModel.ActiveNote.Blocks.Add(empty);

        var focus = await viewModel.MergeEmptyTextBlockBackwardAsync(empty);

        Assert.Same(first, focus);
        Assert.DoesNotContain(empty, viewModel.ActiveBlocks);
        Assert.Single(viewModel.ActiveBlocks.OfType<TextNoteBlock>());

        Assert.Null(await viewModel.MergeEmptyTextBlockBackwardAsync(first));
        Assert.Single(viewModel.ActiveBlocks.OfType<TextNoteBlock>());
    }

    [Fact]
    public async Task DraggedImageInsertion_CanBeUndoneWithBlockOrderRestored()
    {
        var (viewModel, _, _) = await CreateAsync();
        viewModel.Content = "one two";
        var target = Assert.IsType<TextNoteBlock>(viewModel.ActiveBlocks[0]);
        await viewModel.InsertImageFileAsync(target, 4, "source.png", 300);

        await viewModel.UndoDocumentOperationAsync();

        Assert.Equal("one two",
            Assert.IsType<TextNoteBlock>(Assert.Single(viewModel.ActiveBlocks)).Text);
    }

    [Fact]
    public async Task DeleteImage_DoesNotDeleteAssetStillReferencedByAnotherNote()
    {
        const string sharedAsset = "shared.png";
        var first = NoteWithBlocks(new ImageNoteBlock
        {
            AssetFileName = sharedAsset, NaturalWidth = 100, NaturalHeight = 50, DisplayWidth = 100
        });
        var second = NoteWithBlocks(new ImageNoteBlock
        {
            AssetFileName = sharedAsset, NaturalWidth = 100, NaturalHeight = 50, DisplayWidth = 100
        });
        var store = new RecordingNotesStore(new NotesStorageState
        {
            Notes = [first, second], LastOpenedNoteId = first.Id
        });
        var images = new FakeImageStore();
        var viewModel = new NotesToolViewModel(store, imageStore: images);
        await viewModel.InitializeAsync();
        viewModel.SelectImage(viewModel.ActiveBlocks.OfType<ImageNoteBlock>().Single());

        await viewModel.DeleteSelectedImageAsync();

        Assert.DoesNotContain(sharedAsset, images.Deleted);
    }

    [Fact]
    public async Task DeleteNote_CleansItsUnreferencedAssets()
    {
        var images = new FakeImageStore();
        var (viewModel, _, _) = await CreateAsync(images);
        await viewModel.InsertClipboardImageAsync(
            Assert.IsType<TextNoteBlock>(viewModel.ActiveBlocks[0]), 0, 0, [1], 300);
        var asset = viewModel.ActiveBlocks.OfType<ImageNoteBlock>().Single().AssetFileName;

        await viewModel.DeleteNoteCommand.ExecuteAsync(viewModel.ActiveNote);

        Assert.Contains(asset, images.Deleted);
    }

    [Fact]
    public async Task AutomaticTitle_UsesTextBlocksOnly()
    {
        var (viewModel, _, _) = await CreateAsync();
        viewModel.Content = "First useful words for title";
        var target = Assert.IsType<TextNoteBlock>(viewModel.ActiveBlocks[0]);
        await viewModel.InsertClipboardImageAsync(target, 5, 0, [1], 300);

        Assert.Equal("First useful words", viewModel.ActiveTitle);
    }

    [Fact]
    public async Task TextBlockCopyCutDeleteAndUndo_PreserveEditableDocument()
    {
        var clipboard = new RecordingClipboard();
        var (viewModel, _, _) = await CreateAsync(clipboardService: clipboard);
        var block = Assert.IsType<TextNoteBlock>(viewModel.ActiveBlocks[0]);
        block.Text = "whole block";

        viewModel.CopyTextBlock(block);
        Assert.Equal("whole block", clipboard.Text);

        var replacement = await viewModel.CutTextBlockAsync(block);
        Assert.Equal("whole block", clipboard.Text);
        Assert.Same(replacement, Assert.Single(viewModel.ActiveBlocks.OfType<TextNoteBlock>()));
        Assert.Empty(replacement!.Text);

        await viewModel.UndoDocumentOperationAsync();
        var restored = Assert.Single(viewModel.ActiveBlocks.OfType<TextNoteBlock>());
        Assert.Equal("whole block", restored.Text);

        await viewModel.DeleteTextBlockAsync(restored);
        Assert.Empty(Assert.Single(viewModel.ActiveBlocks.OfType<TextNoteBlock>()).Text);
        await viewModel.UndoDocumentOperationAsync();
        Assert.Equal("whole block",
            Assert.Single(viewModel.ActiveBlocks.OfType<TextNoteBlock>()).Text);
    }

    [Fact]
    public async Task ExplicitTextInsertion_UsesRequestedIndexAndIsUndoable()
    {
        var (viewModel, _, _) = await CreateAsync();
        var first = Assert.IsType<TextNoteBlock>(viewModel.ActiveBlocks[0]);
        first.Text = "first";
        var tail = await viewModel.InsertTextBlockBeforeAsync(null);

        Assert.Same(tail, viewModel.ActiveBlocks[1]);
        Assert.True(tail!.PreserveBoundaryBefore);

        await viewModel.UndoDocumentOperationAsync();
        Assert.Single(viewModel.ActiveBlocks);
        Assert.Equal("first", Assert.IsType<TextNoteBlock>(viewModel.ActiveBlocks[0]).Text);
    }

    [Fact]
    public async Task DocumentInsertionZones_InsertBeforeFirstIntermediateAndAtEnd()
    {
        var note = NoteWithBlocks(
            new TextNoteBlock { Text = "first" },
            new ImageNoteBlock
            {
                AssetFileName = "image.png",
                NaturalWidth = 320,
                NaturalHeight = 180
            },
            new TextNoteBlock { Text = "last" });
        var store = new RecordingNotesStore(new NotesStorageState
        {
            Notes = [note],
            LastOpenedNoteId = note.Id
        });
        var viewModel = new NotesToolViewModel(
            store,
            TimeSpan.FromMilliseconds(25),
            imageStore: new FakeImageStore());
        await viewModel.InitializeAsync();

        var firstAnchor = viewModel.ActiveBlocks[0];
        var beforeFirst = await viewModel.InsertTextBlockBeforeAsync(firstAnchor);
        Assert.Same(beforeFirst, viewModel.ActiveBlocks[0]);

        var imageAnchor = Assert.Single(viewModel.ActiveBlocks.OfType<ImageNoteBlock>());
        var beforeImage = await viewModel.InsertTextBlockBeforeAsync(imageAnchor);
        var imageIndex = viewModel.ActiveBlocks.IndexOf(imageAnchor);
        Assert.Same(beforeImage, viewModel.ActiveBlocks[imageIndex - 1]);

        var atEnd = await viewModel.InsertTextBlockBeforeAsync(null);
        Assert.Same(atEnd, viewModel.ActiveBlocks[^1]);
    }

    [Fact]
    public async Task ExportCommands_AreEnabledAndInvokeConfiguredService()
    {
        var exporter = new RecordingNoteExportService();
        var (viewModel, _, _) = await CreateAsync(exportService: exporter);
        var note = Assert.IsType<NoteDocument>(viewModel.ActiveNote);

        Assert.True(viewModel.ExportNotePdfCommand.CanExecute(note));
        Assert.True(viewModel.ExportNoteWordCommand.CanExecute(note));

        await viewModel.ExportNotePdfCommand.ExecuteAsync(note);
        await viewModel.ExportNoteWordCommand.ExecuteAsync(note);

        Assert.Equal(
            [(note.Id, NoteExportFormat.Pdf), (note.Id, NoteExportFormat.Word)],
            exporter.Calls);
    }

    [Fact]
    public async Task PreviousVersionJson_WithLegacyLinkNullLinksAndMalformedRanges_OpensSafely()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"FloatingTools-notes-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "notes.json");
        Directory.CreateDirectory(directory);
        var noteId = Guid.NewGuid();
        var firstId = Guid.NewGuid();
        var legacyId = Guid.NewGuid();
        var malformedId = Guid.NewGuid();
        var badLinkId = Guid.NewGuid();
        var json = $$"""
        {
          "notes": [
            {
              "id": "{{noteId}}",
              "title": "Legacy fixture",
              "createdAt": "2026-01-01T00:00:00+00:00",
              "updatedAt": "2026-01-01T00:00:00+00:00",
              "blocks": [
                { "$type": "text", "id": "{{firstId}}", "text": "before", "links": null },
                { "$type": "link", "id": "{{legacyId}}", "url": "https://example.com/docs", "displayText": "Docs" },
                { "$type": "text", "id": "{{malformedId}}", "text": "after", "links": [
                  { "id": "{{badLinkId}}", "start": -1, "length": 99, "url": "https://invalid-range.example" }
                ] }
              ]
            }
          ],
          "lastOpenedNoteId": "{{noteId}}"
        }
        """;

        try
        {
            await File.WriteAllTextAsync(path, json);
            var viewModel = new NotesToolViewModel(
                new JsonNotesStore(path),
                imageStore: new FakeImageStore());

            await viewModel.InitializeAsync();

            Assert.Collection(viewModel.ActiveBlocks,
                block => Assert.Equal("before", Assert.IsType<TextNoteBlock>(block).Text),
                block => Assert.Equal("Docs", Assert.IsType<TextNoteBlock>(block).Text),
                block => Assert.Equal("after", Assert.IsType<TextNoteBlock>(block).Text));
            Assert.All(viewModel.ActiveBlocks, block =>
                Assert.True(block is TextNoteBlock or ImageNoteBlock));
            var saved = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain("\"$type\": \"link\"", saved);
            Assert.DoesNotContain("\"links\"", saved);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonTextOnlyNotes_AcquireEditorAndKeepItThroughDeletionAndJsonReload(bool image)
    {
        var path = Path.Combine(Path.GetTempPath(), $"FloatingTools-editor-{Guid.NewGuid():N}.json");
        NoteBlock CreateNonTextBlock() => image
            ? new ImageNoteBlock { AssetFileName = "existing.png" }
            : new LinkListNoteBlock { Items = [new NoteLinkItem { Url = "https://example.test" }] };
        var active = NoteWithBlocks(CreateNonTextBlock());
        var inactive = NoteWithBlocks(CreateNonTextBlock());
        active.Title = "Stored title";
        var store = new JsonNotesStore(path);
        try
        {
            await store.SaveAsync(new NotesStorageState { Notes = [active, inactive], LastOpenedNoteId = active.Id });
            var vm = new NotesToolViewModel(store, TimeSpan.FromHours(1), imageStore: new FakeImageStore());
            await vm.InitializeAsync();
            Assert.Equal("Stored title", vm.ActiveTitle);
            Assert.All(vm.Notes, note => Assert.Empty(Assert.Single(note.Blocks.OfType<TextNoteBlock>()).Text));
            Assert.All((await store.LoadAsync()).Notes, note => Assert.Single(note.Blocks.OfType<TextNoteBlock>()));

            var original = Assert.Single(vm.ActiveBlocks.OfType<TextNoteBlock>());
            original.Text = "delete me";
            var replacement = await vm.DeleteTextBlockAsync(original);
            Assert.Same(replacement, Assert.Single(vm.ActiveBlocks.OfType<TextNoteBlock>()));
            Assert.Empty(replacement!.Text);
            Assert.Equal(2, vm.ActiveBlocks.Count);

            var reloaded = new NotesToolViewModel(store, imageStore: new FakeImageStore());
            await reloaded.InitializeAsync();
            var reloadedText = Assert.Single(reloaded.ActiveBlocks.OfType<TextNoteBlock>());
            Assert.Equal(replacement.Id, reloadedText.Id);
            Assert.Empty(reloadedText.Text);
            if (image)
            {
                reloaded.SelectImage(Assert.Single(reloaded.ActiveBlocks.OfType<ImageNoteBlock>()));
                await reloaded.DeleteSelectedImageAsync();
                Assert.Null(reloaded.SelectedImageBlock);
            }
            else
            {
                await reloaded.DeleteLinkListBlockAsync(Assert.Single(reloaded.ActiveBlocks.OfType<LinkListNoteBlock>()));
            }
            Assert.Same(reloadedText, Assert.Single(reloaded.ActiveBlocks));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
    }

    private static async Task<(NotesToolViewModel ViewModel, RecordingNotesStore Store, FakeImageStore Images)>
        CreateAsync(
            FakeImageStore? images = null,
            IClipboardService? clipboardService = null,
            INoteExportService? exportService = null)
    {
        images ??= new FakeImageStore();
        var store = new RecordingNotesStore();
        var viewModel = new NotesToolViewModel(
            store,
            TimeSpan.FromMilliseconds(25),
            imageStore: images,
            clipboardService: clipboardService,
            exportService: exportService);
        await viewModel.InitializeAsync();
        return (viewModel, store, images);
    }

    private static NoteDocument NoteWithBlocks(params NoteBlock[] blocks) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Untitled note",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        Blocks = new ObservableCollection<NoteBlock>(blocks)
    };

    private sealed class FakeImageStore(double width = 640, double height = 320) : INotesImageStore
    {
        private int _next;
        public List<string> Deleted { get; } = [];

        public Task<ManagedNoteImage> ImportPngAsync(byte[] pngBytes, CancellationToken cancellationToken = default) =>
            Task.FromResult(Create());

        public Task<ManagedNoteImage> ImportFileAsync(string sourcePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(Create());

        public string GetAbsolutePath(string assetFileName) => $"C:\\managed\\{assetFileName}";

        public Task DeleteAsync(string assetFileName, CancellationToken cancellationToken = default)
        {
            Deleted.Add(assetFileName);
            return Task.CompletedTask;
        }

        private ManagedNoteImage Create()
        {
            var name = $"image-{Interlocked.Increment(ref _next)}.png";
            return new(name, GetAbsolutePath(name), width, height);
        }
    }

    private sealed class RecordingClipboard : IClipboardService
    {
        public string? Text { get; private set; }
        public void SetText(string text) => Text = text;
    }

    private sealed class RecordingNoteExportService : INoteExportService
    {
        public List<(Guid NoteId, NoteExportFormat Format)> Calls { get; } = [];

        public Task<bool> ExportAsync(
            NoteDocument note,
            NoteExportFormat format,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((note.Id, format));
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingNotesStore(NotesStorageState? initial = null) : INotesStore
    {
        public int SaveCount { get; private set; }
        public NotesStorageState State { get; private set; } = Clone(initial ?? new());

        public Task<NotesStorageState> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Clone(State));

        public Task SaveAsync(NotesStorageState state, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            State = Clone(state);
            return Task.CompletedTask;
        }

        private static NotesStorageState Clone(NotesStorageState state) => new()
        {
            LastOpenedNoteId = state.LastOpenedNoteId,
            Notes = state.Notes.Select(note => new NoteDocument
            {
                Id = note.Id,
                Title = note.Title,
                CreatedAt = note.CreatedAt,
                UpdatedAt = note.UpdatedAt,
                IsTemporary = note.IsTemporary,
                IsTitleLocked = note.IsTitleLocked,
                HasManualTitle = note.HasManualTitle,
                Blocks = new ObservableCollection<NoteBlock>(note.Blocks.Select(CloneBlock))
            }).ToList()
        };

        private static NoteBlock CloneBlock(NoteBlock block) => block switch
        {
            TextNoteBlock text => new TextNoteBlock
            {
                Id = text.Id,
                Text = text.Text,
                PreserveBoundaryBefore = text.PreserveBoundaryBefore,
                LegacyLinks = text.LegacyLinks?.Select(link => new NoteHyperlink
                {
                    Id = link.Id,
                    Start = link.Start,
                    Length = link.Length,
                    Url = link.Url
                }).ToList()
            },
            ImageNoteBlock image => new ImageNoteBlock
            {
                Id = image.Id,
                AssetFileName = image.AssetFileName,
                NaturalWidth = image.NaturalWidth,
                NaturalHeight = image.NaturalHeight,
                DisplayWidth = image.DisplayWidth
            },
            LinkNoteBlock link => new LinkNoteBlock
            {
                Id = link.Id,
                Url = link.Url,
                DisplayName = link.DisplayName,
                LegacyDisplayText = link.LegacyDisplayText,
                LegacyLinks = link.LegacyLinks?.Select(item => new LegacyLinkNoteItem
                {
                    Id = item.Id,
                    Url = item.Url,
                    DisplayName = item.DisplayName
                }).ToList()
            },
            _ => throw new NotSupportedException()
        };
    }
}
