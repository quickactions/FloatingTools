using FloatingTools.App.Models;
using System.Windows;

namespace FloatingTools.App.Services;

internal enum ToolStripChevronDirection { Down, Up, Left, Right }

internal readonly record struct ToolStripCell(double X, double Y, double Width, double Height);

internal readonly record struct ToolStripToolCell(
    ToolId Tool, ToolStripCell Cell, CornerRadius Corners);

internal sealed record ToolStripLayout(
    double Width,
    double Height,
    ToolStripCell MainCell,
    ToolStripCell ChevronCell,
    IReadOnlyList<ToolStripToolCell> ToolCells,
    ToolStripChevronDirection ChevronDirection,
    CornerRadius MainCorners,
    CornerRadius ChevronCorners);

internal static class ToolStripLayoutCalculator
{
    internal const double TileSize = 48;
    internal const double ChevronSize = 32;
    internal static ToolStripLayout Calculate(
        DockSide dockSide,
        TopOpeningDirection opening,
        bool expanded,
        int toolCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(toolCount);
        var tools = Enum.GetValues<ToolId>().Take(toolCount).ToArray();
        var extension = expanded ? toolCount * TileSize : 0;

        if (dockSide == DockSide.Top)
        {
            var leftward = opening == TopOpeningDirection.Left;
            var mainX = leftward ? extension : 0;
            var cells = expanded
                ? tools.Select((tool, index) => new ToolStripToolCell(tool,
                    new ToolStripCell(leftward
                            ? extension - TileSize - index * TileSize
                            : TileSize + index * TileSize,
                        0, TileSize, TileSize),
                    index == toolCount - 1
                        ? leftward ? new CornerRadius(0, 0, 0, 12)
                            : new CornerRadius(0, 0, 12, 0)
                        : new CornerRadius(0))).ToArray()
                : [];
            return new ToolStripLayout(TileSize + extension, TileSize + ChevronSize,
                new ToolStripCell(mainX, 0, TileSize, TileSize),
                new ToolStripCell(mainX, TileSize, TileSize, ChevronSize),
                cells,
                expanded ? leftward ? ToolStripChevronDirection.Right : ToolStripChevronDirection.Left
                    : leftward ? ToolStripChevronDirection.Left : ToolStripChevronDirection.Right,
                expanded && toolCount > 0
                    ? leftward ? new CornerRadius(0, 0, 12, 0)
                        : new CornerRadius(0, 0, 0, 12)
                    : new CornerRadius(0, 0, 12, 12),
                new CornerRadius(0));
        }

        var rightDock = dockSide == DockSide.Right;
        var x = rightDock ? ChevronSize : 0;
        var toolCells = expanded
            ? tools.Select((tool, index) => new ToolStripToolCell(tool,
                new ToolStripCell(x, TileSize + index * TileSize,
                    TileSize, TileSize),
                index == toolCount - 1
                    ? rightDock ? new CornerRadius(0, 0, 0, 12)
                        : new CornerRadius(0, 0, 12, 0)
                    : new CornerRadius(0))).ToArray()
            : [];
        return new ToolStripLayout(TileSize + ChevronSize, TileSize + extension,
            new ToolStripCell(x, 0, TileSize, TileSize),
            new ToolStripCell(rightDock ? 0 : TileSize, 0, ChevronSize, TileSize),
            toolCells,
            expanded ? ToolStripChevronDirection.Up : ToolStripChevronDirection.Down,
            expanded && toolCount > 0
                ? rightDock ? new CornerRadius(12, 0, 0, 0)
                    : new CornerRadius(0, 12, 0, 0)
                : rightDock ? new CornerRadius(12, 0, 0, 12)
                    : new CornerRadius(0, 12, 12, 0),
            rightDock ? new CornerRadius(12, 0, 0, 12) : new CornerRadius(0, 12, 12, 0));
    }
}
