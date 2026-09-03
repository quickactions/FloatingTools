namespace FloatingTools.App.Controls;

public sealed class NotesImageResizeState
{
    private bool _isUpdating;
    private bool _isCompleted;

    public NotesImageResizeState(
        double originalWidth,
        double minimumWidth,
        double maximumWidth)
    {
        MinimumWidth = NormalizePositive(minimumWidth, 1);
        MaximumWidth = Math.Max(
            MinimumWidth,
            NormalizePositive(maximumWidth, MinimumWidth));
        OriginalWidth = Math.Clamp(
            NormalizePositive(originalWidth, MinimumWidth),
            MinimumWidth,
            MaximumWidth);
        ProposedWidth = OriginalWidth;
    }

    public double OriginalWidth { get; }

    public double MinimumWidth { get; }

    public double MaximumWidth { get; }

    public double ProposedWidth { get; private set; }

    public bool TryUpdate(
        double horizontalChange,
        double verticalChange,
        double aspectRatio,
        Action<double> applyPreview)
    {
        ArgumentNullException.ThrowIfNull(applyPreview);
        if (_isUpdating || _isCompleted)
        {
            return false;
        }

        _isUpdating = true;
        try
        {
            var horizontalWidthChange = NormalizeFinite(horizontalChange) * 2;
            var verticalWidthChange = NormalizeFinite(verticalChange)
                * NormalizePositive(aspectRatio, 1);
            var widthDelta = Math.Abs(verticalWidthChange) > Math.Abs(horizontalWidthChange)
                ? verticalWidthChange
                : horizontalWidthChange;
            var proposed = Math.Clamp(
                OriginalWidth + widthDelta,
                MinimumWidth,
                MaximumWidth);
            if (!double.IsFinite(proposed)
                || Math.Abs(proposed - ProposedWidth) < 0.01)
            {
                return false;
            }

            ProposedWidth = proposed;
            applyPreview(proposed);
            return true;
        }
        finally
        {
            _isUpdating = false;
        }
    }

    public bool TryComplete(out double finalWidth)
    {
        finalWidth = ProposedWidth;
        if (_isCompleted)
        {
            return false;
        }

        _isCompleted = true;
        return true;
    }

    private static double NormalizeFinite(double value) =>
        double.IsFinite(value) ? value : 0;

    private static double NormalizePositive(double value, double fallback) =>
        double.IsFinite(value) && value > 0 ? value : fallback;
}
