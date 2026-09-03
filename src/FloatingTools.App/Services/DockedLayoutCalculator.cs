using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public static class DockedLayoutCalculator
{
    public const double CubeSize = 48;
    public const double ToolsButtonWidth = 32;

    public static DockedHorizontalLayout Calculate(
        DockSide dockSide,
        double panelWidth)
    {
        var toolbarWidth = CubeSize + ToolsButtonWidth;
        var normalizedPanelWidth = double.IsFinite(panelWidth) && panelWidth > 0
            ? panelWidth
            : toolbarWidth;
        var windowWidth = Math.Max(toolbarWidth, normalizedPanelWidth);

        if (dockSide == DockSide.Left)
        {
            return new DockedHorizontalLayout(
                windowWidth,
                CubeLeft: 0,
                ToolsButtonLeft: CubeSize,
                PanelLeft: 0,
                ToolsButtonPrecedesCube: false);
        }

        var cubeLeft = windowWidth - CubeSize;
        return new DockedHorizontalLayout(
            windowWidth,
            cubeLeft,
            ToolsButtonLeft: cubeLeft - ToolsButtonWidth,
            PanelLeft: windowWidth - normalizedPanelWidth,
            ToolsButtonPrecedesCube: true);
    }
}
