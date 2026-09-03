using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public static class PanelWindowPlacementCalculator
{
    public static PanelWindowPlacement Calculate(
        PixelRect toolbarBounds,
        int panelWidthPixels,
        MonitorWorkArea monitor,
        DockSide dockSide,
        int requiredPanelHeightPixels = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(panelWidthPixels);
        ArgumentOutOfRangeException.ThrowIfNegative(requiredPanelHeightPixels);
        ArgumentNullException.ThrowIfNull(monitor);

        var workArea = monitor.WorkArea;
        var toolbarLeft = dockSide == DockSide.Left
            ? workArea.Left
            : Math.Max(workArea.Left, workArea.Right - toolbarBounds.Width);
        var panelLeft = dockSide == DockSide.Left
            ? workArea.Left
            : Math.Max(workArea.Left, workArea.Right - panelWidthPixels);
        var maximumPossiblePanelHeight = Math.Max(
            0,
            workArea.Height - toolbarBounds.Height);
        var reservedPanelHeight = Math.Min(
            requiredPanelHeightPixels,
            maximumPossiblePanelHeight);
        var maximumToolbarTop = Math.Max(
            workArea.Top,
            workArea.Bottom - toolbarBounds.Height - reservedPanelHeight);
        var toolbarTop = Math.Clamp(
            toolbarBounds.Top,
            workArea.Top,
            maximumToolbarTop);

        return new PanelWindowPlacement(
            new PixelPoint(toolbarLeft, toolbarTop),
            new PixelPoint(panelLeft, toolbarTop + toolbarBounds.Height));
    }
}
