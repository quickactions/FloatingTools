using System.Windows;

namespace FloatingTools.App.SharedUi.Direction;

/// <summary>
/// Maps semantic text direction onto WPF presentation values. WPF mirrors the
/// coordinate system of an RTL text presenter, so semantic right alignment maps
/// to its logical left edge and therefore the physical right edge.
/// </summary>
public static class TextDirectionWpfExtensions
{
    public static FlowDirection ToFlowDirection(this TextDirectionResolution resolution) =>
        resolution.Direction == TextDirection.RightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

    public static TextAlignment ToPhysicalTextAlignment(
        this TextDirectionResolution resolution)
    {
        var isSemanticallyRightAligned =
            resolution.Alignment == TextDirectionAlignment.Right;
        return resolution.Direction == TextDirection.RightToLeft
            ? isSemanticallyRightAligned
                ? TextAlignment.Left
                : TextAlignment.Right
            : isSemanticallyRightAligned
                ? TextAlignment.Right
                : TextAlignment.Left;
    }
}
