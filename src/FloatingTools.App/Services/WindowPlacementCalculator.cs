using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public static class WindowPlacementCalculator
{
    public static DockSide ChooseNearestDockSide(PixelRect window, PixelRect workArea)
    {
        var distanceFromLeft = Math.Abs((long)window.Left - workArea.Left);
        var distanceFromRight = Math.Abs((long)workArea.Right - window.Right);
        return distanceFromLeft <= distanceFromRight ? DockSide.Left : DockSide.Right;
    }

    public static double ClampVerticalOffset(
        double verticalOffsetDip,
        int windowHeightPixels,
        MonitorWorkArea monitor)
    {
        var scale = NormalizeScale(monitor.DpiScale);
        var maximumOffset = Math.Max(0, (monitor.WorkArea.Height - windowHeightPixels) / scale);
        var requestedOffset = double.IsFinite(verticalOffsetDip) ? verticalOffsetDip : 0;
        return Math.Clamp(requestedOffset, 0, maximumOffset);
    }

    public static MonitorWorkArea SelectMonitor(
        PixelRect window,
        IReadOnlyList<MonitorWorkArea> monitors)
    {
        ArgumentOutOfRangeException.ThrowIfZero(monitors.Count);

        var intersectingMonitor = monitors
            .Select(monitor => new
            {
                Monitor = monitor,
                Area = window.IntersectionArea(monitor.Bounds)
            })
            .OrderByDescending(candidate => candidate.Area)
            .First();

        if (intersectingMonitor.Area > 0)
        {
            return intersectingMonitor.Monitor;
        }

        var windowCenterX = window.Left + (window.Width / 2.0);
        var windowCenterY = window.Top + (window.Height / 2.0);

        return monitors
            .OrderBy(monitor =>
            {
                var centerX = monitor.Bounds.Left + (monitor.Bounds.Width / 2.0);
                var centerY = monitor.Bounds.Top + (monitor.Bounds.Height / 2.0);
                var deltaX = centerX - windowCenterX;
                var deltaY = centerY - windowCenterY;
                return (deltaX * deltaX) + (deltaY * deltaY);
            })
            .First();
    }

    public static ResolvedWindowPlacement Resolve(
        WindowPlacement? requestedPlacement,
        IReadOnlyList<MonitorWorkArea> monitors,
        int windowWidthPixels,
        int windowHeightPixels)
    {
        ArgumentOutOfRangeException.ThrowIfZero(monitors.Count);

        var savedMonitor = requestedPlacement is null
            ? null
            : monitors.FirstOrDefault(monitor =>
                string.Equals(
                    monitor.MonitorId,
                    requestedPlacement.MonitorId,
                    StringComparison.OrdinalIgnoreCase));

        var monitor = savedMonitor
            ?? monitors.FirstOrDefault(candidate => candidate.IsPrimary)
            ?? monitors[0];

        var dockSide = savedMonitor is null
            ? DockSide.Right
            : requestedPlacement!.DockSide;

        var scale = NormalizeScale(monitor.DpiScale);
        var defaultOffset = Math.Max(
            0,
            (monitor.WorkArea.Height - windowHeightPixels) / scale / 2);
        var requestedOffset = savedMonitor is null
            ? defaultOffset
            : requestedPlacement!.VerticalOffset;
        var offset = ClampVerticalOffset(requestedOffset, windowHeightPixels, monitor);

        var left = dockSide == DockSide.Left
            ? monitor.WorkArea.Left
            : monitor.WorkArea.Right - windowWidthPixels;
        var top = monitor.WorkArea.Top + (int)Math.Round(
            offset * scale,
            MidpointRounding.AwayFromZero);

        var normalizedPlacement = new WindowPlacement(monitor.MonitorId, dockSide, offset);
        return new ResolvedWindowPlacement(normalizedPlacement, left, top);
    }

    private static double NormalizeScale(double scale) =>
        double.IsFinite(scale) && scale > 0 ? scale : 1;
}
