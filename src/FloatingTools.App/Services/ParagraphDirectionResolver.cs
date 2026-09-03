using System.Windows;

namespace FloatingTools.App.Services;

public static class ParagraphDirectionResolver
{
    public static FlowDirection Resolve(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return FlowDirection.LeftToRight;
        }

        foreach (var character in text)
        {
            if (character is >= '\u0590' and <= '\u05FF'
                or >= '\uFB1D' and <= '\uFB4F')
            {
                return FlowDirection.RightToLeft;
            }

            if (char.IsLetter(character))
            {
                return FlowDirection.LeftToRight;
            }
        }

        return FlowDirection.LeftToRight;
    }

    public static TextAlignment ResolveAlignment(string? text) =>
        Resolve(text) == FlowDirection.RightToLeft
            ? TextAlignment.Right
            : TextAlignment.Left;
}
