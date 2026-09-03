namespace FloatingTools.App.Controls;

public readonly record struct ResponsiveImageSize(double Width, double Height);

public static class ResponsiveImageSizeCalculator
{
    public const double HorizontalPagePadding = 32;

    public static ResponsiveImageSize Calculate(
        double requestedWidth,
        double availableContainerWidth,
        double aspectRatio)
    {
        var safeRequestedWidth = double.IsFinite(requestedWidth) && requestedWidth > 0
            ? requestedWidth
            : 1;
        var safeAspectRatio = double.IsFinite(aspectRatio) && aspectRatio > 0
            ? aspectRatio
            : 1;
        var availableImageWidth = double.IsFinite(availableContainerWidth)
            && availableContainerWidth > HorizontalPagePadding
                ? availableContainerWidth - HorizontalPagePadding
                : safeRequestedWidth;
        var effectiveWidth = Math.Min(safeRequestedWidth, availableImageWidth);
        return new ResponsiveImageSize(
            effectiveWidth,
            effectiveWidth / safeAspectRatio);
    }
}
