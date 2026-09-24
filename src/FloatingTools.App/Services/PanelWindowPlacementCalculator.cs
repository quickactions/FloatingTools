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

    public static PanelHostPlacement CalculateHostPlacement(
        PanelWindowPlacement visiblePanelPlacement,
        int visiblePanelWidthPixels,
        int visiblePanelHeightPixels,
        int hostWidthPixels,
        int hostHeightPixels,
        MonitorWorkArea monitor,
        DockSide dockSide)
    {
        ArgumentNullException.ThrowIfNull(visiblePanelPlacement);
        ArgumentNullException.ThrowIfNull(monitor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(visiblePanelWidthPixels);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(visiblePanelHeightPixels);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hostWidthPixels);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hostHeightPixels);

        if (hostWidthPixels < visiblePanelWidthPixels
            || hostHeightPixels < visiblePanelHeightPixels)
        {
            throw new ArgumentException(
                "The transparent host must contain the visible panel.");
        }

        var workArea = monitor.WorkArea;
        var hostLeft = dockSide == DockSide.Left
            ? workArea.Left
            : Math.Max(workArea.Left, workArea.Right - hostWidthPixels);
        var maximumHostTop = Math.Max(
            workArea.Top,
            workArea.Bottom - hostHeightPixels);
        var hostTop = Math.Clamp(
            visiblePanelPlacement.PanelPosition.Y,
            workArea.Top,
            maximumHostTop);

        return new PanelHostPlacement(
            new PixelPoint(hostLeft, hostTop),
            new PixelPoint(
                visiblePanelPlacement.PanelPosition.X - hostLeft,
                visiblePanelPlacement.PanelPosition.Y - hostTop));
    }
}

public sealed record PanelHostPlacement(
    PixelPoint HostPosition,
    PixelPoint VisiblePanelOffset);
