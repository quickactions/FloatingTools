namespace FloatingTools.App.Controls;

public readonly record struct TextSelectionRange(int Start, int Length);

public static class NotesTextSelection
{
    public static TextSelectionRange GetLogicalLine(string text, int characterIndex)
    {
        text ??= string.Empty;
        var index = Math.Clamp(characterIndex, 0, text.Length);
        var start = index == 0 ? 0 : text.LastIndexOf('\n', index - 1) + 1;
        var end = text.IndexOf('\n', index);
        if (end < 0)
        {
            end = text.Length;
        }

        if (end > start && text[end - 1] == '\r')
        {
            end--;
        }

        return new TextSelectionRange(start, end - start);
    }
}
