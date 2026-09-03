using FloatingTools.App.Models;

namespace FloatingTools.App.Notes.Persistence.Migration;

/// <summary>
/// Converts deserialized historical Notes data into the current runtime model.
/// This is intentionally a small, ordered compatibility pipeline rather than a
/// version graph: each transformation removes the legacy data it consumes, so
/// applying the pipeline again is a no-op for already-current documents.
/// </summary>
public static class NotesMigrationPipeline
{
    public static NotesStorageState Apply(NotesStorageState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        state.Notes ??= [];
        state.Notes = state.Notes
            .Where(note => note.Id != Guid.Empty && !note.IsTemporary)
            .Select(ApplyToNote)
            .Where(IsMeaningful)
            .OrderByDescending(note => note.UpdatedAt)
            .ToList();
        return state;
    }

    private static NoteDocument ApplyToNote(NoteDocument note)
    {
        // The first MVP used IsTitleLocked for generated and manual titles alike.
        // Its original intent cannot be recovered, so preserve the title as manual.
        if (note.IsTitleLocked)
        {
            note.HasManualTitle = true;
        }

        note.Blocks ??= [];
        if (note.Blocks.Count == 0 && !string.IsNullOrEmpty(note.Content))
        {
            note.Blocks.Add(new TextNoteBlock { Text = note.Content });
        }

        foreach (var block in note.Blocks)
        {
            if (block.Id == Guid.Empty)
            {
                block.Id = Guid.NewGuid();
            }
        }

        note.Content = null;
        ConvertLegacyLinkBlocks(note);
        RemoveInlineHyperlinkMetadata(note);
        NormalizeAdjacentTextBlocks(note);
        return note;
    }

    private static void ConvertLegacyLinkBlocks(NoteDocument note)
    {
        for (var index = 0; index < note.Blocks.Count; index++)
        {
            if (note.Blocks[index] is not LinkNoteBlock legacy)
            {
                continue;
            }

            if (index + 1 < note.Blocks.Count
                && note.Blocks[index + 1] is TextNoteBlock followingText)
            {
                followingText.PreserveBoundaryBefore = true;
            }

            var lines = (legacy.LegacyLinks ?? [])
                .Select(item => string.IsNullOrWhiteSpace(item.DisplayName)
                    ? item.Url
                    : item.DisplayName)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
            if (lines.Count == 0)
            {
                var value = !string.IsNullOrWhiteSpace(legacy.DisplayName)
                    ? legacy.DisplayName
                    : !string.IsNullOrWhiteSpace(legacy.LegacyDisplayText)
                        ? legacy.LegacyDisplayText
                        : legacy.Url;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    lines.Add(value);
                }
            }

            note.Blocks[index] = new TextNoteBlock
            {
                Id = legacy.Id,
                Text = string.Join(Environment.NewLine, lines),
                PreserveBoundaryBefore = true
            };
        }
    }

    private static void RemoveInlineHyperlinkMetadata(NoteDocument note)
    {
        foreach (var text in note.Blocks.OfType<TextNoteBlock>())
        {
            text.LegacyLinks = null;
        }
    }

    private static void NormalizeAdjacentTextBlocks(NoteDocument note)
    {
        for (var index = 1; index < note.Blocks.Count;)
        {
            if (note.Blocks[index - 1] is TextNoteBlock previous
                && note.Blocks[index] is TextNoteBlock current
                && !current.PreserveBoundaryBefore)
            {
                previous.Text += current.Text;
                note.Blocks.RemoveAt(index);
                continue;
            }

            index++;
        }
    }

    private static bool IsMeaningful(NoteDocument note) =>
        note.HasManualTitle
        || note.Blocks.OfType<TextNoteBlock>().Any(block =>
            !string.IsNullOrWhiteSpace(block.Text))
        || note.Blocks.OfType<ImageNoteBlock>().Any(block =>
            !string.IsNullOrWhiteSpace(block.AssetFileName))
        || note.Blocks.OfType<LinkListNoteBlock>().Any(block => block.Items.Count > 0);
}
