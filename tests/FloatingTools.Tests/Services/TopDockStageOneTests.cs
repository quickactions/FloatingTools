using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class TopDockStageOneTests
{
    private static readonly MonitorWorkArea Monitor = new(
        "DISPLAY1", new PixelRect(0, 0, 1920, 1080),
        new PixelRect(0, 40, 1920, 1040), 1, true);

    [Theory]
    [InlineData(700, 40, DockSide.Top)]
    [InlineData(0, 40, DockSide.Left)]
    [InlineData(1840, 40, DockSide.Right)]
    [InlineData(20, 60, DockSide.Left)]
    public void NearestEdge_PrefersSidesOnTies(int left, int top, DockSide expected)
    {
        var window = new PixelRect(left, top, left + 80, top + 48);
        Assert.Equal(expected, WindowPlacementCalculator.ChooseNearestDockSide(window, Monitor.WorkArea));
    }

    [Theory]
    [InlineData(900, TopOpeningDirection.Left)]
    [InlineData(920, TopOpeningDirection.Right)]
    [InlineData(1400, TopOpeningDirection.Right)]
    public void OpeningDirection_UsesRawToolbarCenter(int left, TopOpeningDirection expected)
    {
        var window = new PixelRect(left, 45, left + 80, 93);
        Assert.Equal(expected, WindowPlacementCalculator.ChooseTopOpeningDirection(window, Monitor.WorkArea));
    }

    [Theory]
    [InlineData(TopOpeningDirection.Right, 1800, 1412, 1460)]
    [InlineData(TopOpeningDirection.Left, 20, 460, 0)]
    public void ConnectedTopPair_ClampsWithoutReversingDirection(
        TopOpeningDirection opening, int rawLeft, int toolbarLeft, int panelLeft)
    {
        var raw = new PixelRect(rawLeft, 80, rawLeft + 48, 160);
        var result = PanelWindowPlacementCalculator.Calculate(
            raw, 460, Monitor, DockSide.Top, 300, opening);
        Assert.Equal(new PixelPoint(toolbarLeft, 40), result.ToolbarPosition);
        Assert.Equal(new PixelPoint(panelLeft, 40), result.PanelPosition);
        Assert.True(result.PanelPosition.X >= Monitor.WorkArea.Left);
        Assert.True(result.PanelPosition.X + 460 <= Monitor.WorkArea.Right);
    }

    [Theory]
    [InlineData(TopOpeningDirection.Right, 600, 648, 648, 0)]
    [InlineData(TopOpeningDirection.Left, 600, 140, -44, 184)]
    public void FixedHost_AnchorsVisibleEdge(
        TopOpeningDirection opening, int toolbarLeft, int panelLeft,
        int expectedHostLeft, int expectedOffset)
    {
        var visible = PanelWindowPlacementCalculator.Calculate(
            new PixelRect(toolbarLeft, 40, toolbarLeft + 48, 120),
            460, Monitor, DockSide.Top, 300, opening);
        Assert.Equal(panelLeft, visible.PanelPosition.X);
        var host = PanelWindowPlacementCalculator.CalculateHostPlacement(
            visible, 460, 300, 644, 420, Monitor, DockSide.Top, opening);
        Assert.Equal(expectedHostLeft, host.HostPosition.X);
        Assert.Equal(expectedOffset, host.VisiblePanelOffset.X);
        Assert.Equal(40, host.HostPosition.Y);
    }

    [Fact]
    public void TopSizes_AndZoomLimits_KeepFixedHostAtPresetMaximum()
    {
        Assert.Equal(new ToolSize(460, 300),
            PanelSizeCalculator.GetRequestedActiveToolSize(PanelSizePreset.Standard, DockSide.Top));
        Assert.Equal(new ToolSize(720, 560),
            PanelSizeCalculator.GetRequestedActiveToolSize(PanelSizePreset.Large, DockSide.Top));
        Assert.Equal(new ToolSize(300, 500),
            PanelSizeCalculator.GetRequestedActiveToolSize(PanelSizePreset.Standard));
        Assert.Equal(new ToolSize(560, 760),
            PanelSizeCalculator.GetRequestedActiveToolSize(PanelSizePreset.Large));

        var small = PanelZoomCalculator.CalculateFixedHostLayout(
            PanelSizePreset.Standard, 80, 1800, 900, DockSide.Top);
        var large = PanelZoomCalculator.CalculateFixedHostLayout(
            PanelSizePreset.Standard, 100, 1800, 900, DockSide.Top);
        Assert.Equal(368, small.VisibleLayout.WindowSize.Width);
        Assert.Equal(460, large.VisibleLayout.WindowSize.Width);
        Assert.Equal(small.HostLayout.WindowSize, large.HostLayout.WindowSize);
        Assert.Equal(644, small.HostLayout.WindowSize.Width);
        Assert.Equal(140, small.HostLayout.EffectivePercentage, 6);

        var largePreset = PanelZoomCalculator.CalculateFixedHostLayout(
            PanelSizePreset.Large, 80, 1800, 900, DockSide.Top);
        Assert.Equal(720, largePreset.HostLayout.WindowSize.Width);
        Assert.Equal(100, largePreset.HostLayout.EffectivePercentage, 6);
    }

    [Fact]
    public void VeryNarrowMonitor_ConstrainsVisibleWidthAndRetainsDirection()
    {
        var narrow = Monitor with { WorkArea = new PixelRect(0, 40, 300, 600) };
        var zoom = PanelZoomCalculator.CalculateFixedHostLayout(
            PanelSizePreset.Standard, 140, (narrow.WorkArea.Width - 48), 560, DockSide.Top);
        Assert.True(zoom.VisibleLayout.WindowSize.Width <= 252);
        var visible = PanelWindowPlacementCalculator.Calculate(
            new PixelRect(250, 40, 298, 120), 252, narrow, DockSide.Top, 300,
            TopOpeningDirection.Right);
        Assert.Equal(0, visible.ToolbarPosition.X);
        Assert.Equal(48, visible.PanelPosition.X);
        Assert.Equal(300, visible.PanelPosition.X + 252);
    }

    [Fact]
    public void TopToolMenu_RemainsConnectedAndWithinWorkArea()
    {
        var narrow = Monitor with { WorkArea = new PixelRect(0, 40, 400, 600) };
        var menuWidth = (int)PanelSizeCalculator.ToolMenuWidth;
        var toolbar = new PixelRect(350, 40, 398, 120);
        var visible = PanelWindowPlacementCalculator.Calculate(
            toolbar, menuWidth, narrow, DockSide.Top, 118, TopOpeningDirection.Right);

        Assert.Equal(132, visible.ToolbarPosition.X);
        Assert.Equal(180, visible.PanelPosition.X);
        Assert.Equal(400, visible.PanelPosition.X + menuWidth);
        Assert.Equal(narrow.WorkArea.Top, visible.PanelPosition.Y);
        Assert.True(visible.PanelPosition.Y + 118 <= narrow.WorkArea.Bottom);
    }

    [Fact]
    public void NarrowButUsableMonitor_KeepsPreferredHorizontalPanelWidth()
    {
        var layout = PanelZoomCalculator.CalculateFixedHostLayout(
            PanelSizePreset.Standard, 80, 350, 900, DockSide.Top);

        Assert.Equal(350, layout.VisibleLayout.WindowSize.Width);
        Assert.True(layout.VisibleLayout.WindowSize.Width >= PanelSizeCalculator.TopPreferredMinimumWidth);
        Assert.Equal(350, layout.HostLayout.WindowSize.Width);
        Assert.Equal(350d / PanelSizeCalculator.TopStandardWidth * 100, layout.VisibleLayout.EffectivePercentage, 6);
    }

    [Fact]
    public void RestoreTopPlacement_PreservesOffsetDirectionAndDpi()
    {
        var scaled = Monitor with { DpiScale = 1.25 };
        var placement = new WindowPlacement("DISPLAY1", DockSide.Top, 99)
        {
            HorizontalOffset = 120,
            TopOpeningDirection = TopOpeningDirection.Left
        };
        var restored = WindowPlacementCalculator.Resolve(placement, [scaled], 60, 100);
        Assert.Equal(150, restored.Left);
        Assert.Equal(40, restored.Top);
        Assert.Equal(TopOpeningDirection.Left, restored.Placement.TopOpeningDirection);
        Assert.Equal(120, restored.Placement.HorizontalOffset);
        Assert.Equal(0, restored.Placement.VerticalOffset);
    }
}