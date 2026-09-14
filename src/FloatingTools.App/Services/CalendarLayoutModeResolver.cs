using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public static class CalendarLayoutModeResolver
{
    // Existing Calendar breakpoint promoted from the Full Events toolbar so
    // every size-dependent Calendar behavior uses one layout decision.
    public const double LargeWidthThreshold = 420;

    public static CalendarLayoutMode Resolve(double width) =>
        width >= LargeWidthThreshold ? CalendarLayoutMode.Large : CalendarLayoutMode.Compact;
}
