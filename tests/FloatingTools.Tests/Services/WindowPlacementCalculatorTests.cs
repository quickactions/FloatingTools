using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class WindowPlacementCalculatorTests
{
    private static readonly MonitorWorkArea PrimaryMonitor = new(
        @"\\.\DISPLAY1",
        new PixelRect(0, 0, 1920, 1080),
        new PixelRect(0, 0, 1920, 1040),
        1,
        true);

    [Theory]
    [InlineData(15, 87, DockSide.Left)]
    [InlineData(1810, 1882, DockSide.Right)]
    public void ChooseNearestDockSide_ReturnsClosestWorkAreaEdge(
        int left,
        int right,
        DockSide expected)
    {
        var window = new PixelRect(left, 200, right, 286);

        var result = WindowPlacementCalculator.ChooseNearestDockSide(
            window,
            PrimaryMonitor.WorkArea);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(-20, 0)]
    [InlineData(120, 120)]
    [InlineData(900, 632)]
    public void ClampVerticalOffset_ClampsToVisibleWorkArea(
        double requestedOffset,
        double expectedOffset)
    {
        var monitor = PrimaryMonitor with
        {
            WorkArea = new PixelRect(0, 40, 1920, 1080),
            DpiScale = 1.25
        };

        var result = WindowPlacementCalculator.ClampVerticalOffset(
            requestedOffset,
            windowHeightPixels: 250,
            monitor);

        Assert.Equal(expectedOffset, result);
    }

    [Fact]
    public void Resolve_UsesPrimaryDefaultWhenSavedMonitorIsMissing()
    {
        var secondaryMonitor = new MonitorWorkArea(
            @"\\.\DISPLAY2",
            new PixelRect(1920, 0, 3840, 1080),
            new PixelRect(1920, 0, 3840, 1040),
            1,
            false);
        var savedPlacement = new WindowPlacement(
            @"\\.\REMOVED_DISPLAY",
            DockSide.Left,
            9999);

        var result = WindowPlacementCalculator.Resolve(
            savedPlacement,
            [secondaryMonitor, PrimaryMonitor],
            windowWidthPixels: 72,
            windowHeightPixels: 200);

        Assert.Equal(PrimaryMonitor.MonitorId, result.Placement.MonitorId);
        Assert.Equal(DockSide.Right, result.Placement.DockSide);
        Assert.Equal(1848, result.Left);
        Assert.Equal(420, result.Top);
    }

    [Fact]
    public void SelectMonitor_UsesMonitorWithLargestWindowIntersection()
    {
        var secondaryMonitor = new MonitorWorkArea(
            @"\\.\DISPLAY2",
            new PixelRect(1920, 0, 3840, 1080),
            new PixelRect(1920, 0, 3840, 1040),
            1,
            false);
        var window = new PixelRect(1880, 100, 2080, 300);

        var result = WindowPlacementCalculator.SelectMonitor(
            window,
            [PrimaryMonitor, secondaryMonitor]);

        Assert.Equal(secondaryMonitor.MonitorId, result.MonitorId);
    }
}
