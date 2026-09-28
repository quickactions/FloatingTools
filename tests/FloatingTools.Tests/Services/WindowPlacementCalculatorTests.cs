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
    [InlineData(100, 10, DockSide.Left)] // Top-left corner.
    [InlineData(1820, 10, DockSide.Right)] // Top-right corner.
    [InlineData(960, 10, DockSide.Top)] // Top-center.
    [InlineData(480, 10, DockSide.Top)] // Upper-left quarter, outside corner zone.
    [InlineData(1440, 10, DockSide.Top)] // Upper-right quarter, outside corner zone.
    [InlineData(100, 500, DockSide.Left)]
    [InlineData(1820, 500, DockSide.Right)]
    [InlineData(287, 10, DockSide.Left)] // Just inside left 15% zone.
    [InlineData(288, 10, DockSide.Left)] // Exactly on left threshold.
    [InlineData(289, 10, DockSide.Top)] // Just outside left zone.
    [InlineData(1633, 10, DockSide.Right)] // Just inside right 15% zone.
    [InlineData(1632, 10, DockSide.Right)] // Exactly on right threshold.
    [InlineData(1631, 10, DockSide.Top)] // Just outside right zone.
    [InlineData(480, 500, DockSide.Left)] // Outside corner zone, but side is nearer.
    [InlineData(1440, 500, DockSide.Right)] // Symmetric nearest-side fallback.
    public void ChooseNearestDockSide_UsesSymmetricCornerPriorityForBothOrientations(
        int centerOffset, int topOffset, DockSide expected)
    {
        // Use one consistent monitor coordinate system, including a nonzero origin.
        // Scaling all pixel coordinates equally must preserve the decision.
        foreach (var scale in new[] { 1, 2 })
        foreach (var (width, height) in new[] { (80, 48), (48, 80) })
        {
            var workArea = new PixelRect(-2400, 40, -2400 + 1920 * scale, 40 + 1040 * scale);
            var left = workArea.Left + centerOffset * scale - width * scale / 2;
            var top = workArea.Top + topOffset * scale;
            var window = new PixelRect(left, top, left + width * scale, top + height * scale);

            Assert.Equal(expected,
                WindowPlacementCalculator.ChooseNearestDockSide(window, workArea));
        }
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
