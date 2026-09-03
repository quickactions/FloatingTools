namespace FloatingTools.App.Controls;

public readonly record struct EditableTextBounds(
    double Top,
    double Bottom,
    int TextLength);

public readonly record struct EditableTextFocusTarget(
    int BlockIndex,
    int CaretIndex);

public static class NotesPageFocusResolver
{
    public static EditableTextFocusTarget? Resolve(
        double clickY,
        IReadOnlyList<EditableTextBounds> textBlocks)
    {
        if (textBlocks.Count == 0)
        {
            return null;
        }

        var bestIndex = 0;
        var bestDistance = double.PositiveInfinity;
        for (var index = 0; index < textBlocks.Count; index++)
        {
            var block = textBlocks[index];
            var distance = clickY < block.Top
                ? block.Top - clickY
                : clickY > block.Bottom
                    ? clickY - block.Bottom
                    : 0;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }

        var selected = textBlocks[bestIndex];
        return new EditableTextFocusTarget(
            bestIndex,
            clickY <= selected.Top ? 0 : selected.TextLength);
    }
}
