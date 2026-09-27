using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public static class PanelWindowPlacementCalculator
{
    public static PanelWindowPlacement Calculate(
        PixelRect toolbarBounds,
        int panelWidthPixels,
        MonitorWorkArea monitor,
        DockSide dockSide,
        int requiredPanelHeightPixels = 0,
        TopOpeningDirection topOpeningDirection = TopOpeningDirection.Right)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(panelWidthPixels);
        ArgumentOutOfRangeException.ThrowIfNegative(requiredPanelHeightPixels);
        ArgumentNullException.ThrowIfNull(monitor);

        var workArea = monitor.WorkArea;
        if (dockSide == DockSide.Top)
        {
            var panelWidth = Math.Min(panelWidthPixels, Math.Max(1, workArea.Width - toolbarBounds.Width));
            var topToolbarLeft = topOpeningDirection == TopOpeningDirection.Right
                ? Math.Clamp(toolbarBounds.Left, workArea.Left,
                    Math.Max(workArea.Left, workArea.Right - toolbarBounds.Width - panelWidth))
                : Math.Clamp(toolbarBounds.Left, workArea.Left + panelWidth,
                    Math.Max(workArea.Left + panelWidth, workArea.Right - toolbarBounds.Width));
            var topPanelLeft = topOpeningDirection == TopOpeningDirection.Right
                ? topToolbarLeft + toolbarBounds.Width
                : topToolbarLeft - panelWidth;
            return new PanelWindowPlacement(
                new PixelPoint(topToolbarLeft, workArea.Top),
                new PixelPoint(topPanelLeft, workArea.Top));
        }

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
        DockSide dockSide,
        TopOpeningDirection topOpeningDirection = TopOpeningDirection.Right)
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
        if (dockSide == DockSide.Top)
        {
            var topHostLeft = topOpeningDirection == TopOpeningDirection.Right
                ? visiblePanelPlacement.PanelPosition.X
                : visiblePanelPlacement.PanelPosition.X + visiblePanelWidthPixels - hostWidthPixels;
            return new PanelHostPlacement(
                new PixelPoint(topHostLeft, workArea.Top),
                new PixelPoint(visiblePanelPlacement.PanelPosition.X - topHostLeft, 0));
        }

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
