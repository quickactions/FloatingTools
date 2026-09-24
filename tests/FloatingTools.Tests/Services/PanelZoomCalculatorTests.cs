using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class PanelZoomCalculatorTests
{
    [Theory]
    [InlineData(double.NaN, 100)]
    [InlineData(double.PositiveInfinity, 100)]
    [InlineData(double.NegativeInfinity, 100)]
    [InlineData(20, 80)]
    [InlineData(84, 80)]
    [InlineData(85, 90)]
    [InlineData(116, 120)]
    [InlineData(500, 140)]
    public void NormalizePercentage_UsesFiniteSupportedTenPointSteps(
        double input,
        double expected)
    {
        Assert.Equal(expected, PanelZoomCalculator.NormalizePercentage(input));
    }

    [Fact]
    public void IncreaseAndDecrease_StopAtApprovedLimits()
    {
        Assert.Equal(140, PanelZoomCalculator.Increase(140));
        Assert.Equal(110, PanelZoomCalculator.Increase(100));
        Assert.Equal(90, PanelZoomCalculator.Decrease(100));
        Assert.Equal(80, PanelZoomCalculator.Decrease(80));
    }


    [Theory]
    [InlineData(PanelSizePreset.Standard, 80, 80)]
    [InlineData(PanelSizePreset.Standard, 90, 80)]
    [InlineData(PanelSizePreset.Standard, 100, 90)]
    [InlineData(PanelSizePreset.Standard, 110, 100)]
    [InlineData(PanelSizePreset.Standard, 120, 110)]
    [InlineData(PanelSizePreset.Standard, 130, 120)]
    [InlineData(PanelSizePreset.Standard, 140, 130)]
    [InlineData(PanelSizePreset.Large, 80, 80)]
    [InlineData(PanelSizePreset.Large, 90, 80)]
    [InlineData(PanelSizePreset.Large, 100, 90)]
    public void DecreaseFromEffective_UsesTheNextLowerCalculatedLayoutStep(
        PanelSizePreset preset,
        double requested,
        double expected)
    {
        var layout = PanelZoomCalculator.CalculateLayout(
            preset,
            requested,
            availableWidthDip: 2000,
            availableHeightDip: 2000);

        Assert.Equal(
            expected,
            PanelZoomCalculator.DecreaseFromEffective(
                preset,
                layout.EffectivePercentage));
    }

    [Theory]
    [InlineData(110.00000000000001, 100)]
    [InlineData(100, 90)]
    [InlineData(120, 110)]
    [InlineData(114, 110)]
    [InlineData(110.1, 110)]
    public void DecreaseFromEffective_IgnoresStepNoiseButPreservesFractionalCaps(
        double effective,
        double expected)
    {
        Assert.Equal(
            expected,
            PanelZoomCalculator.DecreaseFromEffective(
                PanelSizePreset.Standard,
                effective));
    }

    [Fact]
    public void DecreaseFromEffective_FractionalMonitorCapTakesTheVisibleStep()
    {
        var layout = PanelZoomCalculator.CalculateLayout(
            PanelSizePreset.Standard,
            requestedPercentage: 140,
            availableWidthDip: 342,
            availableHeightDip: 2000);

        Assert.Equal(114, layout.EffectivePercentage, precision: 6);
        Assert.Equal(
            110,
            PanelZoomCalculator.DecreaseFromEffective(
                PanelSizePreset.Standard,
                layout.EffectivePercentage));
    }

    [Theory]
    [InlineData(408.4, 1, 409)]
    [InlineData(408.4, 1.25, 511)]
    [InlineData(408.4, 1.5, 613)]
    [InlineData(0, 1.25, 1)]
    [InlineData(double.NaN, 1.25, 1)]
    [InlineData(408.4, double.NaN, 1)]
    public void ToPhysicalPixels_CeilsToAWorkAreaSafeWholePixel(
        double size,
        double dpiScale,
        int expected)
    {
        Assert.Equal(
            expected,
            PanelZoomCalculator.ToPhysicalPixels(size, dpiScale));
    }
    [Theory]
    [InlineData(PanelSizePreset.Standard, 80, 240, 408.4)]
    [InlineData(PanelSizePreset.Standard, 100, 300, 500)]
    [InlineData(PanelSizePreset.Standard, 140, 420, 683.2)]
    [InlineData(PanelSizePreset.Large, 80, 448, 616.4)]
    [InlineData(PanelSizePreset.Large, 100, 560, 760)]    public void Layout_ScalesBodyAndWidthButKeepsHeaderUnscaled(
        PanelSizePreset preset,
        double percentage,
        double expectedWidth,
        double expectedHeight)
    {
        var result = PanelZoomCalculator.CalculateLayout(
            preset,
            percentage,
            availableWidthDip: 2000,
            availableHeightDip: 2000);

        Assert.Equal(percentage, result.EffectivePercentage);
        Assert.Equal(expectedWidth, result.WindowSize.Width, precision: 6);
        Assert.Equal(expectedHeight, result.WindowSize.Height, precision: 6);
        Assert.Equal(
            PanelSizeCalculator.GetRequestedActiveToolSize(preset),
            result.LogicalSize);
    }

    [Fact]
    public void Layout_AtOneHundredPercentPreservesExistingConstrainedGeometry()
    {
        var existing = PanelSizeCalculator.GetActiveToolSize(
            PanelSizePreset.Large,
            availableWidthDip: 480,
            availableHeightDip: 620);
        var result = PanelZoomCalculator.CalculateLayout(
            PanelSizePreset.Large,
            100,
            availableWidthDip: 480,
            availableHeightDip: 620);

        Assert.Equal(existing, result.LogicalSize);
        Assert.Equal(existing, result.WindowSize);
        Assert.Equal(100, result.EffectivePercentage);
    }

    [Fact]
    public void Layout_CapsEffectiveZoomToBothMonitorDimensions()
    {
        var widthLimited = PanelZoomCalculator.CalculateLayout(
            PanelSizePreset.Standard,
            140,
            availableWidthDip: 360,
            availableHeightDip: 2000);
        var heightLimited = PanelZoomCalculator.CalculateLayout(
            PanelSizePreset.Standard,
            140,
            availableWidthDip: 2000,
            availableHeightDip: 591.6);

        Assert.Equal(120, widthLimited.EffectivePercentage, precision: 6);
        Assert.Equal(360, widthLimited.WindowSize.Width, precision: 6);
        Assert.Equal(591.6, widthLimited.WindowSize.Height, precision: 6);
        Assert.Equal(120, heightLimited.EffectivePercentage, precision: 6);
        Assert.Equal(360, heightLimited.WindowSize.Width, precision: 6);
        Assert.Equal(591.6, heightLimited.WindowSize.Height, precision: 6);
    }

    [Theory]
    [InlineData(PanelSizePreset.Standard, 80, 240, 408.4)]
    [InlineData(PanelSizePreset.Standard, 90, 270, 454.2)]
    [InlineData(PanelSizePreset.Standard, 100, 300, 500)]
    [InlineData(PanelSizePreset.Large, 80, 448, 616.4)]
    [InlineData(PanelSizePreset.Large, 90, 504, 688.2)]
    [InlineData(PanelSizePreset.Large, 100, 560, 760)]
    public void AuthoritativeLayout_RemainsStableAtBottomAndAfterMovingUp(
        PanelSizePreset preset,
        double percentage,
        double expectedWidth,
        double expectedHeight)
    {
        foreach (var dpiScale in new[] { 1d, 1.25d, 1.5d })
        {
            var workWidth = (int)Math.Round(1600 * dpiScale);
            var workHeight = (int)Math.Round(1040 * dpiScale);
            var toolbarWidth = (int)Math.Round(80 * dpiScale);
            var toolbarHeight = (int)Math.Round(48 * dpiScale);
            var monitor = new MonitorWorkArea(
                "display",
                new PixelRect(0, 0, workWidth, workHeight),
                new PixelRect(0, 0, workWidth, workHeight),
                dpiScale,
                true);
            var layout = PanelZoomCalculator.CalculateLayout(
                preset,
                percentage,
                workWidth / dpiScale,
                (workHeight - toolbarHeight) / dpiScale);
            var panelWidth = PanelZoomCalculator.ToPhysicalPixels(
                layout.WindowSize.Width,
                dpiScale);
            var panelHeight = PanelZoomCalculator.ToPhysicalPixels(
                layout.WindowSize.Height,
                dpiScale);

            Assert.Equal(percentage, layout.EffectivePercentage);
            Assert.Equal(expectedWidth, layout.WindowSize.Width, 6);
            Assert.Equal(expectedHeight, layout.WindowSize.Height, 6);

            foreach (var dockSide in new[] { DockSide.Left, DockSide.Right })
            {
                var toolbarLeft = dockSide == DockSide.Left
                    ? 0
                    : workWidth - toolbarWidth;
                var nearBottom = PanelWindowPlacementCalculator.Calculate(
                    new PixelRect(
                        toolbarLeft,
                        workHeight - toolbarHeight - 4,
                        toolbarLeft + toolbarWidth,
                        workHeight - 4),
                    panelWidth,
                    monitor,
                    dockSide,
                    panelHeight);
                var movedUp = PanelWindowPlacementCalculator.Calculate(
                    new PixelRect(
                        toolbarLeft,
                        toolbarHeight,
                        toolbarLeft + toolbarWidth,
                        toolbarHeight * 2),
                    panelWidth,
                    monitor,
                    dockSide,
                    panelHeight);

                Assert.Equal(
                    workHeight,
                    nearBottom.PanelPosition.Y + panelHeight);
                Assert.Equal(
                    nearBottom.ToolbarPosition.Y + toolbarHeight,
                    nearBottom.PanelPosition.Y);
                Assert.Equal(toolbarHeight, movedUp.ToolbarPosition.Y);
                Assert.Equal(
                    movedUp.ToolbarPosition.Y + toolbarHeight,
                    movedUp.PanelPosition.Y);
                Assert.InRange(
                    nearBottom.PanelPosition.X,
                    monitor.WorkArea.Left,
                    monitor.WorkArea.Right - panelWidth);
                Assert.InRange(
                    movedUp.PanelPosition.X,
                    monitor.WorkArea.Left,
                    monitor.WorkArea.Right - panelWidth);
            }
        }
    }
    [Theory]
    [InlineData(PanelSizePreset.Standard)]
    [InlineData(PanelSizePreset.Large)]
    public void FixedHostLayout_KeepsOneHostSizeAcrossEveryZoomStep(
        PanelSizePreset preset)
    {
        ToolSize? expectedHost = null;

        for (var percentage = 80d;
             percentage <= PanelZoomCalculator.GetMaximumPercentage(preset);
             percentage += 10)
        {
            var layout = PanelZoomCalculator.CalculateFixedHostLayout(
                preset,
                percentage,
                availableWidthDip: 2000,
                availableHeightDip: 1200);

            expectedHost ??= layout.HostLayout.WindowSize;
            Assert.Equal(expectedHost, layout.HostLayout.WindowSize);
            Assert.Equal(
                percentage,
                layout.VisibleLayout.EffectivePercentage,
                precision: 6);
            Assert.True(
                layout.VisibleLayout.WindowSize.Width
                <= layout.HostLayout.WindowSize.Width);
            Assert.True(
                layout.VisibleLayout.WindowSize.Height
                <= layout.HostLayout.WindowSize.Height);
        }
    }
    [Fact]
    public void Layout_PreservesRequestedPreferenceAcrossDifferentMonitors()
    {
        const double requested = 140;

        var constrained = PanelZoomCalculator.CalculateLayout(
            PanelSizePreset.Standard,
            requested,
            availableWidthDip: 360,
            availableHeightDip: 591.6);
        var roomy = PanelZoomCalculator.CalculateLayout(
            PanelSizePreset.Standard,
            requested,
            availableWidthDip: 2000,
            availableHeightDip: 2000);

        Assert.Equal(120, constrained.EffectivePercentage, precision: 6);
        Assert.Equal(140, roomy.EffectivePercentage);
    }

    [Theory]
    [InlineData(DockSide.Left)]
    [InlineData(DockSide.Right)]
    public void ZoomedWidth_RemainsInsideWorkAreaOnEitherDockSide(DockSide dockSide)
    {
        var layout = PanelZoomCalculator.CalculateLayout(
            PanelSizePreset.Large,
            140,
            availableWidthDip: 700,
            availableHeightDip: 900);
        var monitor = new MonitorWorkArea(
            "display",
            new PixelRect(0, 0, 700, 1000),
            new PixelRect(0, 0, 700, 1000),
            1,
            true);
        var placement = PanelWindowPlacementCalculator.Calculate(
            new PixelRect(0, 0, 48, 48),
            (int)Math.Round(layout.WindowSize.Width),
            monitor,
            dockSide,
            (int)Math.Round(layout.WindowSize.Height));

        Assert.True(placement.PanelPosition.X >= monitor.WorkArea.Left);
        Assert.True(
            placement.PanelPosition.X + Math.Round(layout.WindowSize.Width)
            <= monitor.WorkArea.Right);
    }

    [Fact]
    public void Layout_UsesLogicalDipsIndependentlyOfMonitorDpi()
    {
        var oneX = PanelZoomCalculator.CalculateLayout(
            PanelSizePreset.Standard, 140, 1920, 1032);
        var twoX = PanelZoomCalculator.CalculateLayout(
            PanelSizePreset.Standard, 140, 3840 / 2d, 2064 / 2d);

        Assert.Equal(oneX, twoX);
    }
}
