using FloatingTools.App.Models;
using FloatingTools.App.Notes;

namespace FloatingTools.Tests.Notes;

public sealed class NoteTitlePolicyTests
{
    [Fact]
    public void ResolveTitle_LeavesManualTitleUntouched() =>
        Assert.Equal("Chosen title", NoteTitlePolicy.ResolveTitle(new NoteDocument
        {
            Title = "Chosen title", HasManualTitle = true,
            Blocks = [new TextNoteBlock { Text = "automatic text" }]
        }));

    [Theory]
    [InlineData("one two three four five", "one two three")]
    [InlineData("לסיים את הפרויקט היום בבוקר", "לסיים את הפרויקט")]
    [InlineData("hello שלום", "hello שלום")]
    public void ResolveTitle_UsesExistingTextGenerator(string text, string expected) =>
        Assert.Equal(expected, NoteTitlePolicy.ResolveTitle(Note(text)));

    [Fact]
    public void ResolveTitle_CombinesTextBlocksInDocumentOrder() =>
        Assert.Equal("first second third", NoteTitlePolicy.ResolveTitle(new NoteDocument
        {
            Blocks = [new TextNoteBlock { Text = "first second" }, new TextNoteBlock { Text = "third fourth fifth" }]
        }));

    [Fact]
    public void ResolveTitle_UsesCustomNameForLinkOnlyNote() =>
        Assert.Equal("Project home", NoteTitlePolicy.ResolveTitle(Links("https://example.test", "Project home")));

    [Fact]
    public void ResolveTitle_UsesUrlHostWhenLinkHasNoCustomName() =>
        Assert.Equal("example.test", NoteTitlePolicy.ResolveTitle(Links("https://example.test/path", "https://example.test/path")));

    [Fact]
    public void ResolveTitle_UsesUntitledForImageOnlyAndEmptyNotes()
    {
        Assert.Equal("Untitled note", NoteTitlePolicy.ResolveTitle(new NoteDocument { Blocks = [new ImageNoteBlock { AssetFileName = "image.png" }] }));
        Assert.Equal("Untitled note", NoteTitlePolicy.ResolveTitle(new NoteDocument()));
    }

    [Fact]
    public void ResolveTitle_PrefersTextOverLinks() =>
        Assert.Equal("Text wins", NoteTitlePolicy.ResolveTitle(new NoteDocument
        {
            Blocks = [new LinkListNoteBlock { Items = [new NoteLinkItem { Url = "https://example.test", DisplayName = "Link title" }] }, new TextNoteBlock { Text = "Text wins" }]
        }));

    private static NoteDocument Note(string text) => new() { Blocks = [new TextNoteBlock { Text = text }] };

    private static NoteDocument Links(string url, string name) => new()
    {
        Blocks = [new LinkListNoteBlock { Items = [new NoteLinkItem { Url = url, DisplayName = name }] }]
    };
}
