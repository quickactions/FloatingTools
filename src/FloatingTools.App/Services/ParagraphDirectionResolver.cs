using System.Windows;
using FloatingTools.App.SharedUi.Direction;

namespace FloatingTools.App.Services;

public static class ParagraphDirectionResolver
{
    public static FlowDirection Resolve(string? text)
    {
        return TextDirectionResolver.Resolve(text).Direction == TextDirection.RightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }

    public static TextAlignment ResolveAlignment(string? text) =>
        Resolve(text) == FlowDirection.RightToLeft
            ? TextAlignment.Right
            : TextAlignment.Left;
}
