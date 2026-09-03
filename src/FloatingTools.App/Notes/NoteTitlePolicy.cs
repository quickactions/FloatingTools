using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.App.Notes;

/// <summary>
/// Determines the title a note should display without deciding whether the note
/// is meaningful or should be persisted.
/// </summary>
public static class NoteTitlePolicy
{
    public static string ResolveTitle(NoteDocument note)
    {
        ArgumentNullException.ThrowIfNull(note);

        if (note.HasManualTitle)
        {
            return note.Title;
        }

        var text = string.Join(
            Environment.NewLine,
            note.Blocks.OfType<TextNoteBlock>().Select(block => block.Text));
        if (!string.IsNullOrWhiteSpace(text))
        {
            return NoteTitleGenerator.Generate(text);
        }

        var firstLink = note.Blocks.OfType<LinkListNoteBlock>()
            .SelectMany(block => block.Items)
            .FirstOrDefault();
        if (firstLink is null)
        {
            return NoteTitleGenerator.UntitledTitle;
        }

        if (!string.IsNullOrWhiteSpace(firstLink.DisplayName)
            && !string.Equals(firstLink.DisplayName, firstLink.Url, StringComparison.Ordinal))
        {
            return NoteTitleGenerator.Generate(firstLink.DisplayName);
        }

        return Uri.TryCreate(firstLink.Url, UriKind.Absolute, out var uri)
            ? uri.Host
            : firstLink.Url;
    }
}
