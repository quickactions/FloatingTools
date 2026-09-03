using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.ViewModels;

public sealed class NotesToolViewModelTests
{
    [Fact]
    public async Task EmptyInstallation_CreatesUsableInMemoryUntitledNote()
    {
        var (viewModel, store) = await CreateAsync();

        var note = Assert.Single(viewModel.Notes);
        Assert.Same(note, viewModel.ActiveNote);
        Assert.Equal("Untitled note", note.Title);
        Assert.False(note.IsTemporary);
        Assert.Empty(store.State.Notes);
    }

    [Fact]
    public async Task NewNote_ReplacesUnusedBlankDraftWithoutPersistingIt()
    {
        var (viewModel, _) = await CreateAsync();

        await viewModel.NewNoteCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Notes);
        Assert.False(viewModel.ActiveNote!.IsTemporary);
        Assert.Equal("Untitled note", viewModel.ActiveTitle);
    }

    [Fact]
    public async Task TemporaryNote_IsNotPersisted_AndIsDiscardedWhenSwitching()
    {
        var (viewModel, store) = await CreateAsync();
        viewModel.Content = "saved content";
        await viewModel.SaveNowAsync();
        var saved = viewModel.ActiveNote!;

        await viewModel.NewTemporaryNoteCommand.ExecuteAsync(null);
        viewModel.Content = "discard me";
        await viewModel.SelectNoteCommand.ExecuteAsync(saved);

        Assert.Same(saved, viewModel.ActiveNote);
        Assert.DoesNotContain(viewModel.Notes, note => note.IsTemporary);
        Assert.DoesNotContain(store.State.Notes, note => TextOf(note) == "discard me");
    }

    [Fact]
    public async Task TemporaryNote_CanBeKeptAsSaved()
    {
        var (viewModel, store) = await CreateAsync();
        await viewModel.NewTemporaryNoteCommand.ExecuteAsync(null);
        viewModel.Content = "temporary thought worth keeping";

        await viewModel.KeepTemporaryNoteCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsTemporary);
        Assert.Contains(viewModel.ActiveNote!, viewModel.Notes);
        Assert.Contains(store.State.Notes, note => TextOf(note) == viewModel.Content);
        Assert.Equal("temporary thought worth keeping", viewModel.ActiveTitle);
    }

    [Fact]
    public async Task DebouncedAutoSave_WritesOnceAfterSeveralKeystrokes()
    {
        var (viewModel, store) = await CreateAsync(TimeSpan.FromMilliseconds(35));
        var initialWrites = store.SaveCount;

        viewModel.Content = "a";
        viewModel.Content = "ab";
        viewModel.Content = "abc";
        Assert.Equal(initialWrites, store.SaveCount);

        await Task.Delay(140);
        Assert.Equal(initialWrites + 1, store.SaveCount);
        Assert.Equal("abc", TextOf(Assert.Single(store.State.Notes)));
    }

    [Fact]
    public async Task PrepareForExitAsync_FlushesPendingSavedNoteContent()
    {
        var (viewModel, store) = await CreateAsync(TimeSpan.FromSeconds(30));
        viewModel.Content = "persist during exit";

        await viewModel.PrepareForExitAsync();

        Assert.Equal(
            "persist during exit",
            TextOf(Assert.Single(store.State.Notes)));
    }

    [Fact]
    public async Task SwitchingNotes_ImmediatelySavesCurrentContent()
    {
        var (viewModel, store) = await CreateAsync(TimeSpan.FromSeconds(10));
        var first = viewModel.ActiveNote!;
        viewModel.Content = "first saved note";
        await viewModel.SaveNowAsync();
        await viewModel.NewNoteCommand.ExecuteAsync(null);
        var second = viewModel.ActiveNote!;
        viewModel.Content = "saved before switch";

        await viewModel.SelectNoteCommand.ExecuteAsync(first);

        Assert.Contains(store.State.Notes,
            note => note.Id == second.Id && TextOf(note) == "saved before switch");
    }

    [Fact]
    public async Task Initialize_RestoresLastOpenedSavedNote()
    {
        var first = Note("first", "one", DateTimeOffset.UtcNow.AddHours(-1));
        var second = Note("second", "two", DateTimeOffset.UtcNow);
        var store = new RecordingNotesStore(new NotesStorageState
        {
            Notes = [first, second],
            LastOpenedNoteId = first.Id
        });
        var viewModel = new NotesToolViewModel(store);

        await viewModel.InitializeAsync();

        Assert.Equal(first.Id, viewModel.ActiveNote!.Id);
    }

    [Fact]
    public async Task Initialize_WhenTemporaryWasRecorded_RestoresMostRecentSavedNote()
    {
        var older = Note("older", "one", DateTimeOffset.UtcNow.AddHours(-1));
        var recent = Note("recent", "two", DateTimeOffset.UtcNow);
        var store = new RecordingNotesStore(new NotesStorageState
        {
            Notes = [older, recent],
            LastOpenedNoteId = Guid.NewGuid()
        });
        var viewModel = new NotesToolViewModel(store);

        await viewModel.InitializeAsync();

        Assert.Equal(recent.Id, viewModel.ActiveNote!.Id);
    }

    [Theory]
    [InlineData("Algorithms exam review tomorrow please", "Algorithms exam review tomorrow")]
    [InlineData("  לסיים   את הפרויקט\nהיום בבוקר  ", "לסיים את הפרויקט היום")]
    public async Task AutomaticTitle_UsesFirstFourWordsAndTracksContent(string content, string expected)
    {
        var (viewModel, _) = await CreateAsync();
        viewModel.Content = content;
        await viewModel.SaveNowAsync();
        Assert.Equal(expected, viewModel.ActiveTitle);

        viewModel.Content = "completely different beginning";
        await viewModel.SaveNowAsync();
        Assert.Equal("completely different beginning", viewModel.ActiveTitle);
    }

    [Fact]
    public async Task ManualRename_PersistsAndPreventsAutomaticRename()
    {
        var (viewModel, store) = await CreateAsync();
        viewModel.BeginRenameCommand.Execute(viewModel.ActiveNote);
        viewModel.RenameText = "My fixed title";
        await viewModel.SaveRenameCommand.ExecuteAsync(null);
        viewModel.Content = "content that would produce another title";
        await viewModel.SaveNowAsync();

        Assert.Equal("My fixed title", viewModel.ActiveTitle);
        Assert.Equal("My fixed title", Assert.Single(store.State.Notes).Title);
    }

    [Fact]
    public async Task DeleteCurrent_SelectsMostRecentlyUpdatedRemainingNote()
    {
        var (viewModel, store) = await CreateAsync();
        viewModel.Content = "first note";
        await viewModel.SaveNowAsync();
        var first = viewModel.ActiveNote!;
        await viewModel.NewNoteCommand.ExecuteAsync(null);
        var current = viewModel.ActiveNote!;
        viewModel.Content = "current note";
        await viewModel.SaveNowAsync();

        await viewModel.DeleteNoteCommand.ExecuteAsync(current);

        Assert.Same(first, viewModel.ActiveNote);
        Assert.Single(viewModel.Notes);
        Assert.Single(store.State.Notes);
    }

    [Fact]
    public async Task DeleteLastNote_CreatesFreshUntitledNote()
    {
        var (viewModel, _) = await CreateAsync();
        viewModel.Content = "only note";
        await viewModel.SaveNowAsync();

        await viewModel.DeleteNoteCommand.ExecuteAsync(viewModel.ActiveNote);

        Assert.Equal("Untitled note", Assert.Single(viewModel.Notes).Title);
        Assert.NotNull(viewModel.ActiveNote);
    }

    [Theory]
    [InlineData("PROJECT", "Project plan", 1)]
    [InlineData("  פרויקט  ", "תכנון פרויקט", 1)]
    [InlineData("", "anything", 2)]
    public async Task Search_FiltersTitlesOnly_TrimmedAndCaseInsensitive(
        string query, string matchingTitle, int expectedCount)
    {
        var first = Note(matchingTitle, "content", DateTimeOffset.UtcNow);
        var second = Note("Different title", "PROJECT appears only in content", DateTimeOffset.UtcNow);
        var store = new RecordingNotesStore(new NotesStorageState { Notes = [first, second] });
        var viewModel = new NotesToolViewModel(store);
        await viewModel.InitializeAsync();

        viewModel.SearchQuery = query;

        Assert.Equal(expectedCount, viewModel.FilteredNotes.Count);
        if (expectedCount == 1)
        {
            Assert.Equal(matchingTitle, viewModel.FilteredNotes[0].Title);
        }
    }

    [Fact]
    public async Task LeaveTemporary_DiscardsItAndReturnsToSavedNote()
    {
        var (viewModel, _) = await CreateAsync();
        viewModel.Content = "saved note";
        await viewModel.SaveNowAsync();
        var savedId = viewModel.ActiveNote!.Id;
        await viewModel.NewTemporaryNoteCommand.ExecuteAsync(null);
        viewModel.Content = "ephemeral";

        await viewModel.LeaveAsync();

        Assert.False(viewModel.IsTemporary);
        Assert.Equal(savedId, viewModel.ActiveNote!.Id);
        Assert.DoesNotContain(viewModel.Notes, note => TextOf(note) == "ephemeral");
    }

    [Fact]
    public async Task KeepEmptyTemporary_DoesNotCreateOrPersistBlankNote()
    {
        var (viewModel, store) = await CreateAsync();
        await viewModel.NewTemporaryNoteCommand.ExecuteAsync(null);
        var writesBeforeKeep = store.SaveCount;

        await viewModel.KeepTemporaryNoteCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsTemporary);
        Assert.Empty(viewModel.Notes);
        Assert.Empty(store.State.Notes);
        Assert.Equal(writesBeforeKeep, store.SaveCount);
    }

    [Fact]
    public async Task KeepTemporary_ImmediatelyUpdatesTitleListAndPersistence()
    {
        var (viewModel, store) = await CreateAsync(TimeSpan.FromSeconds(10));
        await viewModel.NewTemporaryNoteCommand.ExecuteAsync(null);
        viewModel.Content = "Project ideas for FloatingTools tomorrow";
        var writesBeforeKeep = store.SaveCount;

        await viewModel.KeepTemporaryNoteCommand.ExecuteAsync(null);

        Assert.Equal("Project ideas for FloatingTools", viewModel.ActiveTitle);
        Assert.Same(viewModel.ActiveNote, Assert.Single(viewModel.Notes));
        Assert.Equal(viewModel.ActiveNote!.Id, Assert.Single(store.State.Notes).Id);
        Assert.Equal(writesBeforeKeep + 1, store.SaveCount);
    }

    [Fact]
    public async Task AutomaticTitle_ReturnsToUntitledWhenContentIsDeleted()
    {
        var (viewModel, _) = await CreateAsync();

        viewModel.Content = "א";
        Assert.Equal("א", viewModel.ActiveTitle);
        viewModel.Content = "   \r\n ";

        Assert.Equal("Untitled note", viewModel.ActiveTitle);
    }

    [Fact]
    public async Task SearchReflectsLiveAutomaticTitleChanges()
    {
        var (viewModel, _) = await CreateAsync();
        viewModel.Content = "Initial project idea";
        viewModel.SearchQuery = "initial";
        Assert.Single(viewModel.FilteredNotes);

        viewModel.Content = "Different thought entirely";

        Assert.Empty(viewModel.FilteredNotes);
        viewModel.SearchQuery = "different";
        Assert.Single(viewModel.FilteredNotes);
    }

    [Fact]
    public async Task RepeatedNewNote_DoesNotAccumulateBlankNotes()
    {
        var (viewModel, store) = await CreateAsync();

        await viewModel.NewNoteCommand.ExecuteAsync(null);
        await viewModel.NewNoteCommand.ExecuteAsync(null);
        await viewModel.NewNoteCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Notes);
        Assert.Same(viewModel.ActiveNote, viewModel.Notes[0]);
        Assert.Empty(store.State.Notes);
    }

    [Fact]
    public async Task ErasedAutomaticNote_IsDiscardedWhenLeaving()
    {
        var (viewModel, store) = await CreateAsync();
        var erasedId = viewModel.ActiveNote!.Id;
        viewModel.Content = "meaningful content";
        await viewModel.SaveNowAsync();
        viewModel.Content = string.Empty;

        await viewModel.NewNoteCommand.ExecuteAsync(null);

        Assert.DoesNotContain(viewModel.Notes, note => note.Id == erasedId);
        Assert.DoesNotContain(store.State.Notes, note => note.Id == erasedId);
        Assert.Equal("Untitled note", viewModel.ActiveTitle);
    }

    [Fact]
    public async Task ManuallyRenamedEmptyNote_RemainsPersistedWhenLeaving()
    {
        var (viewModel, store) = await CreateAsync();
        var note = viewModel.ActiveNote!;
        viewModel.BeginRenameCommand.Execute(note);
        viewModel.RenameText = "Project ideas";
        await viewModel.SaveRenameCommand.ExecuteAsync(null);

        await viewModel.NewNoteCommand.ExecuteAsync(null);

        var persisted = Assert.Single(store.State.Notes);
        Assert.Equal(note.Id, persisted.Id);
        Assert.True(persisted.HasManualTitle);
        Assert.Equal("Project ideas", persisted.Title);
    }

    [Fact]
    public async Task DiscardingLastEmptyDraft_LeavesNewUsableActiveDraft()
    {
        var (viewModel, store) = await CreateAsync();
        var originalId = viewModel.ActiveNote!.Id;

        await viewModel.LeaveAsync();

        Assert.NotNull(viewModel.ActiveNote);
        Assert.Equal("Untitled note", viewModel.ActiveTitle);
        Assert.Empty(store.State.Notes);
        Assert.Single(viewModel.Notes);
        Assert.True(viewModel.ActiveNote!.IsActive);
        Assert.NotEqual(originalId, viewModel.ActiveNote.Id);
    }

    [Fact]
    public async Task BeginRename_EntersInlineEditStateForInactiveNote()
    {
        var first = Note("First note", "one", DateTimeOffset.UtcNow);
        var second = Note("Second note", "two", DateTimeOffset.UtcNow.AddMinutes(1));
        var store = new RecordingNotesStore(new NotesStorageState { Notes = [first, second] });
        var viewModel = new NotesToolViewModel(store);
        await viewModel.InitializeAsync();

        viewModel.BeginRenameCommand.Execute(first);

        Assert.Same(first, viewModel.RenamingNote);
        Assert.True(first.IsRenaming);
        Assert.Equal("First note", viewModel.RenameText);
    }

    [Fact]
    public async Task CancelRename_RestoresNormalRowWithoutPersisting()
    {
        var (viewModel, store) = await CreateAsync();
        var note = viewModel.ActiveNote!;
        viewModel.BeginRenameCommand.Execute(note);
        viewModel.RenameText = "Changed but cancelled";
        var writesBeforeCancel = store.SaveCount;

        viewModel.CancelRenameCommand.Execute(null);

        Assert.False(note.IsRenaming);
        Assert.Null(viewModel.RenamingNote);
        Assert.Equal("Untitled note", note.Title);
        Assert.Equal(writesBeforeCancel, store.SaveCount);
    }

    [Fact]
    public async Task BlankRename_IsRejectedAndDoesNotPersist()
    {
        var (viewModel, store) = await CreateAsync();
        var note = viewModel.ActiveNote!;
        viewModel.BeginRenameCommand.Execute(note);
        viewModel.RenameText = "   ";
        var writesBeforeSubmit = store.SaveCount;

        await viewModel.SaveRenameCommand.ExecuteAsync(null);

        Assert.Equal("Untitled note", note.Title);
        Assert.False(note.HasManualTitle);
        Assert.False(note.IsRenaming);
        Assert.Equal(writesBeforeSubmit, store.SaveCount);
    }

    private static async Task<(NotesToolViewModel ViewModel, RecordingNotesStore Store)> CreateAsync(
        TimeSpan? debounce = null)
    {
        var store = new RecordingNotesStore();
        var viewModel = new NotesToolViewModel(store, debounce);
        await viewModel.InitializeAsync();
        return (viewModel, store);
    }

    private static NoteDocument Note(string title, string content, DateTimeOffset updatedAt)
    {
        var note = new NoteDocument
        {
            Id = Guid.NewGuid(),
            Title = title,
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt,
            HasManualTitle = title != "Untitled note"
        };
        note.Blocks.Add(new TextNoteBlock { Text = content });
        return note;
    }

    private static string TextOf(NoteDocument note) =>
        string.Join(Environment.NewLine,
            note.Blocks.OfType<TextNoteBlock>().Select(block => block.Text));

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
                Blocks = new(note.Blocks.Select(CloneBlock))
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
