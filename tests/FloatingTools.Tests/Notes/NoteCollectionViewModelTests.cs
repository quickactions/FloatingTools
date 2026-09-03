using FloatingTools.App.Models;
using FloatingTools.App.Notes;

namespace FloatingTools.Tests.Notes;

public sealed class NoteCollectionViewModelTests
{
    [Fact]
    public async Task NewNote_CreatesSavedActiveNote()
    {
        var fixture = new Fixture();

        await fixture.Collection.NewNoteAsync();

        var note = Assert.Single(fixture.Collection.Notes);
        Assert.Same(note, fixture.Active);
        Assert.False(note.IsTemporary);
        Assert.Equal(1, fixture.SaveCalls);
    }

    [Fact]
    public async Task NewTemporaryNote_CreatesActiveNoteWithoutSavedMembership()
    {
        var fixture = new Fixture();

        await fixture.Collection.NewTemporaryNoteAsync();

        Assert.True(fixture.Active!.IsTemporary);
        Assert.Empty(fixture.Collection.Notes);
        Assert.Equal(0, fixture.SaveCalls);
    }

    [Fact]
    public async Task KeepTemporaryNote_MakesMeaningfulTemporaryNoteSaved()
    {
        var fixture = new Fixture { IsMeaningfulResult = true };
        await fixture.Collection.NewTemporaryNoteAsync();

        await fixture.Collection.KeepTemporaryNoteAsync();

        Assert.False(fixture.Active!.IsTemporary);
        Assert.Contains(fixture.Active, fixture.Collection.Notes);
        Assert.Equal(1, fixture.AutomaticTitleUpdates);
        Assert.Equal(1, fixture.SaveCalls);
    }

    [Fact]
    public async Task KeepTemporaryNote_LeavesEmptyTemporaryNoteUnchanged()
    {
        var fixture = new Fixture { IsMeaningfulResult = false };
        await fixture.Collection.NewTemporaryNoteAsync();

        await fixture.Collection.KeepTemporaryNoteAsync();

        Assert.True(fixture.Active!.IsTemporary);
        Assert.Empty(fixture.Collection.Notes);
        Assert.Equal(0, fixture.SaveCalls);
    }

    [Fact]
    public async Task SelectNote_ResolvesCurrentThenActivatesTargetAndSaves()
    {
        var fixture = new Fixture();
        var current = fixture.AddSaved("current", 1);
        var target = fixture.AddSaved("target", 2);
        fixture.Active = current;

        await fixture.Collection.SelectNoteAsync(target);

        Assert.Equal(new[] { current.Id }, fixture.ResolvedNoteIds);
        Assert.Same(target, fixture.Active);
        Assert.Equal(1, fixture.SaveCalls);
    }

    [Fact]
    public async Task SelectActiveNote_OnlyClosesMenu()
    {
        var fixture = new Fixture();
        var note = fixture.AddSaved("current", 1);
        fixture.Active = note;
        fixture.Collection.IsMenuOpen = true;

        await fixture.Collection.SelectNoteAsync(note);

        Assert.False(fixture.Collection.IsMenuOpen);
        Assert.Empty(fixture.ResolvedNoteIds);
        Assert.Equal(0, fixture.SaveCalls);
    }

    [Fact]
    public async Task DeleteActiveNote_ActivatesRemainingMostRecentNote()
    {
        var fixture = new Fixture();
        var old = fixture.AddSaved("old", 1);
        var current = fixture.AddSaved("current", 2);
        fixture.Active = current;

        await fixture.Collection.DeleteNoteAsync(current);

        Assert.Same(old, fixture.Active);
        Assert.DoesNotContain(current, fixture.Collection.Notes);
        Assert.Equal(1, fixture.UndoDiscardCalls);
        Assert.Equal(1, fixture.AssetCleanupCalls);
        Assert.Equal(1, fixture.SaveCalls);
    }

    [Fact]
    public async Task DeleteNonActiveNote_PreservesActiveNote()
    {
        var fixture = new Fixture();
        var active = fixture.AddSaved("active", 2);
        var deleted = fixture.AddSaved("deleted", 1);
        fixture.Active = active;

        await fixture.Collection.DeleteNoteAsync(deleted);

        Assert.Same(active, fixture.Active);
        Assert.DoesNotContain(deleted, fixture.Collection.Notes);
        Assert.Equal(1, fixture.SaveCalls);
    }

    [Fact]
    public async Task DeleteLastNote_CreatesAndSelectsReplacement()
    {
        var fixture = new Fixture();
        var only = fixture.AddSaved("only", 1);
        fixture.Active = only;

        await fixture.Collection.DeleteNoteAsync(only);

        Assert.Single(fixture.Collection.Notes);
        Assert.Same(fixture.Collection.Notes.Single(), fixture.Active);
        Assert.False(fixture.Active!.IsTemporary);
    }

    [Fact]
    public async Task RenameCommit_TrimsLocksTitleAndPersists()
    {
        var fixture = new Fixture();
        var note = fixture.AddSaved("old", 1);
        fixture.Collection.BeginRename(note);
        fixture.Collection.RenameText = "  renamed  ";

        await fixture.Collection.SaveRenameAsync();

        Assert.Equal("renamed", note.Title);
        Assert.True(note.HasManualTitle);
        Assert.False(note.IsTitleLocked);
        Assert.Null(fixture.Collection.RenamingNote);
        Assert.Equal(1, fixture.SaveCalls);
    }

    [Fact]
    public void RenameCancel_RestoresNormalRenameState()
    {
        var fixture = new Fixture();
        var note = fixture.AddSaved("old", 1);
        fixture.Collection.BeginRename(note);

        fixture.Collection.CancelRename();

        Assert.False(note.IsRenaming);
        Assert.Null(fixture.Collection.RenamingNote);
        Assert.Equal(string.Empty, fixture.Collection.RenameText);
    }

    [Fact]
    public async Task BlankRename_CancelsWithoutChangingTitleOrSaving()
    {
        var fixture = new Fixture();
        var note = fixture.AddSaved("old", 1);
        fixture.Collection.BeginRename(note);
        fixture.Collection.RenameText = " \t ";

        await fixture.Collection.SaveRenameAsync();

        Assert.Equal("old", note.Title);
        Assert.Null(fixture.Collection.RenamingNote);
        Assert.Equal(0, fixture.SaveCalls);
    }

    [Fact]
    public void SearchFiltering_IsCaseInsensitiveAndKeepsMostRecentFirst()
    {
        var fixture = new Fixture();
        var older = fixture.AddSaved("hello older", 1);
        var newer = fixture.AddSaved("HELLO newer", 3);
        fixture.AddSaved("other", 2);
        fixture.Collection.SearchQuery = "hello";

        Assert.Equal(new[] { newer, older }, fixture.Collection.FilteredNotes);
    }

    [Fact]
    public async Task TemporaryMembership_UsesIsMeaningfulCallbackRatherThanImplicitContentRule()
    {
        var fixture = new Fixture { IsMeaningfulResult = false };
        await fixture.Collection.NewTemporaryNoteAsync();
        fixture.Active!.Blocks.OfType<TextNoteBlock>().Single().Text = "has text";

        await fixture.Collection.KeepTemporaryNoteAsync();

        Assert.Equal(1, fixture.IsMeaningfulCalls);
        Assert.True(fixture.Active.IsTemporary);
        Assert.Empty(fixture.Collection.Notes);
    }

    [Fact]
    public void FilteredNotes_WithEmptySearch_PreservesPublicUpdatedAtOrdering()
    {
        var fixture = new Fixture();
        var oldest = fixture.AddSaved("oldest", 1);
        var newest = fixture.AddSaved("newest", 3);
        var middle = fixture.AddSaved("middle", 2);

        Assert.Equal(new[] { newest, middle, oldest }, fixture.Collection.FilteredNotes);
    }

    private sealed class Fixture
    {
        private readonly DateTimeOffset _origin = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private NoteCollectionViewModel? _collection;

        public Fixture()
        {
            _collection = new NoteCollectionViewModel(
                () => _origin.AddMinutes(100),
                () => Active,
                note =>
                {
                    ResolvedNoteIds.Add(note.Id);
                    return Task.CompletedTask;
                },
                note => Active = note,
                note => { },
                note =>
                {
                    if (!_collection!.Notes.Contains(note)) _collection.Notes.Add(note);
                },
                note => { },
                _ =>
                {
                    IsMeaningfulCalls++;
                    return IsMeaningfulResult;
                },
                _ => AutomaticTitleUpdates++,
                _ => ["image.png"],
                _ =>
                {
                    UndoDiscardCalls++;
                    return ["undo-image.png"];
                },
                _ =>
                {
                    AssetCleanupCalls++;
                    return Task.CompletedTask;
                },
                () =>
                {
                    SaveCalls++;
                    return Task.CompletedTask;
                });
        }

        public NoteCollectionViewModel Collection => _collection!;
        public NoteDocument? Active { get; set; }
        public bool IsMeaningfulResult { get; set; }
        public int IsMeaningfulCalls { get; private set; }
        public int AutomaticTitleUpdates { get; private set; }
        public int UndoDiscardCalls { get; private set; }
        public int AssetCleanupCalls { get; private set; }
        public int SaveCalls { get; private set; }
        public List<Guid> ResolvedNoteIds { get; } = [];

        public NoteDocument AddSaved(string title, int minute)
        {
            var note = new NoteDocument
            {
                Id = Guid.NewGuid(),
                Title = title,
                CreatedAt = _origin,
                UpdatedAt = _origin.AddMinutes(minute)
            };
            note.Blocks.Add(new TextNoteBlock());
            Collection.Notes.Add(note);
            return note;
        }

        private NoteDocument CreateNote(bool isTemporary)
        {
            var note = new NoteDocument
            {
                Id = Guid.NewGuid(),
                Title = "Untitled note",
                CreatedAt = _origin,
                UpdatedAt = _origin.AddMinutes(100),
                IsTemporary = isTemporary
            };
            note.Blocks.Add(new TextNoteBlock());
            return note;
        }
    }
}
