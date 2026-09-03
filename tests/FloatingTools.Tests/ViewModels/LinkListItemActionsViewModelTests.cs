using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.ViewModels;

public sealed class LinkListItemActionsViewModelTests
{
    [Fact]
    public async Task OpenCommand_UsesRawUrlAndExistingLauncher()
    {
        var launcher = new RecordingLauncher();
        var fixture = await CreateFixtureAsync(launcher: launcher);
        var item = fixture.Block.Items[0];
        item.DisplayName = "Friendly link title";

        await fixture.Actions.OpenLinkItemCommand.ExecuteAsync(item);

        Assert.Equal([item.Url], launcher.Opened);
        Assert.Equal(1, fixture.CloseCount);
    }

    [Fact]
    public async Task OpenCommand_InvalidUrlDoesNotInvokeLauncher()
    {
        var launcher = new RecordingLauncher();
        var fixture = await CreateFixtureAsync(launcher: launcher);

        await fixture.Actions.OpenLinkItemCommand.ExecuteAsync(
            new NoteLinkItem { Url = "file:///c:/secret" });

        Assert.Empty(launcher.Opened);
        Assert.Equal(1, fixture.CloseCount);
    }

    [Fact]
    public async Task CopyCommand_CopiesRawUrlRatherThanDisplayName()
    {
        var clipboard = new RecordingClipboard();
        var fixture = await CreateFixtureAsync(clipboard: clipboard);
        var item = fixture.Block.Items[0];
        item.DisplayName = "Visible caption";

        fixture.Actions.CopyLinkItemCommand.Execute(item);

        Assert.Equal(item.Url, clipboard.Text);
        Assert.Equal(1, fixture.CloseCount);
    }

    [Fact]
    public async Task EditCommand_TargetsOnlyTheRequestedItem()
    {
        var fixture = await CreateFixtureAsync();
        var first = fixture.Block.Items[0];
        var second = fixture.Block.Items[1];

        fixture.Actions.EditLinkItemCommand.Execute(second);

        Assert.Same(second, fixture.EditRequested);
        Assert.NotSame(first, fixture.EditRequested);
    }

    [Fact]
    public async Task SaveEditCommand_UsesExistingValidationAndMutatesOnlyExactItem()
    {
        var fixture = await CreateFixtureAsync();
        var first = fixture.Block.Items[0];
        var second = fixture.Block.Items[1];

        await fixture.Actions.SaveEditedLinkItemCommand.ExecuteAsync(
            new LinkListItemEditRequest(
                fixture.Block,
                second,
                "Second documentation",
                "https://renamed.test/path"));

        Assert.True(fixture.Actions.LastEditSucceeded);
        Assert.Equal("https://first.test", first.Url);
        Assert.Equal("Second documentation", second.DisplayName);
        Assert.Equal("https://renamed.test/path", second.Url);
    }

    [Fact]
    public async Task DeleteCommand_DeletesExactItemThenPreservesUndoAndPersistence()
    {
        var fixture = await CreateFixtureAsync();
        var first = fixture.Block.Items[0];
        var second = fixture.Block.Items[1];
        var savesBeforeDelete = fixture.Store.SaveCount;

        await fixture.Actions.DeleteLinkItemCommand.ExecuteAsync(first);

        Assert.DoesNotContain(first, fixture.Block.Items);
        Assert.Contains(second, fixture.Block.Items);
        Assert.Equal(1, fixture.CloseCount);
        Assert.Equal(1, fixture.FocusCount);
        Assert.True(fixture.Store.SaveCount > savesBeforeDelete);
        Assert.Equal([second.Url], fixture.Store.LastSavedLinks);

        await fixture.ViewModel.UndoDocumentOperationAsync();

        var restoredBlock = Assert.Single(fixture.ViewModel.ActiveBlocks.OfType<LinkListNoteBlock>());
        Assert.Equal([first.Url, second.Url], restoredBlock.Items.Select(item => item.Url));
    }

    private static async Task<Fixture> CreateFixtureAsync(
        RecordingClipboard? clipboard = null,
        RecordingLauncher? launcher = null)
    {
        var store = new RecordingStore();
        var viewModel = new NotesToolViewModel(
            store,
            TimeSpan.Zero,
            clipboardService: clipboard,
            linkLauncher: launcher);
        await viewModel.InitializeAsync();
        var block = Assert.IsType<LinkListNoteBlock>(
            await viewModel.InsertLinkListBlockBeforeAsync(null));
        viewModel.CommitLinkTokens(block, "https://first.test https://second.test");

        NoteLinkItem? editRequested = null;
        var closeCount = 0;
        var focusCount = 0;
        var actions = new LinkListItemActionsViewModel(
            () => viewModel,
            () => block,
            item => editRequested = item,
            () => closeCount++,
            () => focusCount++);

        return new Fixture(
            viewModel,
            block,
            actions,
            store,
            () => editRequested,
            () => closeCount,
            () => focusCount);
    }

    private sealed record Fixture(
        NotesToolViewModel ViewModel,
        LinkListNoteBlock Block,
        LinkListItemActionsViewModel Actions,
        RecordingStore Store,
        Func<NoteLinkItem?> GetEditRequested,
        Func<int> GetCloseCount,
        Func<int> GetFocusCount)
    {
        public NoteLinkItem? EditRequested => GetEditRequested();

        public int CloseCount => GetCloseCount();

        public int FocusCount => GetFocusCount();
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
            if (!LinkUrlValidator.TryValidate(url, out var valid))
            {
                return Task.FromResult(false);
            }

            Opened.Add(valid);
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingStore : INotesStore
    {
        public int SaveCount { get; private set; }

        public IReadOnlyList<string> LastSavedLinks { get; private set; } = [];

        public Task<NotesStorageState> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new NotesStorageState());

        public Task SaveAsync(NotesStorageState state, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            LastSavedLinks = state.Notes
                .SelectMany(note => note.Blocks.OfType<LinkListNoteBlock>())
                .SelectMany(block => block.Items)
                .Select(item => item.Url)
                .ToArray();
            return Task.CompletedTask;
        }
    }
}
