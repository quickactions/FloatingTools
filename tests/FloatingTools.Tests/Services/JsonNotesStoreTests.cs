using System.IO;
using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class JsonNotesStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "FloatingTools.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAndLoad_RoundTripsUnicodeAndLastOpenedNote()
    {
        var path = Path.Combine(_directory, "notes.json");
        var store = new JsonNotesStore(path);
        var note = new NoteDocument
        {
            Id = Guid.NewGuid(), Title = "פתק עברי", Content = "שלום\nHello",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            IsTitleLocked = true
        };

        await store.SaveAsync(new NotesStorageState { Notes = [note], LastOpenedNoteId = note.Id });
        var restored = await store.LoadAsync();

        Assert.Equal(note.Id, restored.LastOpenedNoteId);
        var restoredNote = Assert.Single(restored.Notes);
        Assert.Equal("שלום\nHello",
            Assert.IsType<TextNoteBlock>(Assert.Single(restoredNote.Blocks)).Text);
        Assert.Null(restoredNote.Content);
        Assert.True(restoredNote.HasManualTitle);
    }

    [Fact]
    public async Task MissingFile_ReturnsEmptyState()
    {
        var state = await new JsonNotesStore(Path.Combine(_directory, "missing.json")).LoadAsync();
        Assert.Empty(state.Notes);
        Assert.Null(state.LastOpenedNoteId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ broken json")]
    public async Task EmptyOrMalformedFile_ReturnsEmptyState(string content)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "notes.json");
        await File.WriteAllTextAsync(path, content);

        var state = await new JsonNotesStore(path).LoadAsync();

        Assert.Empty(state.Notes);
    }

    [Fact]
    public async Task Load_DropsLegacyTemporaryNotes()
    {
        var path = Path.Combine(_directory, "notes.json");
        var store = new JsonNotesStore(path);
        var temporary = new NoteDocument
        {
            Id = Guid.NewGuid(), Title = "temp", IsTemporary = true,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        await store.SaveAsync(new NotesStorageState { Notes = [temporary] });

        Assert.Empty((await store.LoadAsync()).Notes);
    }

    [Fact]
    public async Task Load_MigratesLegacyLockedTitleAsManualToPreserveIt()
    {
        var path = Path.Combine(_directory, "notes.json");
        var store = new JsonNotesStore(path);
        var legacy = new NoteDocument
        {
            Id = Guid.NewGuid(),
            Title = "Do not overwrite this title",
            Content = "changed content",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IsTitleLocked = true,
            HasManualTitle = false
        };
        await store.SaveAsync(new NotesStorageState { Notes = [legacy] });

        var restored = Assert.Single((await store.LoadAsync()).Notes);

        Assert.True(restored.HasManualTitle);
        Assert.Equal("Do not overwrite this title", restored.Title);
        Assert.Equal("changed content",
            Assert.IsType<TextNoteBlock>(Assert.Single(restored.Blocks)).Text);
    }

    [Fact]
    public async Task Load_MigratesRawLegacyJsonWithoutChangingIdentityOrTimestamps()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "notes.json");
        var id = Guid.NewGuid();
        const string created = "2026-08-20T10:15:00+00:00";
        const string updated = "2026-08-21T12:30:00+00:00";
        var json = $$"""
        {
          "notes": [{
            "id": "{{id}}",
            "title": "Legacy title",
            "content": "legacy text",
            "createdAt": "{{created}}",
            "updatedAt": "{{updated}}",
            "isTemporary": false,
            "hasManualTitle": true
          }],
          "lastOpenedNoteId": "{{id}}"
        }
        """;
        await File.WriteAllTextAsync(path, json);

        var state = await new JsonNotesStore(path).LoadAsync();

        var note = Assert.Single(state.Notes);
        Assert.Equal(id, note.Id);
        Assert.Equal(id, state.LastOpenedNoteId);
        Assert.Equal("Legacy title", note.Title);
        Assert.True(note.HasManualTitle);
        Assert.Equal(DateTimeOffset.Parse(created), note.CreatedAt);
        Assert.Equal(DateTimeOffset.Parse(updated), note.UpdatedAt);
        Assert.Equal("legacy text", Assert.IsType<TextNoteBlock>(Assert.Single(note.Blocks)).Text);
    }

    [Fact]
    public async Task StructuredImageNote_RoundTripsReferencesWithoutEmbeddingImageBytes()
    {
        var path = Path.Combine(_directory, "notes.json");
        var note = new NoteDocument
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        note.Blocks.Add(new ImageNoteBlock
        {
            AssetFileName = "managed.png",
            NaturalWidth = 640,
            NaturalHeight = 320,
            DisplayWidth = 280
        });
        var store = new JsonNotesStore(path);

        await store.SaveAsync(new NotesStorageState { Notes = [note] });
        var json = await File.ReadAllTextAsync(path);
        var restored = Assert.Single((await store.LoadAsync()).Notes);

        var image = Assert.IsType<ImageNoteBlock>(Assert.Single(restored.Blocks));
        Assert.Equal("managed.png", image.AssetFileName);
        Assert.Equal(280, image.DisplayWidth);
        Assert.Contains("\"$type\": \"image\"", json);
        Assert.DoesNotContain("base64", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("assetPath", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LinkList_RoundTripsOrderedItemsWithDistinctDiscriminator()
    {
        var path = Path.Combine(_directory, "notes.json");
        var note = new NoteDocument
        {
            Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            Blocks = new(new NoteBlock[]
            {
                new LinkListNoteBlock
                {
                    Items = new([
                        new NoteLinkItem { Url = "https://one.test", DisplayName = "One" },
                        new NoteLinkItem { Url = "https://two.test", DisplayName = "Two" }
                    ])
                }
            })
        };
        var store = new JsonNotesStore(path);

        await store.SaveAsync(new NotesStorageState { Notes = [note], LastOpenedNoteId = note.Id });
        var json = await File.ReadAllTextAsync(path);
        var restored = Assert.IsType<LinkListNoteBlock>(Assert.Single(
            Assert.Single((await store.LoadAsync()).Notes).Blocks));

        Assert.Contains("\"$type\": \"linkList\"", json);
        Assert.DoesNotContain("\"$type\": \"link\"", json);
        Assert.Equal(["One", "Two"], restored.Items.Select(item => item.DisplayName));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
