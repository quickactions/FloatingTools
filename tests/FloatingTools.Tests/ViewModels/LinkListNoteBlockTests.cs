using System.Collections.ObjectModel;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.ViewModels;

public sealed class LinkListNoteBlockTests
{
    [Theory]
    [InlineData("https://a.test https://b.test", 2)]
    [InlineData("https://a.test\thttps://b.test", 2)]
    [InlineData("https://a.test\r\nhttps://b.test", 2)]
    [InlineData("  https://a.test   \r\n\t https://b.test  ", 2)]
    [InlineData("   \r\n\t  ", 0)]
    public void Parser_UsesAllWhitespaceWithoutEmptyTokens(string input, int count) =>
        Assert.Equal(count, LinkTokenParser.Parse(input).Count);

    [Theory]
    [InlineData("http://example.com", true)]
    [InlineData("https://example.com", true)]
    [InlineData("file:///c:/secret", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("cmd:calc", false)]
    [InlineData("not-a-url", false)]
    public void Validator_AcceptsOnlyHttpAndHttps(string input, bool expected) =>
        Assert.Equal(expected, LinkUrlValidator.TryValidate(input, out _));

    [Fact]
    public async Task InsertAndCommit_CreatesOneBlockWithOrderedDefaultItems()
    {
        var (vm, _) = await CreateAsync();
        var textBlockCountBefore = vm.ActiveBlocks.OfType<TextNoteBlock>().Count();
        var block = Assert.IsType<LinkListNoteBlock>(await vm.InsertLinkListBlockBeforeAsync(null));

        var result = vm.CommitLinkTokens(block, "https://one.test https://two.test");

        Assert.Equal(2, result.AcceptedItems.Count);
        Assert.Equal(["https://one.test", "https://two.test"], block.Items.Select(item => item.Url));
        Assert.All(block.Items, item => Assert.Equal(item.Url, item.DisplayName));
        Assert.Equal(2, vm.ActiveBlocks.Count);
        Assert.Equal(textBlockCountBefore, vm.ActiveBlocks.OfType<TextNoteBlock>().Count());
    }

    [Fact]
    public async Task InvalidToken_RemainsDraftAndCannotOpen()
    {
        var launcher = new RecordingLauncher();
        var (vm, _) = await CreateAsync(launcher: launcher);
        var block = Assert.IsType<LinkListNoteBlock>(await vm.InsertLinkListBlockBeforeAsync(null));

        var result = vm.CommitLinkTokens(block, "not-a-url");

        Assert.Empty(block.Items);
        Assert.Equal("not-a-url", result.InvalidDraft);
        Assert.False(await vm.OpenLinkItemAsync(new NoteLinkItem { Url = "file:///c:/x" }));
        Assert.Empty(launcher.Opened);
    }

    [Fact]
    public async Task OpenLinkItem_UsesRawUrlRatherThanDisplayName()
    {
        var launcher = new RecordingLauncher();
        var (vm, _) = await CreateAsync(launcher: launcher);
        var block = Assert.IsType<LinkListNoteBlock>(await vm.InsertLinkListBlockBeforeAsync(null));
        var item = new NoteLinkItem { Url = "https://exact.test/path", DisplayName = "Friendly title" };
        block.Items.Add(item);

        Assert.True(await vm.OpenLinkItemAsync(item));
        Assert.Equal(["https://exact.test/path"], launcher.Opened);
    }

    [Fact]
    public async Task EmptyDraftBackspace_DeletesItemsThenTheEntireLinkListWithoutCreatingText()
    {
        var (vm, _) = await CreateAsync();
        var initialText = Assert.Single(vm.ActiveBlocks.OfType<TextNoteBlock>());
        await vm.DeleteTextBlockAsync(initialText);
        Assert.Empty(vm.ActiveBlocks);

        var block = Assert.IsType<LinkListNoteBlock>(await vm.InsertLinkListBlockBeforeAsync(null));
        Assert.Collection(vm.ActiveBlocks, actual => Assert.Same(block, actual));
        vm.CommitLinkTokens(block, "https://one.test https://two.test");

        Assert.True(await vm.DeleteLastLinkItemOrBlockAsync(block));
        Assert.Equal(["https://one.test"], block.Items.Select(item => item.Url));
        Assert.True(await vm.DeleteLastLinkItemOrBlockAsync(block));
        Assert.Empty(block.Items);
        Assert.Contains(block, vm.ActiveBlocks);
        Assert.True(await vm.DeleteLastLinkItemOrBlockAsync(block));
        Assert.Empty(vm.ActiveBlocks);
    }

    [Fact]
    public async Task CopyEditDeleteAndUndo_OperateOnOneItemOrWholeBlock()
    {
        var clipboard = new RecordingClipboard();
        var (vm, _) = await CreateAsync(clipboard: clipboard);
        var block = Assert.IsType<LinkListNoteBlock>(await vm.InsertLinkListBlockBeforeAsync(null));
        vm.CommitLinkTokens(block, "https://one.test https://two.test");
        var first = block.Items[0];

        vm.CopyLinkItem(first);
        Assert.Equal("https://one.test", clipboard.Text);
        Assert.True(await vm.EditLinkItemAsync(block, first, "שם", "https://renamed.test"));
        Assert.Equal("שם", first.DisplayName);
        Assert.Equal("https://renamed.test", first.Url);
        Assert.True(await vm.EditLinkItemAsync(block, first, "  ", "https://fallback.test"));
        Assert.Equal(first.Url, first.DisplayName);

        await vm.DeleteLinkItemAsync(block, first);
        Assert.Single(block.Items);
        await vm.UndoDocumentOperationAsync();
        block = Assert.Single(vm.ActiveBlocks.OfType<LinkListNoteBlock>());
        Assert.Equal(2, block.Items.Count);

        vm.CopyLinkListBlock(block);
        Assert.Equal(string.Join(Environment.NewLine, block.Items.Select(item => item.Url)), clipboard.Text);
        await vm.DeleteLinkListBlockAsync(block);
        Assert.Empty(vm.ActiveBlocks.OfType<LinkListNoteBlock>());
        await vm.UndoDocumentOperationAsync();
        Assert.Single(vm.ActiveBlocks.OfType<LinkListNoteBlock>());
    }

    [Fact]
    public async Task CutBlock_CopiesRawUrlsThenUsesTheBlockDeleteUndoPath()
    {
        var clipboard = new RecordingClipboard();
        var (vm, _) = await CreateAsync(clipboard: clipboard);
        var block = Assert.IsType<LinkListNoteBlock>(await vm.InsertLinkListBlockBeforeAsync(null));
        vm.CommitLinkTokens(block, "https://one.test https://two.test");

        await vm.CutLinkListBlockAsync(block);

        Assert.Equal("https://one.test" + Environment.NewLine + "https://two.test", clipboard.Text);
        Assert.Empty(vm.ActiveBlocks.OfType<LinkListNoteBlock>());

        await vm.UndoDocumentOperationAsync();
        Assert.Equal(
            ["https://one.test", "https://two.test"],
            Assert.Single(vm.ActiveBlocks.OfType<LinkListNoteBlock>()).Items.Select(item => item.Url));
    }

    [Fact]
    public async Task EmptyBlockIsNotMeaningfulButCommittedItemPersistsWithLinkListDiscriminator()
    {
        var (vm, store) = await CreateAsync();
        var block = Assert.IsType<LinkListNoteBlock>(await vm.InsertLinkListBlockBeforeAsync(null));
        Assert.False(NotesToolViewModel.ShouldPersist(new NoteDocument
        {
            Blocks = new ObservableCollection<NoteBlock> { block }
        }));

        vm.CommitLinkTokens(block, "https://example.com");
        await vm.SaveNowAsync();

        var saved = Assert.Single(store.State.Notes);
        var restored = Assert.Single(saved.Blocks.OfType<LinkListNoteBlock>());
        Assert.Equal("https://example.com", Assert.Single(restored.Items).Url);
        Assert.DoesNotContain(saved.Blocks, item => item is LinkNoteBlock);
    }

    [Fact]
    public async Task PersistencePreservesItemOrderNamesAndAutomaticTitle()
    {
        var (vm, store) = await CreateAsync();
        var block = Assert.IsType<LinkListNoteBlock>(await vm.InsertLinkListBlockBeforeAsync(null));
        vm.CommitLinkTokens(block, "https://first.test https://second.test");
        await vm.EditLinkItemAsync(block, block.Items[0], "First docs", block.Items[0].Url);
        await vm.SaveNowAsync();

        Assert.Equal("First docs", vm.ActiveTitle);
        var items = Assert.Single(store.State.Notes).Blocks.OfType<LinkListNoteBlock>().Single().Items;
        Assert.Equal(["First docs", "https://second.test"], items.Select(item => item.DisplayName));
    }

    private static async Task<(NotesToolViewModel Vm, RecordingStore Store)> CreateAsync(
        RecordingClipboard? clipboard = null,
        INoteLinkLauncher? launcher = null)
    {
        var store = new RecordingStore();
        var vm = new NotesToolViewModel(store, TimeSpan.Zero,
            clipboardService: clipboard,
            linkLauncher: launcher);
        await vm.InitializeAsync();
        return (vm, store);
    }

    private sealed class RecordingClipboard : IClipboardService
    {
        public string? Text { get; private set; }
        public void SetText(string text) => Text = text;
    }

    private sealed class RecordingLauncher : INoteLinkLauncher
    {
        public List<string> Opened { get; } = [];
        public Task<bool> TryOpenAsync(string url, CancellationToken cancellationToken = default)
        {
            if (!LinkUrlValidator.TryValidate(url, out var valid)) return Task.FromResult(false);
            Opened.Add(valid);
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingStore : INotesStore
    {
        public NotesStorageState State { get; private set; } = new();
        public Task<NotesStorageState> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Clone(State));
        public Task SaveAsync(NotesStorageState state, CancellationToken cancellationToken = default)
        {
            State = Clone(state);
            return Task.CompletedTask;
        }
        private static NotesStorageState Clone(NotesStorageState state) => new()
        {
            LastOpenedNoteId = state.LastOpenedNoteId,
            Notes = state.Notes.Select(note => new NoteDocument
            {
                Id = note.Id, Title = note.Title, CreatedAt = note.CreatedAt, UpdatedAt = note.UpdatedAt,
                HasManualTitle = note.HasManualTitle, IsTitleLocked = note.IsTitleLocked,
                Blocks = new ObservableCollection<NoteBlock>(note.Blocks.Select(CloneBlock))
            }).ToList()
        };
        private static NoteBlock CloneBlock(NoteBlock block) => block switch
        {
            TextNoteBlock text => new TextNoteBlock { Id = text.Id, Text = text.Text, PreserveBoundaryBefore = text.PreserveBoundaryBefore },
            ImageNoteBlock image => new ImageNoteBlock { Id = image.Id, AssetFileName = image.AssetFileName, DisplayWidth = image.DisplayWidth },
            LinkListNoteBlock links => new LinkListNoteBlock
            {
                Id = links.Id,
                Items = new ObservableCollection<NoteLinkItem>(links.Items.Select(item => new NoteLinkItem
                { Id = item.Id, Url = item.Url, DisplayName = item.DisplayName }))
            },
            _ => throw new NotSupportedException()
        };
    }
}
