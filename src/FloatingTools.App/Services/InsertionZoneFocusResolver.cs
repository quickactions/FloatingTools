using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

/// <summary>
/// Finds the editable block that should receive focus from an insertion zone.
/// It searches upward from the zone before searching downward; text and link-list
/// blocks are editable, while image and other block types are skipped.
/// </summary>
public static class InsertionZoneFocusResolver
{
    public static NoteBlock? Resolve(
        IReadOnlyList<NoteBlock> blocks,
        int insertionIndex)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentOutOfRangeException.ThrowIfNegative(insertionIndex);
        if (insertionIndex > blocks.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(insertionIndex));
        }

        for (var index = insertionIndex - 1; index >= 0; index--)
        {
            if (IsEditable(blocks[index]))
            {
                return blocks[index];
            }
        }

        for (var index = insertionIndex; index < blocks.Count; index++)
        {
            if (IsEditable(blocks[index]))
            {
                return blocks[index];
            }
        }

        return null;
    }

    private static bool IsEditable(NoteBlock block) =>
        block is TextNoteBlock or LinkListNoteBlock;
}
