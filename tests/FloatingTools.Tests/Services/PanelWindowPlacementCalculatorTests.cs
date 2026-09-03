using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class PanelWindowPlacementCalculatorTests
{
    private static readonly MonitorWorkArea Monitor = new(
        @"\\.\DISPLAY1",
        new PixelRect(0, 0, 1920, 1080),
        new PixelRect(0, 0, 1920, 1040),
        1,
        true);

    [Fact]
    public void Calculate_LeftDock_AlignsBothWindowsToLeftEdge()
    {
        var result = PanelWindowPlacementCalculator.Calculate(
            new PixelRect(0, 100, 80, 148),
            panelWidthPixels: 300,
            Monitor,
            DockSide.Left);

        Assert.Equal(new PixelPoint(0, 100), result.ToolbarPosition);
        Assert.Equal(new PixelPoint(0, 148), result.PanelPosition);
    }

    [Fact]
    public void Calculate_RightDock_AlignsBothWindowsToRightEdge()
    {
        var result = PanelWindowPlacementCalculator.Calculate(
            new PixelRect(1840, 100, 1920, 148),
            panelWidthPixels: 300,
            Monitor,
            DockSide.Right);

        Assert.Equal(new PixelPoint(1840, 100), result.ToolbarPosition);
        Assert.Equal(new PixelPoint(1620, 148), result.PanelPosition);
    }

    [Fact]
    public void Calculate_NearBottom_KeepsPanelAttachedBelowToolbar()
    {
        var result = PanelWindowPlacementCalculator.Calculate(
            new PixelRect(1840, 700, 1920, 748),
            panelWidthPixels: 300,
            Monitor,
            DockSide.Right);

        Assert.Equal(new PixelPoint(1840, 700), result.ToolbarPosition);
        Assert.Equal(new PixelPoint(1620, 748), result.PanelPosition);
    }

    [Fact]
    public void Calculate_ToolMenu_DoesNotReserveActiveToolHeight()
    {
        var result = PanelWindowPlacementCalculator.Calculate(
            new PixelRect(1840, 1030, 1920, 1078),
            panelWidthPixels: 300,
            Monitor,
            DockSide.Right,
            requiredPanelHeightPixels: 0);

        Assert.Equal(new PixelPoint(1840, 992), result.ToolbarPosition);
        Assert.Equal(new PixelPoint(1620, 1040), result.PanelPosition);
    }

    [Fact]
    public void Calculate_ActiveToolWithEnoughSpace_KeepsToolbarPosition()
    {
        var result = PanelWindowPlacementCalculator.Calculate(
            new PixelRect(1840, 100, 1920, 148),
            panelWidthPixels: 300,
            Monitor,
            DockSide.Right,
            requiredPanelHeightPixels: 320);

        Assert.Equal(100, result.ToolbarPosition.Y);
        Assert.Equal(148, result.PanelPosition.Y);
    }

    [Theory]
    [InlineData(DockSide.Left, 0, 0)]
    [InlineData(DockSide.Right, 1840, 1620)]
    public void Calculate_ActiveToolTooLow_MovesToolbarToMaximumLegalTop(
        DockSide dockSide,
        int expectedToolbarLeft,
        int expectedPanelLeft)
    {
        var result = PanelWindowPlacementCalculator.Calculate(
            new PixelRect(1840, 800, 1920, 848),
            panelWidthPixels: 300,
            Monitor,
            dockSide,
            requiredPanelHeightPixels: 320);

        Assert.Equal(new PixelPoint(expectedToolbarLeft, 672), result.ToolbarPosition);
        Assert.Equal(new PixelPoint(expectedPanelLeft, 720), result.PanelPosition);
        Assert.Equal(320, Monitor.WorkArea.Bottom - result.PanelPosition.Y);
    }

    [Fact]
    public void Calculate_OpeningStandardReservesItsFullHeight()
    {
        var result = PanelWindowPlacementCalculator.Calculate(
            new PixelRect(1840, 800, 1920, 848),
            panelWidthPixels: 300,
            Monitor,
            DockSide.Right,
            requiredPanelHeightPixels: 500);

        Assert.Equal(492, result.ToolbarPosition.Y);
        Assert.Equal(540, result.PanelPosition.Y);
        Assert.Equal(500, Monitor.WorkArea.Bottom - result.PanelPosition.Y);
    }

    [Fact]
    public void Calculate_SwitchingToLargeReservesItsFullHeight()
    {
        var result = PanelWindowPlacementCalculator.Calculate(
            new PixelRect(1840, 492, 1920, 540),
            panelWidthPixels: 560,
            Monitor,
            DockSide.Right,
            requiredPanelHeightPixels: 760);

        Assert.Equal(232, result.ToolbarPosition.Y);
        Assert.Equal(280, result.PanelPosition.Y);
        Assert.Equal(760, Monitor.WorkArea.Bottom - result.PanelPosition.Y);
    }
}
