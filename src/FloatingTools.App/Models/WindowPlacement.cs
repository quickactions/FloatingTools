namespace FloatingTools.App.Models;

public sealed record WindowPlacement(
    string MonitorId,
    DockSide DockSide,
    double VerticalOffset)
{
    public double HorizontalOffset { get; init; }
    public TopOpeningDirection TopOpeningDirection { get; init; } = TopOpeningDirection.Right;
}
