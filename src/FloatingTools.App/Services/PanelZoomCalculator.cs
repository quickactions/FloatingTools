using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public static class PanelZoomCalculator
{
    public const double MinimumPercentage = 80;
    public const double DefaultPercentage = 100;
    public const double StandardMaximumPercentage = 140;
    public const double LargeMaximumPercentage = 100;
    public const double MaximumPercentage = StandardMaximumPercentage;
    public const double StepPercentage = 10;
    internal const double PercentageStepTolerance = 1e-9;
    public const double ActiveToolHeaderHeight = 42;

    public static double GetMaximumPercentage(PanelSizePreset preset) =>
        PanelSizeCalculator.NormalizePreset(preset) == PanelSizePreset.Large
            ? LargeMaximumPercentage
            : StandardMaximumPercentage;

    public static double NormalizePercentage(double percentage) =>
        NormalizePercentage(PanelSizePreset.Standard, percentage);

    public static double NormalizePercentage(
        PanelSizePreset preset,
        double percentage)
    {
        if (!double.IsFinite(percentage))
        {
            return DefaultPercentage;
        }

        var clamped = Math.Clamp(
            percentage,
            MinimumPercentage,
            GetMaximumPercentage(preset));
        var step = Math.Round(
            (clamped - MinimumPercentage) / StepPercentage,
            MidpointRounding.AwayFromZero);
        return Math.Min(
            GetMaximumPercentage(preset),
            MinimumPercentage + (step * StepPercentage));
    }

    public static double Increase(double percentage) =>
        Increase(PanelSizePreset.Standard, percentage);

    public static double Increase(PanelSizePreset preset, double percentage) =>
        NormalizePercentage(
            preset,
            NormalizePercentage(preset, percentage) + StepPercentage);

    public static double Decrease(double percentage) =>
        Decrease(PanelSizePreset.Standard, percentage);

    public static double Decrease(PanelSizePreset preset, double percentage) =>
        NormalizePercentage(
            preset,
            NormalizePercentage(preset, percentage) - StepPercentage);

    public static double DecreaseFromEffective(
        PanelSizePreset preset,
        double effectivePercentage)
    {
        if (!double.IsFinite(effectivePercentage)
            || effectivePercentage <= MinimumPercentage)
        {
            return MinimumPercentage;
        }

        // Treat floating-point noise at a step boundary as the exact step.
        var step = Math.Ceiling(
            (effectivePercentage - MinimumPercentage - PercentageStepTolerance)
            / StepPercentage) - 1;
        return NormalizePercentage(
            preset,
            MinimumPercentage + (Math.Max(0, step) * StepPercentage));
    }

    public static int ToPhysicalPixels(double deviceIndependentSize, double dpiScale)
    {
        if (!double.IsFinite(deviceIndependentSize)
            || deviceIndependentSize <= 0
            || !double.IsFinite(dpiScale)
            || dpiScale <= 0)
        {
            return 1;
        }

        return Math.Max(
            1,
            (int)Math.Min(
                int.MaxValue,
                Math.Ceiling(deviceIndependentSize * dpiScale)));
    }

    public static PanelZoomLayout CalculateLayout(
        PanelSizePreset preset,
        double requestedPercentage,
        double availableWidthDip,
        double availableHeightDip,
        DockSide dockSide = DockSide.Right)
    {
        var normalizedPreset = PanelSizeCalculator.NormalizePreset(preset);
        var availableWidth = NormalizeAvailable(availableWidthDip);
        var availableHeight = NormalizeAvailable(availableHeightDip);
        var baseline = PanelSizeCalculator.GetActiveToolSize(
            normalizedPreset,
            availableWidth,
            availableHeight,
            dockSide);
        if (dockSide == DockSide.Top)
        {
            // Keep one logical width and cap the effective scale on narrow monitors.
            // This uses the available width before shrinking below the preferred
            // 320-DIP visible panel width when that much space exists.
            baseline = baseline with
            {
                Width = PanelSizeCalculator.GetRequestedActiveToolSize(
                    normalizedPreset, dockSide).Width
            };
        }
        return CalculateLayout(
            normalizedPreset,
            baseline,
            requestedPercentage,
            availableWidth,
            availableHeight);
    }

    public static PanelFixedHostZoomLayout CalculateFixedHostLayout(
        PanelSizePreset preset,
        double requestedPercentage,
        double availableWidthDip,
        double availableHeightDip,
        DockSide dockSide = DockSide.Right)
    {
        var normalizedPreset = PanelSizeCalculator.NormalizePreset(preset);
        var availableWidth = NormalizeAvailable(availableWidthDip);
        var availableHeight = NormalizeAvailable(availableHeightDip);
        var baseline = PanelSizeCalculator.GetActiveToolSize(
            normalizedPreset,
            availableWidth,
            availableHeight,
            dockSide);
        if (dockSide == DockSide.Top)
        {
            // Keep one logical width and cap the effective scale on narrow monitors.
            // This uses the available width before shrinking below the preferred
            // 320-DIP visible panel width when that much space exists.
            baseline = baseline with
            {
                Width = PanelSizeCalculator.GetRequestedActiveToolSize(
                    normalizedPreset, dockSide).Width
            };
        }

        return new PanelFixedHostZoomLayout(
            CalculateLayout(
                normalizedPreset,
                baseline,
                requestedPercentage,
                availableWidth,
                availableHeight),
            CalculateLayout(
                normalizedPreset,
                baseline,
                GetMaximumPercentage(normalizedPreset),
                availableWidth,
                availableHeight));
    }

    private static PanelZoomLayout CalculateLayout(
        PanelSizePreset preset,
        ToolSize baseline,
        double requestedPercentage,
        double availableWidth,
        double availableHeight)
    {
        var requestedScale = NormalizePercentage(preset, requestedPercentage) / 100d;
        var widthScaleLimit = baseline.Width > 0
            ? availableWidth / baseline.Width
            : requestedScale;
        var baselineBodyHeight = Math.Max(
            0,
            baseline.Height - ActiveToolHeaderHeight);
        var availableBodyHeight = Math.Max(
            0,
            availableHeight - Math.Min(
                ActiveToolHeaderHeight,
                baseline.Height));
        var heightScaleLimit = baselineBodyHeight > 0
            ? availableBodyHeight / baselineBodyHeight
            : requestedScale;
        var effectiveScale = Math.Max(
            0,
            Math.Min(
                requestedScale,
                Math.Min(widthScaleLimit, heightScaleLimit)));

        var width = Math.Min(availableWidth, baseline.Width * effectiveScale);
        var height = baseline.Height <= ActiveToolHeaderHeight
            ? baseline.Height
            : Math.Min(
                availableHeight,
                ActiveToolHeaderHeight + (baselineBodyHeight * effectiveScale));

        return new PanelZoomLayout(
            baseline,
            new ToolSize(width, height),
            effectiveScale * 100d);
    }

    private static double NormalizeAvailable(double value) =>
        double.IsFinite(value) && value > 0 ? value : 0;
}

public sealed record PanelZoomLayout(
    ToolSize LogicalSize,
    ToolSize WindowSize,
    double EffectivePercentage);

public sealed record PanelFixedHostZoomLayout(
    PanelZoomLayout VisibleLayout,
    PanelZoomLayout HostLayout);
