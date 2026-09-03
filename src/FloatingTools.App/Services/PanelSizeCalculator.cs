namespace FloatingTools.App.Services;

using FloatingTools.App.Models;

public static class PanelSizeCalculator
{
    public const double StandardWidth = 300;
    public const double StandardHeight = 500;
    public const double LargeWidth = 560;
    public const double LargeHeight = 760;
    public const double MinimumActiveToolHeight = 320;
    public const double ToolMenuWidth = 220;
    public const double ToolMenuHeaderHeight = 42;
    public const double ToolMenuIconSize = 48;
    public const double ToolMenuVerticalPadding = 28;
    public const int ToolMenuColumns = 4;

    public static ToolSize GetActiveToolSize(
        PanelSizePreset preset,
        double availableWidthDip,
        double availableHeightDip)
    {
        var requested = GetRequestedActiveToolSize(preset);
        var availableWidth = NormalizeAvailable(availableWidthDip);
        var availableHeight = NormalizeAvailable(availableHeightDip);
        var height = Math.Min(requested.Height, availableHeight);
        if (availableHeight >= MinimumActiveToolHeight)
        {
            height = Math.Max(MinimumActiveToolHeight, height);
        }

        return new ToolSize(
            Math.Min(requested.Width, availableWidth),
            height);
    }

    public static double GetRequiredActiveToolHeight(
        PanelSizePreset preset,
        double maximumAvailableHeightDip)
    {
        var availableHeight = NormalizeAvailable(maximumAvailableHeightDip);
        if (availableHeight < MinimumActiveToolHeight)
        {
            return availableHeight;
        }

        return Math.Clamp(
            GetRequestedActiveToolSize(preset).Height,
            MinimumActiveToolHeight,
            availableHeight);
    }

    public static ToolSize GetRequestedActiveToolSize(PanelSizePreset preset)
    {
        return preset == PanelSizePreset.Large
            ? new ToolSize(LargeWidth, LargeHeight)
            : new ToolSize(StandardWidth, StandardHeight);
    }

    public static PanelSizePreset NormalizePreset(PanelSizePreset preset)
    {
        return Enum.IsDefined(preset)
            ? preset
            : PanelSizePreset.Standard;
    }

    private static double NormalizeAvailable(double value)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            return 0;
        }

        return value;
    }

    public static double GetToolMenuHeight(int toolCount)
    {
        var visibleToolCount = Math.Max(1, toolCount);
        var rowCount = (int)Math.Ceiling(visibleToolCount / (double)ToolMenuColumns);
        return ToolMenuHeaderHeight
            + ToolMenuVerticalPadding
            + (rowCount * ToolMenuIconSize);
    }
}
