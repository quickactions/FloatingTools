using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class DockedLayoutCalculatorTests
{
    [Fact]
    public void RightDock_AlignsPanelAndCubeAtRightEdge()
    {
        var result = DockedLayoutCalculator.Calculate(
            DockSide.Right,
            panelWidth: 300);

        Assert.Equal(300, result.WindowWidth);
        Assert.Equal(252, result.CubeLeft);
        Assert.Equal(220, result.ToolsButtonLeft);
        Assert.Equal(0, result.PanelLeft);
        Assert.Equal(
            result.PanelLeft + 300,
            result.CubeLeft + DockedLayoutCalculator.CubeSize);
    }

    [Fact]
    public void LeftDock_AlignsPanelAndCubeAtLeftEdge()
    {
        var result = DockedLayoutCalculator.Calculate(
            DockSide.Left,
            panelWidth: 300);

        Assert.Equal(0, result.CubeLeft);
        Assert.Equal(48, result.ToolsButtonLeft);
        Assert.Equal(0, result.PanelLeft);
        Assert.Equal(result.PanelLeft, result.CubeLeft);
    }

    [Fact]
    public void PanelAnchor_DoesNotIncludeToolsButtonWidth()
    {
        var right = DockedLayoutCalculator.Calculate(
            DockSide.Right,
            panelWidth: 220);
        var left = DockedLayoutCalculator.Calculate(
            DockSide.Left,
            panelWidth: 220);

        Assert.Equal(
            right.PanelLeft + 220,
            right.CubeLeft + DockedLayoutCalculator.CubeSize);
        Assert.Equal(left.PanelLeft, left.CubeLeft);
        Assert.True(right.ToolsButtonPrecedesCube);
        Assert.False(left.ToolsButtonPrecedesCube);
    }
}
