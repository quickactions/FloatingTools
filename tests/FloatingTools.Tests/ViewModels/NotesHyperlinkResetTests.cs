using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.ViewModels;

public sealed class NotesHyperlinkResetTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"FloatingTools-reset-{Guid.NewGuid():N}");
    public NotesHyperlinkResetTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public async Task LegacySingleAndMultiLinks_MigrateToPlainTextWithoutLossOrDuplication()
    {
        var path = Path.Combine(_directory, "notes.json");
        var noteId = Guid.NewGuid();
        await File.WriteAllTextAsync(path, $$"""{"notes":[{"id":"{{noteId}}","blocks":[{"$type":"link","url":"https://a","displayText":"Alpha"},{"$type":"link","links":[{"url":"https://b","displayName":"Beta"},{"url":"https://c","displayName":"Gamma"}]}]}],"lastOpenedNoteId":"{{noteId}}"}""");
        var vm = new NotesToolViewModel(new JsonNotesStore(path));
        await vm.InitializeAsync();
        Assert.All(vm.ActiveBlocks, block => Assert.IsType<TextNoteBlock>(block));
        Assert.Equal(["Alpha", $"Beta{Environment.NewLine}Gamma"], vm.ActiveBlocks.Cast<TextNoteBlock>().Select(block => block.Text));

        var again = new NotesToolViewModel(new JsonNotesStore(path));
        await again.InitializeAsync();
        Assert.Equal(["Alpha", $"Beta{Environment.NewLine}Gamma"], again.ActiveBlocks.Cast<TextNoteBlock>().Select(block => block.Text));
    }

    [Fact]
    public async Task InlineMetadata_IsRemovedWhileTextIsPreserved()
    {
        var path = Path.Combine(_directory, "notes.json");
        var noteId = Guid.NewGuid();
        await File.WriteAllTextAsync(path, $$"""
        {
          "notes": [{
            "id": "{{noteId}}",
            "title": "Legacy inline",
            "createdAt": "2026-01-01T00:00:00+00:00",
            "updatedAt": "2026-01-01T00:00:00+00:00",
            "blocks": [{
              "$type": "text",
              "text": "Microsoft documentation",
              "links": [{"start": 0, "length": 9, "url": "https://example.com"}]
            }]
          }],
          "lastOpenedNoteId": "{{noteId}}"
        }
        """);
        var store = new JsonNotesStore(path);
        var vm = new NotesToolViewModel(store);
        await vm.InitializeAsync();
        var text = Assert.IsType<TextNoteBlock>(Assert.Single(vm.ActiveBlocks));
        Assert.Equal("Microsoft documentation", text.Text);
        Assert.Null(text.LegacyLinks);
        Assert.DoesNotContain("\"links\"", await File.ReadAllTextAsync(path));
    }
}
