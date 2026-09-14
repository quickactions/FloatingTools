using FloatingTools.App.Services;
using FloatingTools.App.Models;

namespace FloatingTools.Tests.Services;

public sealed class CalendarDayPanelSizingTests
{
    [Fact]
    public void ApprovedNarrowBaselineIsPreservedByCentralRatios()
    {
        Assert.Equal(106,
            CalendarDayPanelSizing.GetDefaultHeight(CalendarDayPanelSizing.BaselineRegionHeight),
            precision: 6);
        Assert.Equal(268,
            CalendarDayPanelSizing.GetMaximumHeight(CalendarDayPanelSizing.BaselineRegionHeight),
            precision: 6);
        Assert.Equal(92, CalendarDayPanelSizing.CompactMinimumHeight);
        Assert.Equal(180, CalendarDayPanelSizing.LargeMinimumHeight);
        Assert.Equal(122, CalendarDayPanelSizing.EditorMinimumHeight);
    }

    [Fact]
    public void LargerRegionsScaleBothDefaultAndMaximumProportionally()
    {
        const double regionHeight = 638.2;

        Assert.Equal(
            regionHeight * CalendarDayPanelSizing.DefaultHeightRatio,
            CalendarDayPanelSizing.GetDefaultHeight(regionHeight));
        Assert.Equal(
            regionHeight * CalendarDayPanelSizing.MaximumHeightRatio,
            CalendarDayPanelSizing.GetMaximumHeight(regionHeight));
        Assert.True(CalendarDayPanelSizing.GetDefaultHeight(regionHeight) > 106);
        Assert.True(CalendarDayPanelSizing.GetMaximumHeight(regionHeight) > 268);
    }

    [Theory]
    [InlineData(50, 268, 92)]
    [InlineData(180, 268, 180)]
    [InlineData(400, 268, 268)]
    [InlineData(100, 268, 122, 122)]
    public void ClampHonorsCurrentBounds(
        double height,
        double maximum,
        double expected,
        double minimum = CalendarDayPanelSizing.CompactMinimumHeight)
    {
        Assert.Equal(expected, CalendarDayPanelSizing.Clamp(height, maximum, minimum));
    }

    [Theory]
    [InlineData(CalendarLayoutMode.Compact, false, 92)]
    [InlineData(CalendarLayoutMode.Compact, true, 122)]
    [InlineData(CalendarLayoutMode.Large, false, 180)]
    [InlineData(CalendarLayoutMode.Large, true, 180)]
    public void MinimumHeightCombinesLayoutAndEditorRequirements(
        CalendarLayoutMode layoutMode,
        bool isEditorOpen,
        double expected)
    {
        Assert.Equal(expected,
            CalendarDayPanelSizing.GetMinimumHeight(layoutMode, isEditorOpen));
    }
}
