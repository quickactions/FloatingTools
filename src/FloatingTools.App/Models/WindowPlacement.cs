namespace FloatingTools.App.Models;

public sealed record WindowPlacement(
    string MonitorId,
    DockSide DockSide,
    double VerticalOffset);
