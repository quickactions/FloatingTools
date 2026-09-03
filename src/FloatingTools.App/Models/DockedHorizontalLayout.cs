namespace FloatingTools.App.Models;

public sealed record DockedHorizontalLayout(
    double WindowWidth,
    double CubeLeft,
    double ToolsButtonLeft,
    double PanelLeft,
    bool ToolsButtonPrecedesCube);
