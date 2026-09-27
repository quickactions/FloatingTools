using FloatingTools.App.Models;
using FloatingTools.App.Services;
using System.Windows;

namespace FloatingTools.Tests.Services;

public sealed class ToolStripLayoutCalculatorTests
{
    private static readonly ToolId[] Tools =
        [ToolId.Translation, ToolId.Notes, ToolId.QuickChat, ToolId.Calendar];

    [Theory]
    [InlineData(DockSide.Left, TopOpeningDirection.Right, false, 80, 48, 0, 0, 48, 0)]
    [InlineData(DockSide.Left, TopOpeningDirection.Right, true, 80, 240, 0, 0, 48, 0)]
    [InlineData(DockSide.Right, TopOpeningDirection.Right, false, 80, 48, 32, 0, 0, 0)]
    [InlineData(DockSide.Right, TopOpeningDirection.Right, true, 80, 240, 32, 0, 0, 0)]
    [InlineData(DockSide.Top, TopOpeningDirection.Right, false, 48, 80, 0, 0, 0, 48)]
    [InlineData(DockSide.Top, TopOpeningDirection.Right, true, 240, 80, 0, 0, 0, 48)]
    [InlineData(DockSide.Top, TopOpeningDirection.Left, false, 48, 80, 0, 0, 0, 48)]
    [InlineData(DockSide.Top, TopOpeningDirection.Left, true, 240, 80, 192, 0, 192, 48)]
    public void AnchorAndToolsHaveIndependentGeometry(
        DockSide dock, TopOpeningDirection opening, bool expanded,
        double width, double height, double mainX, double mainY,
        double chevronX, double chevronY)
    {
        var layout = ToolStripLayoutCalculator.Calculate(dock, opening, expanded, 4);
        Assert.Equal((width, height), (layout.Width, layout.Height));
        Assert.Equal(new ToolStripCell(mainX, mainY, 48, 48), layout.MainCell);
        Assert.Equal(new ToolStripCell(chevronX, chevronY,
            dock == DockSide.Top ? 48 : 32, dock == DockSide.Top ? 32 : 48),
            layout.ChevronCell);
        Assert.Equal(dock switch
        {
            DockSide.Left => expanded ? new CornerRadius(0, 12, 0, 0)
                : new CornerRadius(0, 12, 12, 0),
            DockSide.Right => expanded ? new CornerRadius(12, 0, 0, 0)
                : new CornerRadius(12, 0, 0, 12),
            _ => !expanded ? new CornerRadius(0, 0, 12, 12)
                : opening == TopOpeningDirection.Left
                    ? new CornerRadius(0, 0, 12, 0)
                    : new CornerRadius(0, 0, 0, 12)
        }, layout.MainCorners);
        Assert.Equal(expanded ? 4 : 0, layout.ToolCells.Count);
        if (!expanded) return;

        Assert.Equal(Tools, layout.ToolCells.Select(cell => cell.Tool));
        for (var index = 0; index < layout.ToolCells.Count; index++)
        {
            var cell = layout.ToolCells[index].Cell;
            Assert.Equal((48d, 48d), (cell.Width, cell.Height));
            Assert.False(Overlaps(cell, layout.ChevronCell));
            if (dock == DockSide.Top)
            {
                Assert.Equal(layout.MainCell.Y, cell.Y);
                Assert.Equal(opening == TopOpeningDirection.Left
                    ? 144 - index * 48 : 48 + index * 48, cell.X);
            }
            else
            {
                Assert.Equal(layout.MainCell.X, cell.X);
                Assert.Equal(48 + index * 48, cell.Y);
            }
            Assert.Equal(index == layout.ToolCells.Count - 1
                ? dock switch
                {
                    DockSide.Left => new CornerRadius(0, 0, 12, 0),
                    DockSide.Right => new CornerRadius(0, 0, 0, 12),
                    _ => opening == TopOpeningDirection.Left
                        ? new CornerRadius(0, 0, 0, 12)
                        : new CornerRadius(0, 0, 12, 0)
                }
                : new CornerRadius(0), layout.ToolCells[index].Corners);
        }
    }

    [Theory]
    [InlineData(DockSide.Left, TopOpeningDirection.Right)]
    [InlineData(DockSide.Right, TopOpeningDirection.Right)]
    [InlineData(DockSide.Top, TopOpeningDirection.Right)]
    [InlineData(DockSide.Top, TopOpeningDirection.Left)]
    public void ChevronNeverMovesRelativeToMain(DockSide dock, TopOpeningDirection opening)
    {
        var closed = ToolStripLayoutCalculator.Calculate(dock, opening, false, 4);
        var expanded = ToolStripLayoutCalculator.Calculate(dock, opening, true, 4);
        Assert.Equal(closed.ChevronCell.X - closed.MainCell.X,
            expanded.ChevronCell.X - expanded.MainCell.X);
        Assert.Equal(closed.ChevronCell.Y - closed.MainCell.Y,
            expanded.ChevronCell.Y - expanded.MainCell.Y);
        Assert.NotEqual(closed.MainCorners, expanded.MainCorners);
    }

    private static bool Overlaps(ToolStripCell a, ToolStripCell b) =>
        a.X < b.X + b.Width && a.X + a.Width > b.X &&
        a.Y < b.Y + b.Height && a.Y + a.Height > b.Y;
}
