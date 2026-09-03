namespace FloatingTools.App.Models;

public readonly record struct PixelPoint(int X, int Y);

public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Math.Max(0, Right - Left);

    public int Height => Math.Max(0, Bottom - Top);

    public long IntersectionArea(PixelRect other)
    {
        var width = Math.Max(0, Math.Min(Right, other.Right) - Math.Max(Left, other.Left));
        var height = Math.Max(0, Math.Min(Bottom, other.Bottom) - Math.Max(Top, other.Top));
        return (long)width * height;
    }
}

public sealed record MonitorWorkArea(
    string MonitorId,
    PixelRect Bounds,
    PixelRect WorkArea,
    double DpiScale,
    bool IsPrimary);

public sealed record ResolvedWindowPlacement(
    WindowPlacement Placement,
    int Left,
    int Top);
