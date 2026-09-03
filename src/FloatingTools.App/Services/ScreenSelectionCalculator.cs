using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public static class ScreenSelectionCalculator
{
    public const int MinimumSelectionSizePixels = 4;

    public static PixelRect? Create(
        PixelPoint start,
        PixelPoint end,
        PixelRect bounds)
    {
        var left = Math.Clamp(Math.Min(start.X, end.X), bounds.Left, bounds.Right);
        var top = Math.Clamp(Math.Min(start.Y, end.Y), bounds.Top, bounds.Bottom);
        var right = Math.Clamp(Math.Max(start.X, end.X), bounds.Left, bounds.Right);
        var bottom = Math.Clamp(Math.Max(start.Y, end.Y), bounds.Top, bounds.Bottom);

        if (right - left < MinimumSelectionSizePixels
            || bottom - top < MinimumSelectionSizePixels)
        {
            return null;
        }

        return new PixelRect(left, top, right, bottom);
    }
}
