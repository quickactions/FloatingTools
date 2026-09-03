using System.Collections.ObjectModel;
using FloatingTools.App.Models;
using FloatingTools.App.Notes.Persistence.Migration;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Notes.Persistence.Migration;

public sealed class NotesMigrationPipelineTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "FloatingTools.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Apply_CurrentDocument_IsANoOpAndIsIdempotent()
    {
        var noteId = Guid.NewGuid();
        var textId = Guid.NewGuid();
        var imageId = Guid.NewGuid();
        var linksId = Guid.NewGuid();
        var created = DateTimeOffset.Parse("2026-02-01T10:00:00+00:00");
        var updated = DateTimeOffset.Parse("2026-02-02T11:00:00+00:00");
        var state = new NotesStorageState
        {
            LastOpenedNoteId = noteId,
            Notes =
            [
                new NoteDocument
                {
                    Id = noteId,
                    Title = "Current note",
                    HasManualTitle = true,
                    CreatedAt = created,
                    UpdatedAt = updated,
                    Blocks = new ObservableCollection<NoteBlock>
                    {
                        new TextNoteBlock { Id = textId, Text = "Current text" },
                        new ImageNoteBlock { Id = imageId, AssetFileName = "current.png" },
                        new LinkListNoteBlock
                        {
                            Id = linksId,
                            Items = new([new NoteLinkItem
                            {
                                Url = "https://current.test",
                                DisplayName = "Current link"
                            }])
                        }
                    }
                }
            ]
        };

        var before = Describe(Assert.Single(state.Notes));
        NotesMigrationPipeline.Apply(state);
        var afterFirstApply = Describe(Assert.Single(state.Notes));
        NotesMigrationPipeline.Apply(state);
        var afterSecondApply = Describe(Assert.Single(state.Notes));

        AssertEquivalent(before, afterFirstApply);
        AssertEquivalent(afterFirstApply, afterSecondApply);
        Assert.Equal(noteId, state.LastOpenedNoteId);
    }

    [Fact]
    public void Apply_LegacyMixedDocument_PreservesCurrentContentAndRemovesCompatibilityTypes()
    {
        var noteId = Guid.NewGuid();
        var textId = Guid.NewGuid();
        var singleLinkId = Guid.NewGuid();
        var imageId = Guid.NewGuid();
        var multiLinkId = Guid.NewGuid();
        var trailingTextId = Guid.NewGuid();
        var state = new NotesStorageState
        {
            LastOpenedNoteId = noteId,
            Notes =
            [
                new NoteDocument
                {
                    Id = noteId,
                    Title = "Legacy mixed note",
                    IsTitleLocked = true,
                    CreatedAt = DateTimeOffset.Parse("2025-04-03T02:01:00+00:00"),
                    UpdatedAt = DateTimeOffset.Parse("2025-05-04T03:02:00+00:00"),
                    Blocks = new ObservableCollection<NoteBlock>
                    {
                        new TextNoteBlock
                        {
                            Id = textId,
                            Text = "Before",
                            LegacyLinks = null
                        },
                        new LinkNoteBlock
                        {
                            Id = singleLinkId,
                            Url = "https://raw-url.test"
                        },
                        new ImageNoteBlock
                        {
                            Id = imageId,
                            AssetFileName = "legacy-image.png",
                            NaturalWidth = 640,
                            NaturalHeight = 320
                        },
                        new LinkNoteBlock
                        {
                            Id = multiLinkId,
                            LegacyLinks = new([
                                new LegacyLinkNoteItem
                                {
                                    Url = "https://one.test",
                                    DisplayName = "First item"
                                },
                                new LegacyLinkNoteItem
                                {
                                    Url = "https://two.test",
                                    DisplayName = ""
                                }
                            ])
                        },
                        new TextNoteBlock
                        {
                            Id = trailingTextId,
                            Text = "After",
                            LegacyLinks = new([
                                new NoteHyperlink { Start = -1, Length = 99, Url = "https://ignored.test" }
                            ])
                        }
                    }
                }
            ]
        };

        NotesMigrationPipeline.Apply(state);
        var note = Assert.Single(state.Notes);

        Assert.True(note.HasManualTitle);
        Assert.Collection(note.Blocks,
            block =>
            {
                var text = Assert.IsType<TextNoteBlock>(block);
                Assert.Equal(textId, text.Id);
                Assert.Equal("Before", text.Text);
                Assert.Null(text.LegacyLinks);
            },
            block =>
            {
                var text = Assert.IsType<TextNoteBlock>(block);
                Assert.Equal(singleLinkId, text.Id);
                Assert.Equal("https://raw-url.test", text.Text);
            },
            block =>
            {
                var image = Assert.IsType<ImageNoteBlock>(block);
                Assert.Equal(imageId, image.Id);
                Assert.Equal("legacy-image.png", image.AssetFileName);
            },
            block =>
            {
                var text = Assert.IsType<TextNoteBlock>(block);
                Assert.Equal(multiLinkId, text.Id);
                Assert.Equal($"First item{Environment.NewLine}https://two.test", text.Text);
            },
            block =>
            {
                var text = Assert.IsType<TextNoteBlock>(block);
                Assert.Equal(trailingTextId, text.Id);
                Assert.Equal("After", text.Text);
                Assert.Null(text.LegacyLinks);
            });
        Assert.DoesNotContain(note.Blocks, block => block is LinkNoteBlock);

        var migrated = Describe(note);
        NotesMigrationPipeline.Apply(state);
        AssertEquivalent(migrated, Describe(Assert.Single(state.Notes)));
    }

    [Fact]
    public async Task JsonStore_MigratesAndRoundTripsALegacyFixtureWithoutLegacyFields()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "notes.json");
        var noteId = Guid.NewGuid();
        var textId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var imageId = Guid.NewGuid();
        await File.WriteAllTextAsync(path, $$"""
        {
          "notes": [{
            "id": "{{noteId}}",
            "title": "Stored historical note",
            "content": "",
            "isTitleLocked": true,
            "createdAt": "2025-01-02T03:04:05+00:00",
            "updatedAt": "2025-01-03T04:05:06+00:00",
            "blocks": [
              { "$type": "text", "id": "{{textId}}", "text": "Visible text", "links": null },
              { "$type": "link", "id": "{{linkId}}", "links": [
                { "url": "https://first.test", "displayName": "First" },
                { "url": "https://second.test", "displayName": "Second" }
              ] },
              { "$type": "image", "id": "{{imageId}}", "assetFileName": "fixture.png", "naturalWidth": 200, "naturalHeight": 100 }
            ]
          }],
          "lastOpenedNoteId": "{{noteId}}"
        }
        """);

        var store = new JsonNotesStore(path);
        var migrated = await store.LoadAsync();
        var beforeRoundTrip = Describe(Assert.Single(migrated.Notes));

        await store.SaveAsync(migrated);
        var serialized = await File.ReadAllTextAsync(path);
        var reloaded = await store.LoadAsync();

        Assert.DoesNotContain("\"$type\": \"link\"", serialized);
        Assert.DoesNotContain("\"links\"", serialized);
        AssertEquivalent(beforeRoundTrip, Describe(Assert.Single(reloaded.Notes)));
        Assert.Equal(noteId, reloaded.LastOpenedNoteId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static NoteDescription Describe(NoteDocument note) => new(
        note.Id,
        note.Title,
        note.HasManualTitle,
        note.CreatedAt,
        note.UpdatedAt,
        note.Blocks.Select(block => block switch
        {
            TextNoteBlock text => $"text:{text.Id}:{text.Text}:{text.PreserveBoundaryBefore}",
            ImageNoteBlock image => $"image:{image.Id}:{image.AssetFileName}:{image.NaturalWidth}:{image.NaturalHeight}",
            LinkListNoteBlock links => $"linkList:{links.Id}:{string.Join('|', links.Items.Select(item => item.Url + ':' + item.DisplayName))}",
            LinkNoteBlock legacy => $"legacy:{legacy.Id}",
            _ => block.GetType().Name
        }).ToArray());

    private static void AssertEquivalent(NoteDescription expected, NoteDescription actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.HasManualTitle, actual.HasManualTitle);
        Assert.Equal(expected.CreatedAt, actual.CreatedAt);
        Assert.Equal(expected.UpdatedAt, actual.UpdatedAt);
        Assert.Equal(expected.Blocks, actual.Blocks);
    }

    private sealed record NoteDescription(
        Guid Id,
        string Title,
        bool HasManualTitle,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        string[] Blocks);
}
